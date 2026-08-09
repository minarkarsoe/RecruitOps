namespace RecruitOps.Domain.Enums;

/// <summary>
/// Status of an overall bulk CV upload job batch.
/// </summary>
public enum BulkBatchStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>
/// Status of an individual file item within a bulk CV upload batch.
/// </summary>
public enum BulkFileStatus
{
    Queued = 0,
    Processing = 1,
    Success = 2,
    Skipped = 3,
    Failed = 4
}
