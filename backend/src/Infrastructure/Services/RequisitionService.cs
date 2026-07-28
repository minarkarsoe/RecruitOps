using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class RequisitionService : IRequisitionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _access;
    private readonly TimeProvider _clock;

    public RequisitionService(
        AppDbContext db, ICurrentUser user, IDepartmentAccess access, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _access = access;
        _clock = clock;
    }

    // ── List ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RequisitionListItemDto>> GetRequisitionsAsync(CancellationToken ct = default)
    {
        var query = _db.Requisitions.AsNoTracking();

        // ADR-0003: department scoping is explicit, not a global query filter.
        if (_user.IsDepartmentScoped)
        {
            var allowed = await _access.AccessibleDepartmentIdsAsync(ct);
            if (allowed.Count == 0) return Array.Empty<RequisitionListItemDto>();
            query = query.Where(r => allowed.Contains(r.DepartmentId));
        }

        return await FetchListAsync(query, ct);
    }

    // ── Approver inbox ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RequisitionListItemDto>> GetInboxAsync(CancellationToken ct = default)
    {
        var userId = _user.UserId;
        if (userId is null) return Array.Empty<RequisitionListItemDto>();

        // Note this intentionally does NOT apply department scoping: an Approver is named on
        // the chain, not attached to a department (ADR-0003), so scoping would empty their
        // inbox. The per-step ApproverUserId match below is the access control here.

        // Step 1: only the requisitions where the caller has a Waiting step at all. Filtering
        // in SQL matters — the earlier version pulled every waiting approval row in the
        // company into memory on each inbox load.
        var candidateIds = await _db.RequisitionApprovals
            .AsNoTracking()
            .Where(a => a.Decision == ApprovalDecision.Waiting && a.ApproverUserId == userId.Value)
            .Select(a => a.RequisitionId)
            .Distinct()
            .ToListAsync(ct);

        if (candidateIds.Count == 0) return Array.Empty<RequisitionListItemDto>();

        // Step 2: of those, keep only the ones where it is actually the caller's TURN — the
        // lowest-sequence Waiting step must be theirs, or a later approver could act early.
        var waitingSteps = await _db.RequisitionApprovals
            .AsNoTracking()
            .Where(a => a.Decision == ApprovalDecision.Waiting && candidateIds.Contains(a.RequisitionId))
            .Select(a => new { a.RequisitionId, a.ApproverUserId, a.Sequence })
            .ToListAsync(ct);

        var myIds = waitingSteps
            .GroupBy(a => a.RequisitionId)
            // MinBy rather than First(): First() would silently depend on the query's ordering
            // surviving every future edit to the statement above.
            .Where(g => g.MinBy(a => a.Sequence)!.ApproverUserId == userId.Value)
            .Select(g => g.Key)
            .ToHashSet();

        if (myIds.Count == 0) return Array.Empty<RequisitionListItemDto>();

        // Status check matters: cancelling leaves the Waiting steps in place so the audit
        // trail stays truthful, so without this a cancelled requisition would sit in the
        // approver's inbox forever.
        var query = _db.Requisitions.AsNoTracking()
            .Where(r => myIds.Contains(r.Id) && r.Status == RequisitionStatus.PendingApproval);
        return await FetchListAsync(query, ct);
    }

    // ── Detail ───────────────────────────────────────────────────────────────

    public async Task<RequisitionDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.Requisitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (r is null) return null;
        if (!await _access.CanAccessAsync(r.DepartmentId, ct)) return null;

        return await BuildDetailAsync(r, ct);
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public async Task<RequisitionDetailDto?> CreateAsync(CreateRequisitionRequest request, CancellationToken ct = default)
    {
        if (!await _access.CanAccessAsync(request.DepartmentId, ct)) return null;

        // Must exist AND be active. A deactivated department is one the company has stopped
        // hiring into; letting a requisition be raised there would produce work nobody
        // intends to approve.
        var departmentUsable = await _db.Departments
            .AnyAsync(d => d.Id == request.DepartmentId && d.IsActive, ct);
        if (!departmentUsable) return null;

        var requisition = new Requisition
        {
            DepartmentId = request.DepartmentId,
            RequestedByUserId = _user.UserId ?? Guid.Empty,
            Title = request.Title,
            JobDescription = request.JobDescription,
            Headcount = request.Headcount,
            SalaryBudget = request.SalaryBudget,
            Status = RequisitionStatus.Draft,
        };

        _db.Requisitions.Add(requisition);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(requisition.Id, ct);
    }

    public async Task<RequisitionDetailDto?> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var requisition = await _db.Requisitions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (requisition is null) return null;
        if (!await _access.CanAccessAsync(requisition.DepartmentId, ct)) return null;

        // Same rule as edit and cancel. CanAccessAsync alone is not enough: it returns true
        // unconditionally for every non-department-scoped role, which includes Approver — so
        // without this an approver could push someone else's Draft into a chain and then
        // decide on it themselves.
        if (!IsOwnerOrCompanyWide(requisition)) return null;

        if (requisition.Status != RequisitionStatus.Draft)
            throw new InvalidOperationException($"Only a Draft can be submitted; this one is {requisition.Status}.");

        // Department-specific chain first, then the company-wide default (DepartmentId null).
        var chain = await _db.ApprovalChains
            .Where(c => c.IsActive && (c.DepartmentId == requisition.DepartmentId || c.DepartmentId == null))
            .OrderBy(c => c.DepartmentId == null ? 1 : 0)
            .FirstOrDefaultAsync(ct);

        if (chain is null)
            throw new InvalidOperationException("No active approval chain is configured for this department.");

        var steps = await _db.ApprovalChainSteps
            .Where(s => s.ApprovalChainId == chain.Id)
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

        if (steps.Count == 0)
            throw new InvalidOperationException("The approval chain has no steps configured.");

        foreach (var step in steps)
        {
            _db.RequisitionApprovals.Add(new RequisitionApproval
            {
                TenantId = requisition.TenantId,
                RequisitionId = requisition.Id,
                Sequence = step.Sequence,
                ApproverUserId = step.ApproverUserId,
                Label = step.Label,
                Decision = ApprovalDecision.Waiting,
            });
        }

        requisition.Status = RequisitionStatus.PendingApproval;
        requisition.SubmittedAt = _clock.GetUtcNow();
        requisition.UpdatedAt = _clock.GetUtcNow();

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(requisition.Id, ct);
    }

    public async Task<RequisitionDetailDto?> DecideAsync(Guid id, ApprovalDecisionRequest request, CancellationToken ct = default)
    {
        var requisition = await _db.Requisitions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (requisition is null) return null;

        var approvals = await _db.RequisitionApprovals
            .Where(a => a.RequisitionId == id)
            .OrderBy(a => a.Sequence)
            .ToListAsync(ct);

        // Identify the caller BEFORE reporting anything about the requisition's state.
        // The status guard below throws a 409 that names the status; running it first let
        // any authenticated user probe an arbitrary GUID and tell "doesn't exist" (404) from
        // "exists, and is Approved" (409) — the leak ADR-0003's 404-not-403 rule prevents.
        var current = approvals.FirstOrDefault(a => a.Decision == ApprovalDecision.Waiting);
        if (current is null) return null;
        if (_user.UserId is null || current.ApproverUserId != _user.UserId.Value) return null;

        if (requisition.Status != RequisitionStatus.PendingApproval)
            throw new InvalidOperationException($"This requisition is {requisition.Status}, not awaiting approval.");

        var now = _clock.GetUtcNow();
        current.Decision = request.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        current.DecidedAt = now;
        current.Comment = request.Comment;

        if (!request.Approve)
        {
            requisition.Status = RequisitionStatus.Rejected;
            requisition.DecidedAt = now;
        }
        else if (approvals.All(a => a.Decision == ApprovalDecision.Approved))
        {
            requisition.Status = RequisitionStatus.Approved;
            requisition.DecidedAt = now;
        }

        requisition.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<RequisitionDetailDto?> UpdateAsync(
        Guid id, UpdateRequisitionRequest request, CancellationToken ct = default)
    {
        var requisition = await _db.Requisitions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (requisition is null) return null;
        if (!await _access.CanAccessAsync(requisition.DepartmentId, ct)) return null;
        if (!IsOwnerOrCompanyWide(requisition)) return null;

        // Once submitted, the content is what approvers are deciding on. Editing it underneath
        // them would make every recorded decision refer to a document that no longer exists.
        if (requisition.Status != RequisitionStatus.Draft)
            throw new InvalidOperationException(
                $"Only a Draft can be edited; this one is {requisition.Status}. Cancel it and raise a new one.");

        if (request.DepartmentId != requisition.DepartmentId)
        {
            // Both ends must be reachable, or this becomes a way to move a requisition
            // somewhere the caller cannot see it (ADR-0003).
            if (!await _access.CanAccessAsync(request.DepartmentId, ct)) return null;
            if (!await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId && d.IsActive, ct)) return null;
            requisition.DepartmentId = request.DepartmentId;
        }

        requisition.Title = request.Title;
        requisition.JobDescription = request.JobDescription;
        requisition.Headcount = request.Headcount;
        requisition.SalaryBudget = request.SalaryBudget;
        requisition.UpdatedAt = _clock.GetUtcNow();

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<RequisitionDetailDto?> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var requisition = await _db.Requisitions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (requisition is null) return null;
        if (!await _access.CanAccessAsync(requisition.DepartmentId, ct)) return null;
        if (!IsOwnerOrCompanyWide(requisition)) return null;

        // Terminal states are final — reopening a decided requisition would rewrite history.
        if (requisition.Status is not (RequisitionStatus.Draft or RequisitionStatus.PendingApproval))
            throw new InvalidOperationException(
                $"Only a Draft or PendingApproval requisition can be cancelled; this one is {requisition.Status}.");

        var now = _clock.GetUtcNow();
        requisition.Status = RequisitionStatus.Cancelled;
        requisition.DecidedAt = now;
        requisition.UpdatedAt = now;

        // Approval steps are deliberately left as-is: "cancelled while waiting on Finance"
        // is a fact worth keeping. GetInboxAsync filters on requisition status instead.
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    /// <summary>Editing or withdrawing someone else's request is a different authority from
    /// raising your own — approvers are explicitly excluded, since being asked to approve
    /// something does not make it yours. Callers return 404 rather than 403 on false, so
    /// existence is not leaked (ADR-0003).</summary>
    private bool IsOwnerOrCompanyWide(Requisition requisition)
    {
        if (_user.UserId is not null && requisition.RequestedByUserId == _user.UserId.Value)
            return true;

        // nameof over string literals so renaming the enum breaks the build here too.
        // Roles lives in the Api layer, which Infrastructure must not reference, so the
        // Domain enum is the shared vocabulary.
        return _user.Role is nameof(UserRole.Admin) or nameof(UserRole.HrDirector);
    }

    /// <summary>Two-query approach that avoids EF Core LINQ translation pitfalls:
    /// <list type="number">
    /// <item>SQL JOIN for rows + department names (sorting done in SQL)</item>
    /// <item>Batch load of "Waiting" approval labels (no N+1)</item>
    /// <item>In-memory projection to DTO (safe for enum.ToString() etc.)</item>
    /// </list></summary>
    private async Task<IReadOnlyList<RequisitionListItemDto>> FetchListAsync(
        IQueryable<Requisition> query, CancellationToken ct)
    {
        var rows = await (
            from r in query
            join d in _db.Departments.AsNoTracking() on r.DepartmentId equals d.Id
            orderby r.SubmittedAt descending
            select new { r, DepartmentName = d.Name }
        ).ToListAsync(ct);

        if (rows.Count == 0) return Array.Empty<RequisitionListItemDto>();

        var ids = rows.Select(x => x.r.Id).ToList();
        var waitingRows = await _db.RequisitionApprovals
            .AsNoTracking()
            .Where(a => ids.Contains(a.RequisitionId) && a.Decision == ApprovalDecision.Waiting)
            .OrderBy(a => a.Sequence)
            .Select(a => new { a.RequisitionId, a.Label })
            .ToListAsync(ct);

        var awaiting = waitingRows
            .GroupBy(a => a.RequisitionId)
            .ToDictionary(g => g.Key, g => (string?)g.First().Label);

        return rows.Select(x => new RequisitionListItemDto(
            x.r.Id,
            x.r.DepartmentId,
            x.DepartmentName,
            x.r.Title,
            x.r.Headcount,
            x.r.SalaryBudget,
            x.r.Status.ToString(),
            x.r.SubmittedAt,
            awaiting.GetValueOrDefault(x.r.Id)
        )).ToList();
    }

    /// <summary>Builds a full <see cref="RequisitionDetailDto"/> for a single entity,
    /// including department name, approval steps, and awaiting-step label.
    /// Caller must verify access before calling this.</summary>
    private async Task<RequisitionDetailDto> BuildDetailAsync(Requisition r, CancellationToken ct)
    {
        var dept = await _db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == r.DepartmentId, ct);

        var approvalRows = await _db.RequisitionApprovals
            .AsNoTracking()
            .Where(a => a.RequisitionId == r.Id)
            .OrderBy(a => a.Sequence)
            .ToListAsync(ct);

        var steps = approvalRows.Select(a => new ApprovalStepDto(
            a.Sequence,
            a.Label,
            a.ApproverUserId,
            a.Decision.ToString(),
            a.DecidedAt,
            a.Comment
        )).ToList();

        var awaiting = approvalRows
            .FirstOrDefault(a => a.Decision == ApprovalDecision.Waiting)?.Label;

        return new RequisitionDetailDto(
            r.Id,
            r.DepartmentId,
            dept?.Name ?? string.Empty,
            r.RequestedByUserId,
            r.Title,
            r.JobDescription,
            r.Headcount,
            r.SalaryBudget,
            r.Status.ToString(),
            r.SubmittedAt,
            r.DecidedAt,
            awaiting,
            steps
        );
    }
}
