using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using static RecruitOps.Domain.PipelineAnalytics;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 5.2's Pipeline &amp; source page (ADR-0032), drawn in <c>design/internal/analytics-pipeline.html</c>.
///
/// <para><b>The charts are a cohort</b>: the applications that arrived in the window, on the company's calendar,
/// under the position and source filters. <b>The leaderboard counts events</b> in the window — CVs uploaded,
/// interviews held, offers accepted — on applications under the same filters. It is computed only for HR
/// Director and Admin and is absent for everyone else.</para>
///
/// <para>Reads the cohort and computes in memory, as the executive dashboard does: an in-house install
/// receives applications in the thousands a year, not millions (ADR-0004).</para></summary>
public class PipelineAnalyticsService : IPipelineAnalyticsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    private readonly TimeProvider _clock;

    public PipelineAnalyticsService(AppDbContext db, ICurrentUser user, ICurrentTenant tenant, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _clock = clock;
    }

    private enum Scope { Company, Recruiter }

    private static readonly ChannelGroup[] Named =
        [ChannelGroup.Facebook, ChannelGroup.LinkedIn, ChannelGroup.CareerPage, ChannelGroup.ManualUpload];

    public async Task<PipelineAnalyticsDto?> GetAsync(
        Window window, Guid? postingId, ChannelGroup? source, CancellationToken ct = default)
    {
        // Who sees the page (ADR-0032 §6). A Hiring Manager has no access to it at all (spec 5.2).
        if (_user.IsExcludedFromCandidateData) return null;
        Scope? scope = RoleScope.Parse(_user.Role) switch
        {
            UserRole.Admin or UserRole.HrDirector => Scope.Company,
            UserRole.Recruiter => Scope.Recruiter,
            _ => null,
        };
        if (scope is null) return null;

        var zone = await _db.Companies.Where(c => c.Id == _tenant.TenantId).Select(c => c.TimeZoneId).FirstOrDefaultAsync(ct);
        var (from, to) = ToDate(window, OfferPortal.Today(_clock.GetUtcNow(), zone));
        // A day either side covers any time zone; the exact day is decided in memory.
        var after = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var before = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(2);
        bool InWindow(DateTimeOffset at)
        {
            var day = OfferPortal.Today(at, zone);
            return day >= from && day <= to;
        }
        bool Filtered(Guid posting, SourceChannel channel) =>
            (postingId is null || posting == postingId) && (source is null || GroupOf(channel) == source);

        // The window's applications, before the position and source filters: the position choices come from
        // them, so a choice never vanishes from its own list when the other filter moves.
        var arrived = (await (
                from a in _db.JobApplications
                join p in _db.JobPostings on a.JobPostingId equals p.Id
                where a.AppliedAt >= after && a.AppliedAt < before
                select new { a.Id, a.JobPostingId, p.Title, a.Source, a.Status, a.AppliedAt })
            .AsNoTracking().ToListAsync(ct))
            .Where(a => InWindow(a.AppliedAt))
            .ToList();
        var cohort = arrived.Where(a => Filtered(a.JobPostingId, a.Source)).ToList();
        var ids = cohort.Select(a => a.Id).ToList();

        var history = (await _db.ApplicationStageHistories.AsNoTracking()
                .Where(h => ids.Contains(h.JobApplicationId))
                .Select(h => new { h.JobApplicationId, h.ToStatus })
                .ToListAsync(ct))
            .ToLookup(h => h.JobApplicationId, h => h.ToStatus);

        // A hire is an accepted offer (ADR-0031), however long after the window it came.
        var hired = (await _db.Offers.AsNoTracking()
                .Where(o => o.Status == OfferStatus.Accepted && ids.Contains(o.JobApplicationId))
                .Select(o => o.JobApplicationId)
                .ToListAsync(ct))
            .ToHashSet();

        var total = cohort.Count;
        var sources = new List<ChannelShareDto>();
        foreach (var group in Named.Append(ChannelGroup.Other))
        {
            var rows = cohort.Where(a => GroupOf(a.Source) == group).ToList();
            if (group == ChannelGroup.Other && rows.Count == 0) continue;
            var hires = rows.Count(a => hired.Contains(a.Id));
            sources.Add(new ChannelShareDto(
                group.ToString(),
                rows.Count,
                total == 0 ? null : Round(100.0 * rows.Count / total),
                hires,
                rows.Count == 0 ? null : Round(100.0 * hires / rows.Count)));
        }

        var reached = cohort.Select(a => Reached(history[a.Id].Append(a.Status))).ToList();
        var funnel = new List<FunnelBandDto>();
        int? previous = null;
        foreach (var band in Enum.GetValues<Band>())
        {
            var count = reached.Count(b => b >= band);
            funnel.Add(new FunnelBandDto(
                band.ToString(),
                count,
                previous is not { } p || p == 0 ? null : Round(100.0 * (p - count) / p)));
            previous = count;
        }

        var leaderboard = scope == Scope.Company
            ? await LeaderboardAsync(cohort.Select(a => a.Id).ToList(), after, before, InWindow, Filtered, ct)
            : null;

        var positions = arrived
            .GroupBy(a => a.JobPostingId)
            .Select(g => new FilterOptionDto(g.Key, g.First().Title))
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .ToList();

        return new PipelineAnalyticsDto(
            scope.Value.ToString(),
            window == Window.Year ? "year" : "90d",
            from,
            to,
            total,
            sources,
            funnel,
            leaderboard,
            positions);
    }

    /// <summary>Per person, what they did in the window on applications under the filters (ADR-0032 §5): CVs
    /// they uploaded that arrived in it, rounds they scheduled that were held in it (not cancelled), and
    /// offers they made that were accepted in it. Listed by name — never ranked.</summary>
    private async Task<IReadOnlyList<LeaderboardRowDto>> LeaderboardAsync(
        List<Guid> cohortIds, DateTimeOffset after, DateTimeOffset before,
        Func<DateTimeOffset, bool> inWindow, Func<Guid, SourceChannel, bool> filtered, CancellationToken ct)
    {
        // The cohort is already the window's arrivals under the filters.
        var cvs = await (
                from f in _db.BulkUploadFiles
                join b in _db.BulkUploadBatches on f.BulkUploadBatchId equals b.Id
                where f.JobApplicationId != null && cohortIds.Contains(f.JobApplicationId.Value) && b.UploadedByUserId != null
                select b.UploadedByUserId!.Value) // non-null: filtered in the where clause
            .ToListAsync(ct);

        var interviews = (await (
                from i in _db.Interviews
                join a in _db.JobApplications on i.JobApplicationId equals a.Id
                where i.ScheduledByUserId != null && i.Status != InterviewStatus.Cancelled
                      && i.ScheduledStart >= after && i.ScheduledStart < before
                select new { By = i.ScheduledByUserId!.Value, i.ScheduledStart, a.JobPostingId, a.Source }) // non-null: filtered above
            .AsNoTracking().ToListAsync(ct))
            .Where(i => inWindow(i.ScheduledStart) && filtered(i.JobPostingId, i.Source))
            .Select(i => i.By)
            .ToList();

        var hires = (await (
                from o in _db.Offers
                join a in _db.JobApplications on o.JobApplicationId equals a.Id
                where o.Status == OfferStatus.Accepted && o.RespondedAt >= after && o.RespondedAt < before
                select new { o.CreatedByUserId, o.RespondedAt, a.JobPostingId, a.Source })
            .AsNoTracking().ToListAsync(ct))
            .Where(o => o.RespondedAt is { } at && inWindow(at) && filtered(o.JobPostingId, o.Source))
            .Select(o => o.CreatedByUserId)
            .ToList();

        // One pass per list, not a count per person per list.
        static Dictionary<Guid, int> Tally(List<Guid> ids) => ids.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var cvsBy = Tally(cvs);
        var interviewsBy = Tally(interviews);
        var hiresBy = Tally(hires);

        var people = cvsBy.Keys.Union(interviewsBy.Keys).Union(hiresBy.Keys).ToList();
        // Display names only: a colleague's email is users:read (ADR-0019).
        var names = await _db.Users.AsNoTracking()
            .Where(u => people.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return people
            .Where(names.ContainsKey)
            .Select(id => new LeaderboardRowDto(
                id, names[id], cvsBy.GetValueOrDefault(id), interviewsBy.GetValueOrDefault(id), hiresBy.GetValueOrDefault(id)))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.UserId)
            .ToList();
    }

    /// <summary>To one decimal, a half rounded up (ADR-0031 §1).</summary>
    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
