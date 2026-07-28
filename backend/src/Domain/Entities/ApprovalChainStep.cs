using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>One ordered step in an <see cref="ApprovalChain"/>. Steps run sequentially:
/// step 1 must approve before step 2 is asked.</summary>
public class ApprovalChainStep : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ApprovalChainId { get; set; }

    /// <summary>1-based position in the chain.</summary>
    public int Sequence { get; set; }

    /// <summary>The user who approves at this step.</summary>
    public Guid ApproverUserId { get; set; }

    /// <summary>Free-text label shown in the UI, e.g. "Finance".</summary>
    public string Label { get; set; } = string.Empty;
}
