using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>One recruiter's "here are fifty CVs" against one job posting (Module 2.3, ADR-0026).
///
/// <para><b>This row replaces a static dictionary, and that is the whole point.</b> The previous
/// implementation kept the batch — <i>including the raw uploaded bytes</i> — in a
/// <c>static ConcurrentDictionary</c> and never wrote it down. A restart did not make the status
/// go stale; it erased the entry, so the status endpoint returned 404 and the recruiter had no way
/// to find out whether any of their fifty candidates had been created. See ADR-0026's Context.</para>
///
/// <para><b>Deliberately almost empty.</b> Status and every count are derived from
/// <see cref="BulkUploadFile"/> rows rather than stored here. The old code maintained
/// <c>ProcessedFiles</c>, <c>SuccessCount</c>, <c>SkippedCount</c> and <c>FailedCount</c> by hand
/// under a lock, which is a second source of truth that can only ever drift from the first. A
/// count that is computed cannot disagree with the rows it counts.</para>
/// </summary>
public class BulkUploadBatch : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The posting every CV in this batch is filed against. Department access is checked
    /// through it, on the way in and on every status read.</summary>
    public Guid JobPostingId { get; set; }

    /// <summary>Who uploaded it, for the stage-history attribution on each application created.
    /// <para>Null is possible and is not a job running as nobody: the worker attributes the
    /// created rows to <i>this recorded user</i>, never to whoever happens to be around when the
    /// file is finally processed (ADR-0026 §4).</para></summary>
    public Guid? UploadedByUserId { get; set; }
}
