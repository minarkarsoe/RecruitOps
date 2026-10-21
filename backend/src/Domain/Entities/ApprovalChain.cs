using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A company-configurable approval route (Module 1.3).
/// Reused by budget/headcount plans in Module 6 — build once.</summary>
public class ApprovalChain : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Null = the company-wide default chain; otherwise specific to a department.</summary>
    public Guid? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>What the chain approves. A requisition and an offer of the same department find
    /// their chains separately (ADR-0030 §1).</summary>
    public ApprovalChainPurpose Purpose { get; set; } = ApprovalChainPurpose.Requisition;
}
