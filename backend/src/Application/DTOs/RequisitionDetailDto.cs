namespace RecruitOps.Application.DTOs;

/// <summary>Full detail for the requisition detail page.
/// Supersedes <see cref="RequisitionListItemDto"/> for the single-item endpoint — it carries
/// the job description and the full approval timeline so the page has no second round-trip.</summary>
public record RequisitionDetailDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    /// <summary>Who raised it — the UI uses this to decide whether to offer "Cancel".
    /// The backend re-checks it; this is a display hint, not the authority.</summary>
    Guid RequestedByUserId,
    string Title,
    string JobDescription,
    int Headcount,
    decimal? SalaryBudget,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? DecidedAt,
    string? AwaitingApprovalFrom,
    IReadOnlyList<ApprovalStepDto> Approvals);
