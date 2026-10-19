using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>The one place the "may this user touch this application" rule lives
/// (ADR-0017 §4). See <see cref="IApplicationAccess"/> for why it is not three copies.
/// <para>Scoped, with a tiny per-request cache: a single request routinely asks about the
/// same application from the controller, the service and a mention resolver.</para></summary>
public class ApplicationAccess : IApplicationAccess
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _departments;
    private readonly Dictionary<Guid, ApplicationReach?> _cache = new();

    public ApplicationAccess(AppDbContext db, ICurrentUser user, IDepartmentAccess departments)
    {
        _db = db;
        _user = user;
        _departments = departments;
    }

    public async Task<ApplicationReach?> ResolveAsync(
        Guid jobApplicationId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(jobApplicationId, out var cached)) return cached;

        var reach = await ResolveUncachedAsync(jobApplicationId, ct);
        _cache[jobApplicationId] = reach;
        return reach;
    }

    private async Task<ApplicationReach?> ResolveUncachedAsync(
        Guid jobApplicationId, CancellationToken ct)
    {
        var row = await LoadRowAsync(jobApplicationId, ct);
        if (row is null) return null;

        var userId = _user.UserId;
        if (userId is null) return null;

        // Clauses 0 and 1 together, and deliberately not spelled out here any more:
        //   0. roles with no standing reach into candidate data (ADR-0018) — an Approver is
        //      company-wide on the requisition axis, which used to mean CanAccessAsync said yes
        //      for every department and handed them every candidate in the company;
        //   1. department scoping (ADR-0003), which company-wide roles also pass.
        // Both live in IDepartmentAccess.CanReachCandidatesInAsync now. Writing them out
        // separately here is what let a later service copy only half of the pair.
        if (await _departments.CanReachCandidatesInAsync(row.DepartmentId, ct))
        {
            return ViaDepartment(row);
        }

        // Clause 2: the panel exception (ADR-0017 §4). Read-only, this application only — and,
        // for a role limited to its own rounds, only those (ResolveRoundAsync).
        return await IsOnPanelForAsync(userId.Value, jobApplicationId, ct)
            ? ViaParticipation(row, _user.IsLimitedToOwnRounds)
            : null;
    }

    public async Task<ApplicationReach?> ResolveForUserAsync(
        Guid userId, Guid jobApplicationId, CancellationToken ct = default)
    {
        var row = await LoadRowAsync(jobApplicationId, ct);
        if (row is null) return null;

        // Deactivated users reach nothing — the same answer login gives them.
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);

        if (user is null) return null;

        // The same three clauses as above, in the same order, against a supplied user instead
        // of the ambient one — and, critically, against the same RoleScope predicates. The
        // previous version of this rule lived in NoteService and spelled the role list out by
        // hand, so it would not have followed clause 0 in.
        if (!RoleScope.IsExcludedFromCandidateData(user.Role))
        {
            var reachesDepartment =
                !RoleScope.IsDepartmentScoped(user.Role)
                || await _db.UserDepartments.AsNoTracking()
                    .AnyAsync(ud => ud.UserId == userId
                                    && ud.DepartmentId == row.DepartmentId, ct);

            if (reachesDepartment) return ViaDepartment(row);
        }

        return await IsOnPanelForAsync(userId, jobApplicationId, ct)
            ? ViaParticipation(row, RoleScope.IsLimitedToOwnRounds(user.Role))
            : null;
    }

    public async Task<ApplicationReach?> ResolveByInterviewAsync(
        Guid interviewId, CancellationToken ct = default)
    {
        var applicationId = await _db.Interviews.AsNoTracking()
            .Where(i => i.Id == interviewId)
            .Select(i => (Guid?)i.JobApplicationId)
            .FirstOrDefaultAsync(ct);

        return applicationId is null ? null : await ResolveAsync(applicationId.Value, ct);
    }

    public async Task<ApplicationReach?> ResolveRoundAsync(Guid interviewId, CancellationToken ct = default)
    {
        var reach = await ResolveByInterviewAsync(interviewId, ct);
        if (reach is null || !reach.OwnRoundsOnly) return reach;

        // OwnRoundsOnly is only ever set on participation reach, which needs a signed-in user.
        return _user.UserId is { } userId && await SitsOnAsync(userId, interviewId, ct) ? reach : null;
    }

    public async Task<bool> SitsOnAsync(Guid userId, Guid interviewId, CancellationToken ct = default)
        => await (
            from ip in _db.InterviewParticipants.AsNoTracking()
            join i in _db.Interviews.AsNoTracking() on ip.InterviewId equals i.Id
            where ip.InterviewId == interviewId && ip.UserId == userId
                  && i.Status != InterviewStatus.Cancelled
            select ip.Id
        ).AnyAsync(ct);

    public async Task<bool> IsParticipantAsync(Guid interviewId, CancellationToken ct = default)
    {
        var userId = _user.UserId;
        if (userId is null) return false;

        return await _db.InterviewParticipants.AsNoTracking()
            .AnyAsync(p => p.InterviewId == interviewId && p.UserId == userId.Value, ct);
    }

    // ---------- helpers ----------

    private sealed record Row(Guid ApplicationId, Guid JobPostingId, Guid DepartmentId);

    /// <summary>The application and the department it hangs off, or null if there is no such
    /// application. Two-step (query in SQL, project in memory) is the house pattern — the
    /// anonymous type stays so nothing depends on EF translating a record constructor.</summary>
    private async Task<Row?> LoadRowAsync(Guid jobApplicationId, CancellationToken ct)
    {
        var row = await (
            from a in _db.JobApplications.AsNoTracking()
            join p in _db.JobPostings.AsNoTracking() on a.JobPostingId equals p.Id
            where a.Id == jobApplicationId
            select new { ApplicationId = a.Id, JobPostingId = p.Id, p.DepartmentId }
        ).FirstOrDefaultAsync(ct);

        return row is null ? null : new Row(row.ApplicationId, row.JobPostingId, row.DepartmentId);
    }

    /// <summary>The panel exception's test. A <b>cancelled</b> round grants nothing: its panel can no
    /// longer be edited, so someone seated on it by mistake — or on a round called off for any
    /// reason — kept the candidate's whole file for good (third security review, 2026-10-19, M1).
    /// <see cref="InterviewService"/>'s list applies the same cut; keep them together.</summary>
    private async Task<bool> IsOnPanelForAsync(
        Guid userId, Guid jobApplicationId, CancellationToken ct)
        => await (
            from ip in _db.InterviewParticipants.AsNoTracking()
            join i in _db.Interviews.AsNoTracking() on ip.InterviewId equals i.Id
            where i.JobApplicationId == jobApplicationId && ip.UserId == userId
                  && i.Status != InterviewStatus.Cancelled
            select ip.Id
        ).AnyAsync(ct);

    private static ApplicationReach ViaDepartment(Row row) => new(
        row.ApplicationId, row.JobPostingId, row.DepartmentId,
        ApplicationReachKind.Department);

    private static ApplicationReach ViaParticipation(Row row, bool ownRoundsOnly) =>
        new(row.ApplicationId, row.JobPostingId, row.DepartmentId, ApplicationReachKind.Participation)
        {
            OwnRoundsOnly = ownRoundsOnly,
        };
}
