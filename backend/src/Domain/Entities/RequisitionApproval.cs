using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One approval step against a specific requisition, generated from the
/// company's <see cref="ApprovalChain"/> when the requisition is submitted (Module 1.3).
/// Kept separate from the chain so editing the chain never rewrites decisions
/// already made — the audit trail must stay truthful.</summary>
public class RequisitionApproval : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RequisitionId { get; set; }

    /// <summary>1-based position; steps are decided in order.</summary>
    public int Sequence { get; set; }

    public Guid ApproverUserId { get; set; }
    public string Label { get; set; } = string.Empty;

    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Waiting;
    public DateTimeOffset? DecidedAt { get; set; }
    public string? Comment { get; set; }
}
