namespace RecruitOps.Domain.Enums;

/// <summary>Outcome of a single step in an approval chain (Modules 1 and 6).</summary>
public enum ApprovalDecision
{
    Waiting,
    Approved,
    Rejected,
}
