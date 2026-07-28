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

    /// <summary>Requisitions where the current user is the next-in-line approver
    /// (i.e. the lowest-sequence Waiting step belongs to them). Used for the approval inbox.</summary>
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

    /// <summary>Records the current user's decision on the step awaiting them.
    /// Approving the last step approves the requisition; any rejection rejects it.</summary>
    Task<RequisitionDetailDto?> DecideAsync(Guid id, ApprovalDecisionRequest request, CancellationToken ct = default);

    /// <summary>Withdraws a Draft or PendingApproval requisition. Permitted to the requester
    /// or a company-wide role (Admin/HrDirector); returns null for anyone else, same as
    /// "not found". Throws InvalidOperationException on an already-decided requisition.</summary>
    Task<RequisitionDetailDto?> CancelAsync(Guid id, CancellationToken ct = default);
}
