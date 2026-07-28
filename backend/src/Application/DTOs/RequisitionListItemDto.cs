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
    string? AwaitingApprovalFrom); // label of the step currently waiting, if any
