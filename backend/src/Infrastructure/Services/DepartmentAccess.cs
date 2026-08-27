using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Looks the user's departments up in the database on each request
/// (ADR-0003). Chosen over embedding them in the JWT so that revoking access takes
/// effect immediately — a stale access-control claim is a security problem, and the
/// token lives for 8 hours.
/// <para>Registered scoped, so the result is computed at most once per request.</para></summary>
public class DepartmentAccess : IDepartmentAccess
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private IReadOnlyCollection<Guid>? _cached;

    public DepartmentAccess(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IReadOnlyCollection<Guid>> AccessibleDepartmentIdsAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;

        var userId = _user.UserId;
        if (userId is null)
        {
            _cached = Array.Empty<Guid>();
            return _cached;
        }

        _cached = await _db.UserDepartments
            .AsNoTracking()
            .Where(ud => ud.UserId == userId.Value)
            .Select(ud => ud.DepartmentId)
            .ToListAsync(ct);

        return _cached;
    }

    public async Task<bool> CanAccessAsync(Guid departmentId, CancellationToken ct = default)
    {
        // Unscoped roles (Admin / HrDirector / Recruiter) work across all departments.
        // ⚠️ An Approver lands here too and gets `true` for every department — correct on the
        // requisition axis, catastrophic if asked about a candidate. See CanReachCandidatesInAsync.
        if (!_user.IsDepartmentScoped) return true;

        var allowed = await AccessibleDepartmentIdsAsync(ct);
        return allowed.Contains(departmentId);
    }

    /// <summary>The candidate axis: department scoping AND the ADR-0018 exclusion, in one place.
    ///
    /// <para>The order matters for cost, not correctness — the exclusion is a claim check with no
    /// database round trip, so testing it first short-circuits the query for a role that could
    /// never pass anyway.</para></summary>
    public async Task<bool> CanReachCandidatesInAsync(Guid departmentId, CancellationToken ct = default)
        => !_user.IsExcludedFromCandidateData
           && await CanAccessAsync(departmentId, ct);
}
