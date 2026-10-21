namespace RecruitOps.Domain.Enums;

/// <summary>What an <c>ApprovalChain</c> approves (ADR-0030 §1). Every chain that existed before
/// offers is a requisition chain.</summary>
public enum ApprovalChainPurpose
{
    Requisition,
    Offer,
}
