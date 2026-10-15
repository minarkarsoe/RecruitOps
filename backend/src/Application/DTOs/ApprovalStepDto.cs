namespace RecruitOps.Application.DTOs;

/// <summary>One step in the approval chain snapshot attached to a requisition.
/// Returned as part of <see cref="RequisitionDetailDto"/> so the UI can render
/// a full approval timeline without a second round-trip.</summary>
public record ApprovalStepDto(
    int Round,                 // 1-based submission attempt (ADR-0023); steps repeat per round
    int Sequence,
    string Label,
    Guid ApproverUserId,       // who the step was ASSIGNED to
    string Decision,           // ApprovalDecision enum name: "Waiting" | "Approved" | "Rejected"
    DateTimeOffset? DecidedAt,
    string? Comment,
    Guid? DecidedByUserId);    // who actually decided, when a senior closed it (ADR-0024); null = the assignee
