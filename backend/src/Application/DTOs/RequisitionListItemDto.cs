namespace RecruitOps.Application.DTOs;

public record RequisitionListItemDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    string Title,
    int Headcount,
    decimal? SalaryBudget,
    string Status,                 // RequisitionStatus name
    DateTimeOffset? SubmittedAt,
    string? AwaitingApprovalFrom,  // label of the step currently waiting, if any
    // The caller's OWN waiting step in the current round, if they have one. Distinct from
    // AwaitingApprovalFrom since ADR-0024: a senior may hold a later step while the chain
    // still waits on a junior, so the two differ exactly when it is not yet the caller's
    // turn but they may approve ahead anyway. Null when the caller has no step here.
    string? YourStepLabel);
