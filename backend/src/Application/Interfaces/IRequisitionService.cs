using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 1 — Job Requisition &amp; Approval.
/// <para>Every method here is responsible for applying department scoping
/// (ADR-0003) — it is NOT automatic.</para></summary>
public interface IRequisitionService
{
    /// <summary>Requisitions visible to the current user. Hiring Managers see only
    /// their own departments; Admin/HrDirector/Recruiter see all.</summary>
    Task<IReadOnlyList<RequisitionListItemDto>> GetRequisitionsAsync(CancellationToken ct = default);

    /// <summary>Requisitions with a Waiting step in the current round assigned to the current
    /// user. Since ADR-0024 this includes steps it is not yet their turn to decide — a later
    /// approver may close an earlier step — so <c>AwaitingApprovalFrom</c> on each row is what
    /// tells the caller whose turn it actually is. Used for the approval inbox.</summary>
    Task<IReadOnlyList<RequisitionListItemDto>> GetInboxAsync(CancellationToken ct = default);

    /// <summary>Full detail including job description and approval timeline.
    /// Returns null when it does not exist OR the user may not see it — the caller
    /// returns 404 either way, so existence is not leaked (ADR-0003).</summary>
    Task<RequisitionDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates a Draft. Returns null if the user may not use that department.</summary>
    Task<RequisitionDetailDto?> CreateAsync(CreateRequisitionRequest request, CancellationToken ct = default);

    /// <summary>Edits a Draft. Permitted to the requester or a company-wide role
    /// (Admin/HrDirector); returns null for anyone else, same as "not found". Throws
    /// InvalidOperationException once the requisition has been submitted — approvers must
    /// not be deciding on a document that can change underneath them.</summary>
    Task<RequisitionDetailDto?> UpdateAsync(Guid id, UpdateRequisitionRequest request, CancellationToken ct = default);

    /// <summary>Draft → PendingApproval, generating approval steps from the department's
    /// chain (falling back to the company-wide default). Returns null if not found or
    /// not permitted; throws InvalidOperationException if it is not a Draft or no chain exists.</summary>
    Task<RequisitionDetailDto?> SubmitAsync(Guid id, CancellationToken ct = default);

    /// <summary>Records the current user's decision on their own Waiting step in the current
    /// round. Approving closes every Waiting step at or below theirs — a later step outranks
    /// an earlier one (ADR-0024) — and stamps <c>DecidedByUserId</c> on the ones that were not
    /// theirs. Approving the last outstanding step approves the requisition; any rejection
    /// rejects it. Rejecting is permitted only at the lowest Waiting step: a senior may not
    /// reject on a junior's behalf. Throws InvalidOperationException on a reject-forward
    /// attempt, and returns null (404) when the caller holds no Waiting step here.</summary>
    Task<RequisitionDetailDto?> DecideAsync(Guid id, ApprovalDecisionRequest request, CancellationToken ct = default);

    /// <summary>Rejected → Draft so the requester can correct and resubmit (ADR-0023); the
    /// next submit opens a new round beside the rejected one rather than over it. Permitted to
    /// the requester or a company-wide role (Admin/HrDirector); returns null for anyone else,
    /// same as "not found". Throws InvalidOperationException on any status but Rejected —
    /// Approved and Cancelled stay terminal.</summary>
    Task<RequisitionDetailDto?> ReviseAsync(Guid id, CancellationToken ct = default);

    /// <summary>Withdraws a Draft or PendingApproval requisition. Permitted to the requester
    /// or a company-wide role (Admin/HrDirector); returns null for anyone else, same as
    /// "not found". Throws InvalidOperationException on an already-decided requisition.</summary>
    Task<RequisitionDetailDto?> CancelAsync(Guid id, CancellationToken ct = default);
}
