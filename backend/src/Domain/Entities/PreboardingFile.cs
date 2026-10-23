using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>A file a candidate uploaded for a <see cref="PreboardingDocument"/> (ADR-0030 §5b). The object
/// is in the private bucket under <see cref="StorageKey"/>, which holds no name the candidate chose.
/// A replaced or removed file's object is deleted and its row kept with <see cref="RemovedAt"/> — the
/// record of what was there.</summary>
public class PreboardingFile : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid PreboardingDocumentId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    /// <summary>The name on the candidate's device, on one line — for display only.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Decided from the file's first bytes, never from what the browser said.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; set; }

    public DateTimeOffset? RemovedAt { get; set; }
}
