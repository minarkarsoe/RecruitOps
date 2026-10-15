using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One uploaded CV waiting to become a candidate and an application (ADR-0026).
///
/// <para><b>The bytes are not here.</b> They go to object storage at upload time (ADR-0013) and
/// this row keeps only <see cref="StorageKey"/>. That is the second half of what ADR-0026 replaced:
/// fifty CVs of several MB each used to sit in process memory for the whole batch, per concurrent
/// upload, on a server sized by a guide that did not account for it.</para>
///
/// <para><b>Claimed exactly like an OutboundMessage</b>, and for the same reasons: a due row
/// (<see cref="BulkFileStatus.Queued"/> with <see cref="NextAttemptAt"/> in the past) is claimed by
/// pushing <see cref="NextAttemptAt"/> forward by a visibility timeout and incrementing
/// <see cref="Attempts"/>. There is no "in flight" state, so a process that dies mid-extraction
/// loses nothing — the row is still Queued and becomes due again by itself. <see cref="Attempts"/>
/// is what stops a file that can never be parsed from circulating forever.</para>
/// </summary>
public class BulkUploadFile : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid BulkUploadBatchId { get; set; }

    /// <summary>Position in the upload, so the status list comes back in the order the recruiter
    /// selected the files. Without it the list is ordered by whatever the database felt like, and
    /// "the third one failed" stops meaning anything.</summary>
    public int Ordinal { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>Where the uploaded bytes actually live. Empty for a file rejected on the way in —
    /// an oversized or unsupported file is never stored, because storing several megabytes in order
    /// to then refuse them is work nobody asked for.
    ///
    /// <para>⚠️ On success this same key becomes <c>JobApplication.ResumeFileKey</c>. The file is
    /// uploaded once and referenced, not copied: a second upload would double the storage for
    /// every CV in the system and give two keys that can fall out of step.</para></summary>
    public string StorageKey { get; set; } = string.Empty;

    public BulkFileStatus Status { get; set; } = BulkFileStatus.Queued;

    /// <summary>How many times the worker has claimed this row.</summary>
    public int Attempts { get; set; }

    /// <summary>When this row next becomes claimable — pushed forward on each claim, and used as
    /// the backoff timer.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Why it did not work, written for the recruiter looking at the upload panel rather
    /// than for a log file.</summary>
    public string? LastError { get; set; }

    /// <summary>Filled on success. Both are what the recruiter clicks through to.</summary>
    public Guid? CandidateId { get; set; }

    public Guid? JobApplicationId { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
