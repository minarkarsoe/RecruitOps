using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 2.1 — turning an approved requisition into a published vacancy.
/// <para>Like <see cref="IRequisitionService"/>, every method here applies department
/// scoping itself (ADR-0003) — it is not automatic.</para></summary>
public interface IJobPostingService
{
    Task<IReadOnlyList<JobPostingListItemDto>> GetPostingsAsync(CancellationToken ct = default);

    Task<JobPostingDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates a Draft posting from an <b>Approved</b> requisition, copying its
    /// title and description. Returns null if the requisition doesn't exist or isn't the
    /// caller's; throws InvalidOperationException if it isn't approved, or already has a
    /// posting.</summary>
    Task<JobPostingDetailDto?> CreateFromRequisitionAsync(
        CreateJobPostingRequest request, CancellationToken ct = default);

    /// <summary>Edits the advert. Allowed while Live — fixing a typo in a published advert
    /// must not require taking it down and losing the shared link.</summary>
    Task<JobPostingDetailDto?> UpdateAsync(
        Guid id, UpdateJobPostingRequest request, CancellationToken ct = default);

    /// <summary>Draft → Live, minting the public link token on first publish.</summary>
    Task<JobPostingDetailDto?> PublishAsync(Guid id, CancellationToken ct = default);

    /// <summary>Live → Closed. Existing applications are untouched; only new ones stop.</summary>
    Task<JobPostingDetailDto?> CloseAsync(Guid id, CancellationToken ct = default);
}
