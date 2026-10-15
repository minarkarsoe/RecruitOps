namespace RecruitOps.Infrastructure.Services.Delivery;

/// <summary>Tuning for bulk CV processing (Module 2.3 on ADR-0026's mechanism).
///
/// <para><b>The same shape as <see cref="OutboundDeliveryOptions"/>, with different numbers</b>,
/// because the two queues have opposite characters: sending mail is a fast network call that
/// mostly succeeds, while extracting text from a scanned PDF is slow, local and CPU-bound
/// (ADR-0008 keeps Phase 1 extraction entirely offline). One shared options class would force one
/// set of numbers onto both and the smaller batch would win.</para>
/// </summary>
public sealed class BulkResumeOptions
{
    public const string SectionName = "BulkResume";

    /// <summary>How long to wait between polls. Shorter than the mail queue's: a recruiter is
    /// watching the upload panel, and this is the number that decides how long the first file sits
    /// at "Queued" while they look at it.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How many files one pass claims.
    ///
    /// <para>Small on purpose, and for a different reason than the mail queue's batch size: a pass
    /// holds each claimed file's bytes only one at a time, but its total duration must stay well
    /// inside <see cref="VisibilityTimeout"/>. Five slow PDFs is a realistic pass; fifty would not
    /// be.</para></summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>How long a claimed file stays invisible before it is treated as abandoned. Generous
    /// because OCR on a large scanned document is genuinely slow, and a timeout that fires early
    /// means the same CV is parsed twice and the candidate is created twice.</summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Attempts before a file is given up on and marked Failed. Lower than the mail
    /// queue's: a CV that fails to parse fails for a reason that does not heal — a corrupt upload,
    /// a format the extractor cannot read — whereas a mail server genuinely does come back.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>First retry delay; doubles per attempt, capped by <see cref="MaxBackoff"/>.</summary>
    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Largest single CV accepted, in bytes. 10 MB by default.
    ///
    /// <para>Configurable because it is a genuine deployment question — a customer whose CVs
    /// arrive as phone photographs of printed documents needs a bigger number, and a customer on a
    /// small on-premise box needs a smaller one. Enforced at upload, before anything is stored.</para></summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Largest batch accepted in one request.
    /// <para>Not a security limit — it is a limit on how much a single request may make the API do
    /// at once. The per-IP rate limiter is what stops abuse.</para></summary>
    public int MaxFilesPerBatch { get; set; } = 50;
}
