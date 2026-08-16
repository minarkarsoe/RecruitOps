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

    /// <summary>Which submission attempt this step belongs to, 1-based (ADR-0023). A rejected
    /// requisition can be revised and resubmitted, which stamps out a fresh set of steps at the
    /// next round and leaves the previous round untouched — so every query against these rows
    /// must be scoped to the current round, or a dead round's steps answer for the live one.</summary>
    public int Round { get; set; } = 1;

    /// <summary>1-based position within the round; steps are decided in order.</summary>
    public int Sequence { get; set; }

    public Guid ApproverUserId { get; set; }
    public string Label { get; set; } = string.Empty;

    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Waiting;
    public DateTimeOffset? DecidedAt { get; set; }
    public string? Comment { get; set; }

    /// <summary>Who actually decided, when that is not <see cref="ApproverUserId"/> — a senior
    /// approver closing a junior's step under ADR-0024. Null means the assigned approver decided
    /// it themselves, which keeps every pre-existing row correct without a backfill.
    /// Deliberately additive: overwriting ApproverUserId would make the row claim the senior was
    /// always the assignee, which is false and unfalsifiable afterwards.</summary>
    public Guid? DecidedByUserId { get; set; }
}
