using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class PipelineService : IPipelineService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _access;
    private readonly TimeProvider _clock;

    public PipelineService(
        AppDbContext db, ICurrentUser user, IDepartmentAccess access, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _access = access;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PipelineItemDto>?> GetForPostingAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == jobPostingId, ct);

        // Null, not empty: "no such posting" and "a posting with no applicants" are
        // different answers, and only one of them should be a 404.
        if (posting is null) return null;
        if (!await CanReachCandidatesInAsync(posting.DepartmentId, ct)) return null;

        var rows = await (
            from a in _db.JobApplications.AsNoTracking()
            join c in _db.Candidates.AsNoTracking() on a.CandidateId equals c.Id
            where a.JobPostingId == jobPostingId
            orderby a.AppliedAt descending
            select new { a, c }
        ).ToListAsync(ct);

        // Projected in memory: enum.ToString() does not translate to SQL in EF Core 10.
        return rows.Select(x => new PipelineItemDto(
            x.a.Id,
            x.c.Id,
            x.c.FullName,
            x.c.Email,
            x.c.Phone,
            x.a.Status.ToString(),
            x.a.Source.ToString(),
            x.a.AppliedAt,
            x.a.CoverNote,
            x.a.CustomFieldsJson
        )).ToList();
    }

    public async Task<PipelineItemDto?> MoveStageAsync(
        Guid applicationId, MoveStageRequest request, CancellationToken ct = default)
    {
        var application = await _db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application is null) return null;

        var posting = await _db.JobPostings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == application.JobPostingId, ct);
        if (posting is null) return null;
        if (!await CanReachCandidatesInAsync(posting.DepartmentId, ct)) return null;

        if (!Enum.TryParse<PipelineStatus>(request.ToStatus, ignoreCase: false, out var target))
            throw new InvalidOperationException($"'{request.ToStatus}' is not a pipeline stage.");

        // Hired and Rejected are terminal. Moving out of them would silently corrupt
        // time-to-hire and offer-acceptance figures, which are read from this history.
        if (application.Status is PipelineStatus.Hired or PipelineStatus.Rejected)
            throw new InvalidOperationException(
                $"This application is {application.Status}; that is final. Re-apply instead of reopening.");

        // A no-op move would write a history row saying nothing changed, and Module 5 would
        // count it as a stage transition.
        if (application.Status == target)
            throw new InvalidOperationException($"This application is already at {target}.");

        var now = _clock.GetUtcNow();
        var from = application.Status;

        application.Status = target;
        application.UpdatedAt = now;

        _db.ApplicationStageHistories.Add(new ApplicationStageHistory
        {
            TenantId = application.TenantId,
            JobApplicationId = application.Id,
            FromStatus = from,
            ToStatus = target,
            ChangedAt = now,
            ChangedByUserId = _user.UserId,
            Note = request.Note,
        });

        await _db.SaveChangesAsync(ct);

        var candidate = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == application.CandidateId, ct);

        return new PipelineItemDto(
            application.Id,
            application.CandidateId,
            candidate?.FullName ?? string.Empty,
            candidate?.Email,
            candidate?.Phone,
            application.Status.ToString(),
            application.Source.ToString(),
            application.AppliedAt,
            application.CoverNote,
            application.CustomFieldsJson);
    }

    public async Task<IReadOnlyList<StageHistoryItemDto>?> GetHistoryAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        var application = await _db.JobApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application is null) return null;

        var posting = await _db.JobPostings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == application.JobPostingId, ct);
        if (posting is null) return null;
        if (!await CanReachCandidatesInAsync(posting.DepartmentId, ct)) return null;

        var rows = await _db.ApplicationStageHistories.AsNoTracking()
            .Where(h => h.JobApplicationId == applicationId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(ct);

        var actorIds = rows.Where(h => h.ChangedByUserId is not null)
            .Select(h => h.ChangedByUserId!.Value).Distinct().ToList();

        var names = await _db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return rows.Select(h => new StageHistoryItemDto(
            h.FromStatus?.ToString(),
            h.ToStatus.ToString(),
            h.ChangedAt,
            h.ChangedByUserId is null ? null : names.GetValueOrDefault(h.ChangedByUserId.Value),
            h.Note
        )).ToList();
    }

    // ---------- helpers ----------

    /// <summary>Department scoping (ADR-0003) <b>plus</b> the candidate-data exclusion
    /// (ADR-0018), through one door so a fourth method cannot get only half of it.
    ///
    /// <para><c>CanAccessAsync</c> on its own is not the right question here. It answers
    /// "does this role work across departments", and an Approver does — on the requisition
    /// axis, which is what ADR-0003 was arguing about. Asked about a <i>candidate</i>, the
    /// same true handed an Approver the whole company's pipeline, stage history included.
    /// They reach an individual application by sitting on its panel (ADR-0017 §4), which
    /// <c>IApplicationAccess</c> resolves; nothing here grants standing reach.</para>
    /// </summary>
    private async Task<bool> CanReachCandidatesInAsync(Guid departmentId, CancellationToken ct)
        => !_user.IsExcludedFromCandidateData
           && await _access.CanAccessAsync(departmentId, ct);
}
