using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 5.1's executive dashboard (ADR-0031), drawn in <c>design/internal/analytics-dashboard.html</c>.
///
/// <para><b>Every number is computed over the caller's rows.</b> A Recruiter's tiles are the offers they
/// made, recomputed — never the company's average filtered for display, which would disclose the other
/// recruiters' numbers. A Hiring Manager's are their departments', and Time-to-Hire and the acceptance
/// rate are not computed for them at all.</para>
///
/// <para><b>Offers are counted as <c>OfferService</c> would leave them</b> (ADR-0030 §5a): one still Sent or
/// Viewed past its expiry date is expired here too — read, not written.</para>
///
/// <para>Reads everything an offer is measured from in one query and computes in memory: an in-house
/// install makes offers in the hundreds a year (ADR-0004), and the rules read more plainly as code than
/// as SQL.</para></summary>
public class ExecutiveDashboardService : IExecutiveDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    private readonly IDepartmentAccess _departments;
    private readonly TimeProvider _clock;

    public ExecutiveDashboardService(
        AppDbContext db, ICurrentUser user, ICurrentTenant tenant, IDepartmentAccess departments, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _departments = departments;
        _clock = clock;
    }

    private enum Scope { Company, Recruiter, Department }

    /// <summary>One offer, with what it is measured from.</summary>
    private sealed record OfferRow(
        OfferStatus Status,
        DateTimeOffset? RespondedAt,
        DateOnly ExpiresOn,
        Guid CreatedByUserId,
        DateTimeOffset AppliedAt,
        Guid DepartmentId,
        DateTimeOffset? RequisitionApprovedAt);

    public async Task<ExecutiveDashboardDto?> GetAsync(ReportingPeriod.Kind kind, Guid? departmentId, Guid? recruiterId, CancellationToken ct = default)
    {
        // Who sees what (ADR-0031 §3). A custom role carries its base role.
        if (_user.IsExcludedFromCandidateData) return null;
        var role = RoleScope.Parse(_user.Role);
        var scope = role switch
        {
            UserRole.Admin or UserRole.HrDirector => Scope.Company,
            UserRole.Recruiter => Scope.Recruiter,
            UserRole.HiringManager => Scope.Department,
            _ => (Scope?)null,
        };
        if (scope is null) return null;

        HashSet<Guid>? reach = null;
        if (scope == Scope.Department)
        {
            reach = (await _departments.AccessibleDepartmentIdsAsync(ct)).ToHashSet();
            if (departmentId is { } asked && !reach.Contains(asked)) return null;
        }

        // A recruiter's rows are their own whatever they ask for; a Hiring Manager's are every recruiter's in
        // their departments. Only company-wide viewers choose.
        Guid? recruiter = scope switch
        {
            Scope.Recruiter => _user.UserId ?? Guid.Empty,
            Scope.Company => recruiterId,
            _ => null,
        };

        var zone = await _db.Companies.Where(c => c.Id == _tenant.TenantId).Select(c => c.TimeZoneId).FirstOrDefaultAsync(ct);
        var today = OfferPortal.Today(_clock.GetUtcNow(), zone);
        var current = ReportingPeriod.ToDate(kind, today);
        var previous = ReportingPeriod.Previous(kind, today);

        var offers = await OffersAsync(previous.From, ct);
        var inReach = reach is null ? offers : offers.Where(o => reach.Contains(o.DepartmentId)).ToList();
        var measured = inReach.Where(o =>
            (departmentId is null || o.DepartmentId == departmentId)
            && (recruiter is null || o.CreatedByUserId == recruiter)).ToList();

        DateOnly Day(DateTimeOffset at) => OfferPortal.Today(at, zone);
        bool In((DateOnly From, DateOnly To) p, DateOnly day) => day >= p.From && day <= p.To;

        // Accepted offers, by the day they were accepted.
        List<OfferRow> AcceptedIn((DateOnly From, DateOnly To) p) => measured
            .Where(o => o.Status == OfferStatus.Accepted && o.RespondedAt is { } at && In(p, Day(at))).ToList();

        DurationMetricDto Duration(Func<OfferRow, DateTimeOffset?> from)
        {
            static (double? Days, int Count) Average(IEnumerable<double> days)
            {
                var list = days.ToList();
                return list.Count == 0 ? (null, 0) : (Round(list.Average()), list.Count);
            }
            IEnumerable<double> Days(List<OfferRow> rows) => rows
                .Where(o => from(o) is not null)
                .Select(o => Math.Max(0, (o.RespondedAt!.Value - from(o)!.Value).TotalDays)); // AcceptedIn: RespondedAt is set
            var now = Average(Days(AcceptedIn(current)));
            var then = Average(Days(AcceptedIn(previous)));
            return new DurationMetricDto(now.Days, now.Count, then.Days, then.Count);
        }

        AcceptanceMetricDto Acceptance()
        {
            (int Accepted, int Decided) Outcomes((DateOnly From, DateOnly To) p)
            {
                int accepted = 0, decided = 0;
                foreach (var o in measured)
                {
                    var outcome = Outcome(o, today, zone);
                    if (outcome is null || !In(p, outcome.Value.On)) continue;
                    decided++;
                    if (outcome.Value.Accepted) accepted++;
                }
                return (accepted, decided);
            }
            static double? Rate((int Accepted, int Decided) x) =>
                x.Decided == 0 ? null : Round(100.0 * x.Accepted / x.Decided);
            var now = Outcomes(current);
            var then = Outcomes(previous);
            return new AcceptanceMetricDto(now.Accepted, now.Decided, Rate(now), then.Accepted, then.Decided, Rate(then));
        }

        var departmentNames = await _db.Departments
            .Where(d => reach == null || reach.Contains(d.Id))
            .Select(d => new { d.Id, d.Name, d.IsActive })
            .ToListAsync(ct);

        var (vacancies, open) = await VacanciesAsync(reach, departmentId, departmentNames.ToDictionary(d => d.Id, d => d.Name), ct);

        // Not narrowed by the department filter: a choice must not vanish from its own list when the
        // other filter moves.
        var recruiters = scope == Scope.Company
            ? await RecruitersAsync(inReach.Select(o => o.CreatedByUserId).Distinct().ToList(), ct)
            : Array.Empty<FilterOptionDto>();

        return new ExecutiveDashboardDto(
            scope.Value.ToString(),
            new ReportingPeriodDto(kind.ToString(), current.From, current.To),
            new ReportingPeriodDto(kind.ToString(), previous.From, previous.To),
            scope == Scope.Department ? null : Duration(o => o.AppliedAt),
            Duration(o => o.RequisitionApprovedAt),
            scope == Scope.Department ? null : Acceptance(),
            vacancies,
            open,
            departmentNames.Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new FilterOptionDto(d.Id, d.Name)).ToList(),
            recruiters);
    }

    /// <summary>To one decimal, a half rounded up as a person checking by hand would: 30.25 days is 30.3, where
    /// <see cref="Math.Round(double, int)"/>'s default (to even) says 30.2.</summary>
    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>Offers that may have an outcome on or after <paramref name="since"/> — the previous period's
    /// first day. Bounded loosely (a day either side covers any time zone); the exact day is decided in
    /// memory, on the company's calendar.</summary>
    private Task<List<OfferRow>> OffersAsync(DateOnly since, CancellationToken ct)
    {
        var after = new DateTimeOffset(since.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var expiringAfter = since.AddDays(-2);
        return (
            from o in _db.Offers
            join a in _db.JobApplications on o.JobApplicationId equals a.Id
            join p in _db.JobPostings on a.JobPostingId equals p.Id
            join r in _db.Requisitions on p.RequisitionId equals r.Id
            where ((o.Status == OfferStatus.Accepted || o.Status == OfferStatus.Rejected) && o.RespondedAt >= after)
                  || ((o.Status == OfferStatus.Expired || o.Status == OfferStatus.Sent || o.Status == OfferStatus.Viewed)
                      && o.ExpiresOn >= expiringAfter)
            select new OfferRow(o.Status, o.RespondedAt, o.ExpiresOn, o.CreatedByUserId, a.AppliedAt, p.DepartmentId,
                r.Status == RequisitionStatus.Approved ? r.DecidedAt : null)
        ).AsNoTracking().ToListAsync(ct);
    }

    /// <summary>An offer's outcome and the company day it fell on — or null while it is still waiting. An
    /// offer Sent or Viewed past its expiry date has expired, as <c>OfferService</c> would record on its next
    /// read; an expiry falls on the day after the last day it could be answered.</summary>
    private static (bool Accepted, DateOnly On)? Outcome(OfferRow o, DateOnly today, string? zone) => o.Status switch
    {
        OfferStatus.Accepted when o.RespondedAt is { } at => (true, OfferPortal.Today(at, zone)),
        OfferStatus.Rejected when o.RespondedAt is { } at => (false, OfferPortal.Today(at, zone)),
        OfferStatus.Expired => (false, o.ExpiresOn.AddDays(1)),
        OfferStatus.Sent or OfferStatus.Viewed when o.ExpiresOn < today => (false, o.ExpiresOn.AddDays(1)),
        _ => null,
    };

    /// <summary>Per department: each approved requisition's headcount less the offers accepted for it, never
    /// below zero (ADR-0031 §4). Not narrowed by recruiter — vacancies are not anyone's performance.</summary>
    private async Task<(IReadOnlyList<DepartmentVacanciesDto> Vacancies, int Open)> VacanciesAsync(
        HashSet<Guid>? reach, Guid? departmentId, Dictionary<Guid, string> names, CancellationToken ct)
    {
        var requisitions = await _db.Requisitions.AsNoTracking()
            .Where(r => r.Status == RequisitionStatus.Approved
                        && (reach == null || reach.Contains(r.DepartmentId))
                        && (departmentId == null || r.DepartmentId == departmentId))
            .Select(r => new { r.Id, r.DepartmentId, r.Headcount })
            .ToListAsync(ct);
        var ids = requisitions.Select(r => r.Id).ToList();
        var filled = await (
            from o in _db.Offers
            join a in _db.JobApplications on o.JobApplicationId equals a.Id
            join p in _db.JobPostings on a.JobPostingId equals p.Id
            where o.Status == OfferStatus.Accepted && ids.Contains(p.RequisitionId)
            group o by p.RequisitionId into g
            select new { RequisitionId = g.Key, Count = g.Count() }
        ).AsNoTracking().ToDictionaryAsync(x => x.RequisitionId, x => x.Count, ct);

        var byDepartment = requisitions
            .GroupBy(r => r.DepartmentId)
            .Select(g => new DepartmentVacanciesDto(
                g.Key,
                names.GetValueOrDefault(g.Key) ?? "Unknown department",
                g.Sum(r => Math.Max(0, r.Headcount - filled.GetValueOrDefault(r.Id)))))
            .Where(d => d.Open > 0)
            .OrderByDescending(d => d.Open).ThenBy(d => d.DepartmentName)
            .ToList();
        return (byDepartment, byDepartment.Sum(d => d.Open));
    }

    /// <summary>The people who made offers in reach — display names only: a colleague's email is
    /// <c>users:read</c> (ADR-0019).</summary>
    private async Task<IReadOnlyList<FilterOptionDto>> RecruitersAsync(List<Guid> ids, CancellationToken ct) =>
        (await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName }).ToListAsync(ct))
        .OrderBy(u => u.DisplayName)
        .Select(u => new FilterOptionDto(u.Id, u.DisplayName))
        .ToList();
}
