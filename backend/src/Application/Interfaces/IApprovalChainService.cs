using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 1.3 — configurable approval routes. Company configuration, so
/// administration is Admin-only; the chain is consumed by RequisitionService on submit.</summary>
public interface IApprovalChainService
{
    Task<IReadOnlyList<ApprovalChainDto>> GetChainsAsync(CancellationToken ct = default);

    Task<ApprovalChainDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Null if the referenced department or any approver does not exist.</summary>
    Task<ApprovalChainDto?> CreateAsync(CreateApprovalChainRequest request, CancellationToken ct = default);
}
