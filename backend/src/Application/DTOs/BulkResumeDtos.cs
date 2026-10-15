namespace RecruitOps.Application.DTOs;

/// <summary>
/// Response returned immediately after enqueueing a bulk CV upload batch.
/// </summary>
public record BulkUploadBatchResponseDto(
    Guid BatchId,
    Guid JobPostingId,
    int TotalFiles,
    string Status,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Status summary for an individual file item in a bulk batch.
/// </summary>
public record BulkFileItemStatusDto(
    string FileName,
    string Status,
    string? ErrorMessage,
    Guid? ApplicationId,
    Guid? CandidateId
);

/// <summary>
/// Detailed status report for a bulk CV upload batch and all its items.
/// </summary>
public record BulkBatchStatusDto(
    Guid BatchId,
    Guid JobPostingId,
    string Status,
    int TotalFiles,
    int ProcessedFiles,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<BulkFileItemStatusDto> Items
);

/// <summary>
/// Input model representing a file item passed into the bulk resume processing service.
/// </summary>
public record BulkFileItemInput(
    string FileName,
    byte[] Content,
    string ContentType
);
