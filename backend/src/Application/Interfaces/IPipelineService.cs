using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 2.5 — the talent pipeline for one posting.
/// Department scoping (ADR-0003) is applied here, not automatically.</summary>
public interface IPipelineService
{
    Task<IReadOnlyList<PipelineItemDto>?> GetForPostingAsync(Guid jobPostingId, CancellationToken ct = default);

    /// <summary>Moves an application to another stage and appends a history row.
    /// Returns null if not found or not the caller's; throws InvalidOperationException for
    /// an unknown status name or a move that isn't allowed.</summary>
    Task<PipelineItemDto?> MoveStageAsync(Guid applicationId, MoveStageRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<StageHistoryItemDto>?> GetHistoryAsync(Guid applicationId, CancellationToken ct = default);
}
