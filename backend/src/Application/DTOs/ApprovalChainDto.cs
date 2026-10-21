namespace RecruitOps.Application.DTOs;

public record ApprovalChainStepDto(int Sequence, Guid ApproverUserId, string Label);

public record ApprovalChainDto(
    Guid Id,
    string Name,
    Guid? DepartmentId,      // null = company-wide default
    bool IsActive,
    string Purpose,          // "Requisition" | "Offer" (ADR-0030 §1)
    IReadOnlyList<ApprovalChainStepDto> Steps);
