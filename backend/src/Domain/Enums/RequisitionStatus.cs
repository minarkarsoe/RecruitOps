namespace RecruitOps.Domain.Enums;

/// <summary>Lifecycle of a headcount request (Module 1).</summary>
public enum RequisitionStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Cancelled,
}
