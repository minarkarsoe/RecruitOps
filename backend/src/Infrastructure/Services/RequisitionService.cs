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

        // Step 2 used to keep only the ones where it was the caller's TURN. Under ADR-0024 a
        // later approver outranks an earlier one and may close their step, so "not yet your
        // turn" no longer means "not yours to see" — hiding these would make skip-ahead
        // undiscoverable. The DTO carries AwaitingApprovalFrom so the UI can mark them.
        //
        // What still has to be filtered is the ROUND. A rejected round leaves its later steps
        // Waiting forever — that is the honest record (ADR-0023) — and those dead rows must
        // never put a stale requisition back into anyone's inbox. So every row is loaded, not
        // just the Waiting ones, because the current round can only be identified from all of them.
        var allSteps = await _db.RequisitionApprovals
            .AsNoTracking()
            .Where(a => candidateIds.Contains(a.RequisitionId))
            .Select(a => new { a.RequisitionId, a.ApproverUserId, a.Sequence, a.Round, a.Decision })
            .ToListAsync(ct);

        var myIds = allSteps
            .GroupBy(a => a.RequisitionId)
            .Where(g =>
            {
                var currentRound = g.Max(a => a.Round);
                return g.Any(a => a.Round == currentRound
                                  && a.Decision == ApprovalDecision.Waiting
                                  && a.ApproverUserId == userId.Value);
            })
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

        // A resubmission after a rejection opens the NEXT round and leaves the previous one
        // exactly as it was (ADR-0023). The chain is re-resolved above on every submit, so a
        // chain edited between rounds applies to the new round only — the old round keeps the
        // steps that were actually decided.
        var previousRound = await _db.RequisitionApprovals
            .Where(a => a.RequisitionId == requisition.Id)
            .Select(a => (int?)a.Round)
            .MaxAsync(ct) ?? 0;

        var round = previousRound + 1;

        foreach (var step in steps)
        {
            _db.RequisitionApprovals.Add(new RequisitionApproval
            {
                TenantId = requisition.TenantId,
                RequisitionId = requisition.Id,
                Round = round,
                Sequence = step.Sequence,
                ApproverUserId = step.ApproverUserId,
                Label = step.Label,
                Decision = ApprovalDecision.Waiting,
            });
        }

        requisition.Status = RequisitionStatus.PendingApproval;
        requisition.SubmittedAt = _clock.GetUtcNow();
        requisition.UpdatedAt = _clock.GetUtcNow();

        // Cleared, not just left: on a resubmission this still holds the moment the PREVIOUS
        // round was rejected, and a PendingApproval requisition showing a decision date is a
        // false statement on every screen that renders it. The round's own DecidedAt on the
        // approval rows is where that history lives now.
        requisition.DecidedAt = null;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(requisition.Id, ct);
    }

    public async Task<RequisitionDetailDto?> DecideAsync(Guid id, ApprovalDecisionRequest request, CancellationToken ct = default)
    {
        var requisition = await _db.Requisitions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (requisition is null) return null;

        var allRows = await _db.RequisitionApprovals
            .Where(a => a.RequisitionId == id)
            .OrderBy(a => a.Sequence)
            .ToListAsync(ct);

        if (allRows.Count == 0) return null;

        // Only the current round is live. Earlier rounds are history and must never be
        // decided on, or re-approved (ADR-0023) — their Waiting rows are the honest record
        // of a chain that was cut short, not work anybody still owes.
        var currentRound = allRows.Max(a => a.Round);
        var approvals = allRows.Where(a => a.Round == currentRound).ToList();

        // Identify the caller BEFORE reporting anything about the requisition's state.
        // The status guard below throws a 409 that names the status; running it first let
        // any authenticated user probe an arbitrary GUID and tell "doesn't exist" (404) from
        // "exists, and is Approved" (409) — the leak ADR-0003's 404-not-403 rule prevents.
        // Skip-ahead must not reopen that: an unauthorised caller still gets a bare 404.
        if (_user.UserId is null) return null;

        // The caller's OWN waiting step, not merely the lowest one. Under ADR-0024 a later
        // step outranks an earlier one, so being further down the chain is now sufficient
        // authority to act — but only from a step the caller was actually named on.
        var mine = approvals.FirstOrDefault(
            a => a.Decision == ApprovalDecision.Waiting && a.ApproverUserId == _user.UserId.Value);
        if (mine is null) return null;

        // Whose turn it genuinely is: the lowest undecided step in this round.
        var lowestWaiting = approvals
            .Where(a => a.Decision == ApprovalDecision.Waiting)
            .MinBy(a => a.Sequence)!;

        if (requisition.Status != RequisitionStatus.PendingApproval)
            throw new InvalidOperationException($"This requisition is {requisition.Status}, not awaiting approval.");

        // Rejecting forward is refused, and it is not the mirror image of approving forward
        // (ADR-0024). Approving forward removes a junior's step but not their say — the
        // requisition proceeds, which their approval would have caused anyway. Rejecting
        // forward would END it before the junior ever saw it, substituting the senior's
        // opinion for a review that never happened. A senior may still reject their own step.
        if (!request.Approve && mine.Sequence != lowestWaiting.Sequence)
            throw new InvalidOperationException(
                $"This requisition is waiting on {lowestWaiting.Label}. You can approve on their behalf, " +
                "but only they can reject at their step.");

        var now = _clock.GetUtcNow();

        if (!request.Approve)
        {
            mine.Decision = ApprovalDecision.Rejected;
            mine.DecidedAt = now;
            mine.Comment = request.Comment;

            requisition.Status = RequisitionStatus.Rejected;
            requisition.DecidedAt = now;
        }
        else
        {
            // Everything at or below the caller's own position closes in one action. When it
            // was not their step, DecidedByUserId records who really pressed the button —
            // the chain is a record of what happened, not of what the template expected.
            var closing = approvals.Where(a =>
                a.Decision == ApprovalDecision.Waiting && a.Sequence <= mine.Sequence);

            foreach (var step in closing)
            {
                step.Decision = ApprovalDecision.Approved;
                step.DecidedAt = now;
                step.Comment = request.Comment;
                if (step.ApproverUserId != _user.UserId.Value)
                    step.DecidedByUserId = _user.UserId.Value;
            }

            // Scoped to this round. Across all rounds this could never be true once a
            // rejected round is preserved, and a fully-approved requisition would silently
            // sit in PendingApproval with no Waiting step — invisible in every inbox.
            if (approvals.All(a => a.Decision == ApprovalDecision.Approved))
            {
                requisition.Status = RequisitionStatus.Approved;
                requisition.DecidedAt = now;
            }
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

    public async Task<RequisitionDetailDto?> ReviseAsync(Guid id, CancellationToken ct = default)
    {
        var requisition = await _db.Requisitions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (requisition is null) return null;
        if (!await _access.CanAccessAsync(requisition.DepartmentId, ct)) return null;

        // Same ownership rule as edit, submit and cancel. An approver's tool is Reject, which
        // records a decision; being asked to approve something is not authority to rewrite it
        // and put it back in the queue (ADR-0023, mirroring the module doc's cancellation rule).
        if (!IsOwnerOrCompanyWide(requisition)) return null;

        // Only Rejected reopens. Approved and Cancelled stay terminal — approved work must not
        // be reopened silently, and a withdrawn request stays withdrawn.
        if (requisition.Status != RequisitionStatus.Rejected)
            throw new InvalidOperationException(
                $"Only a Rejected requisition can be revised; this one is {requisition.Status}.");

        var now = _clock.GetUtcNow();
        requisition.Status = RequisitionStatus.Draft;
        requisition.UpdatedAt = now;

        // SubmittedAt and DecidedAt are deliberately left alone here: while this sits in
        // Draft they still describe the round that was actually decided, which is true and
        // useful. SubmitAsync overwrites the first and clears the second when round n+1
        // opens. The approval rows are untouched for the same reason — the rejection and its
        // comment are the whole point of revising, and the next submit opens round n+1
        // beside them rather than over them.
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
        // Every row, not just the Waiting ones: the current round can only be identified by
        // comparing rounds, and a rejected round's leftover Waiting steps would otherwise
        // label the requisition with a step nobody is waiting on (ADR-0023).
        var approvalRows = await _db.RequisitionApprovals
            .AsNoTracking()
            .Where(a => ids.Contains(a.RequisitionId))
            .Select(a => new { a.RequisitionId, a.Label, a.Sequence, a.Round, a.Decision, a.ApproverUserId })
            .ToListAsync(ct);

        var callerId = _user.UserId;

        var labels = approvalRows
            .GroupBy(a => a.RequisitionId)
            .ToDictionary(g => g.Key, g =>
            {
                var currentRound = g.Max(a => a.Round);
                var waiting = g
                    .Where(a => a.Round == currentRound && a.Decision == ApprovalDecision.Waiting)
                    .ToList();

                // MinBy rather than First(): First() would silently depend on a query
                // ordering surviving every future edit to the statement above.
                var awaitingFrom = waiting.MinBy(a => a.Sequence)?.Label;
                var yours = callerId is null
                    ? null
                    : waiting.FirstOrDefault(a => a.ApproverUserId == callerId.Value)?.Label;

                return (AwaitingFrom: awaitingFrom, Yours: yours);
            });

        return rows.Select(x => new RequisitionListItemDto(
            x.r.Id,
            x.r.DepartmentId,
            x.DepartmentName,
            x.r.Title,
            x.r.Headcount,
            x.r.SalaryBudget,
            x.r.Status.ToString(),
            x.r.SubmittedAt,
            labels.GetValueOrDefault(x.r.Id).AwaitingFrom,
            labels.GetValueOrDefault(x.r.Id).Yours
        )).ToList();
    }

    /// <summary>Builds a full <see cref="RequisitionDetailDto"/> for a single entity,
    /// including department name, approval steps, and awaiting-step label.
    /// Caller must verify access before calling this.</summary>
    private async Task<RequisitionDetailDto> BuildDetailAsync(Requisition r, CancellationToken ct)
    {
        var dept = await _db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == r.DepartmentId, ct);

        // All rounds are returned, ordered oldest first, so an earlier rejection stays
        // readable above the attempt it produced (ADR-0023). Ordering by round first matters:
        // sequence alone would interleave the rounds into one nonsensical chain.
        var approvalRows = await _db.RequisitionApprovals
            .AsNoTracking()
            .Where(a => a.RequisitionId == r.Id)
            .OrderBy(a => a.Round).ThenBy(a => a.Sequence)
            .ToListAsync(ct);

        var steps = approvalRows.Select(a => new ApprovalStepDto(
            a.Round,
            a.Sequence,
            a.Label,
            a.ApproverUserId,
            a.Decision.ToString(),
            a.DecidedAt,
            a.Comment,
            a.DecidedByUserId
        )).ToList();

        // Scoped to the current round: a superseded round's leftover Waiting steps describe a
        // chain that was cut short, not somebody the requisition is waiting on.
        var currentRound = approvalRows.Count == 0 ? 0 : approvalRows.Max(a => a.Round);
        var awaiting = approvalRows
            .Where(a => a.Round == currentRound && a.Decision == ApprovalDecision.Waiting)
            .MinBy(a => a.Sequence)?.Label;

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
