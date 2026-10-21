using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One step of one round of an offer's internal approval — a snapshot of the offer chain
/// taken at submission, decided by the same rules as a requisition's (<see cref="ApprovalRail"/>,
/// ADR-0030 §1).</summary>
public class OfferApproval : BaseEntity, ITenantScoped, IApprovalStep
{
    public Guid TenantId { get; set; }
    public Guid OfferId { get; set; }
    public int Round { get; set; } = 1;
    public int Sequence { get; set; }
    public Guid ApproverUserId { get; set; }
    public string Label { get; set; } = string.Empty;
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Waiting;
    public DateTimeOffset? DecidedAt { get; set; }
    public string? Comment { get; set; }
    public Guid? DecidedByUserId { get; set; }
}
