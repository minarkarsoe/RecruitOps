using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Common;

/// <summary>One step of one round of an approval — a requisition's (Module 1.3) or an offer's
/// (Module 4.2). Both are snapshots of an <c>ApprovalChain</c> taken at submission, and both are
/// decided by the same rules (<see cref="ApprovalRail"/>), so the two approvals cannot drift apart
/// (ADR-0030 §1).</summary>
public interface IApprovalStep
{
    /// <summary>Which submission attempt, 1-based (ADR-0023). Only the highest round is live.</summary>
    int Round { get; }

    /// <summary>Position in the chain, 1-based. Seniority is position and nothing else (ADR-0024).</summary>
    int Sequence { get; }

    Guid ApproverUserId { get; }

    string Label { get; }

    ApprovalDecision Decision { get; set; }

    DateTimeOffset? DecidedAt { get; set; }

    string? Comment { get; set; }

    /// <summary>Set when someone other than <see cref="ApproverUserId"/> closed this step — a later
    /// approver approving forward (ADR-0024). The rail records who acted, not who was expected.</summary>
    Guid? DecidedByUserId { get; set; }
}
