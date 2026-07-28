namespace RecruitOps.Application.DTOs;

/// <summary>One step in the approval chain snapshot attached to a requisition.
/// Returned as part of <see cref="RequisitionDetailDto"/> so the UI can render
/// a full approval timeline without a second round-trip.</summary>
public record ApprovalStepDto(
    int Sequence,
    string Label,
    Guid ApproverUserId,
    string Decision,           // ApprovalDecision enum name: "Waiting" | "Approved" | "Rejected"
    DateTimeOffset? DecidedAt,
    string? Comment);
