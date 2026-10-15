using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IBulkResumeService
{
    /// <summary>
    /// Enqueues a batch of CV files for background processing against a target job posting.
    /// Returns null if job posting does not exist or user lacks department access.
    /// </summary>
    Task<BulkUploadBatchResponseDto?> EnqueueBatchAsync(
        Guid jobPostingId,
        IReadOnlyList<BulkFileItemInput> files,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current status and per-file progress of a bulk CV upload batch.
    /// Returns null if batch is not found or department access is denied for the job posting.
    /// </summary>
    Task<BulkBatchStatusDto?> GetBatchStatusAsync(
        Guid jobPostingId,
        Guid batchId,
        CancellationToken ct = default);
}
