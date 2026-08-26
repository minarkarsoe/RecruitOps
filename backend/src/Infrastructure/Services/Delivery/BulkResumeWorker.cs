using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery;

/// <summary>Turns queued CVs into candidates and applications (Module 2.3 on ADR-0026's mechanism).
///
/// <para><b>The same discipline as <see cref="OutboundMessageWorker"/>, deliberately not the same
/// code.</b> Claiming, the visibility timeout, the attempt cap and the per-message tenant scope are
/// reproduced here rather than shared through a base class. Two queues did not seem like enough to
/// justify a generic claim loop over two entities with different status enums and different
/// terminal states — and the mail worker has been security-reviewed, so pulling it apart to serve
/// a second caller would put that behind a re-review. <b>A third queue is the point at which to
/// extract it</b>; until then this comment is the honest record of a duplication.</para>
///
/// <para><b>What this is not.</b> It is not <c>Task.Run</c> over a static dictionary. A restart
/// mid-batch loses nothing: the rows are Queued with a due time in the past, and the next pass
/// picks them up.</para>
/// </summary>
public sealed class BulkResumeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly BulkResumeOptions _options;
    private readonly ILogger<BulkResumeWorker> _logger;

    public BulkResumeWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        IOptions<BulkResumeOptions> options,
        ILogger<BulkResumeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A dead BackgroundService is silent, and silence is how fifty CVs disappear.
                _logger.LogError(ex, "Bulk resume pass failed. Continuing.");
            }

            try
            {
                await Task.Delay(_options.PollInterval, _clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One pass: claim what is due, process each, record what happened.
    /// <para>Public so tests drive it directly rather than sleeping and hoping.</para>
    /// <returns>How many files were handled.</returns></summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var claimed = await ClaimDueAsync(ct);

        foreach (var file in claimed)
        {
            await HandleClaimedAsync(file, ct);
        }

        return claimed.Count;
    }

    /// <summary>⚠️ <c>IgnoreQueryFilters()</c> is mandatory here and nowhere else in this file: the
    /// worker runs outside any request, so the tenant filter would match nothing.</summary>
    private async Task<List<BulkUploadFile>> ClaimDueAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = _clock.GetUtcNow();

        var due = await db.BulkUploadFiles
            .IgnoreQueryFilters()
            .Where(f => f.Status == BulkFileStatus.Queued && f.NextAttemptAt <= now)
            .OrderBy(f => f.NextAttemptAt)
            .ThenBy(f => f.Ordinal)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (due.Count == 0) return due;

        foreach (var file in due)
        {
            file.Attempts += 1;
            file.NextAttemptAt = now + _options.VisibilityTimeout;
        }

        await db.SaveChangesAsync(ct);
        return due;
    }

    /// <summary>Processes one claimed file and <b>guarantees the row is accounted for</b>.
    ///
    /// <para>The wrapper is the same one the 2026-08-20 security review forced onto the mail
    /// worker, and it is here from the start for the same reason: anything escaping between the
    /// claim and the record would skip the attempt cap, so the row would be reclaimed every
    /// visibility window forever — and it would abandon the rest of the batch with it.</para></summary>
    private async Task HandleClaimedAsync(BulkUploadFile claimed, CancellationToken ct)
    {
        try
        {
            await ProcessInTenantScopeAsync(claimed, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex,
                "Processing CV {FileId} for tenant {TenantId} failed outside the extractor.",
                claimed.Id, claimed.TenantId);

            await RecordOutsideTenantScopeAsync(claimed.Id, BulkFileOutcome.Retry(ex.Message), ct);
        }
    }

    private async Task ProcessInTenantScopeAsync(BulkUploadFile claimed, CancellationToken ct)
    {
        // A scope per file. IAmbientTenantScope refuses a second EnterTenant, so a reused scope
        // crashes rather than quietly running as the previous file's tenant.
        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAmbientTenantScope>().EnterTenant(claimed.TenantId);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Re-read inside the tenant scope, through the filter on purpose: if the tenant was not
        // established correctly the row is simply not found, and that is a logged miss rather
        // than a cross-tenant write.
        var file = await db.BulkUploadFiles.FirstOrDefaultAsync(f => f.Id == claimed.Id, ct);
        if (file is null)
        {
            // Throw rather than return — returning would leave the row un-recorded and therefore
            // un-capped. The wrapper above turns this into a counted attempt.
            throw new InvalidOperationException(
                $"Claimed CV {claimed.Id} was not readable inside tenant {claimed.TenantId}. "
                + "The tenant scope is not being established correctly.");
        }

        var outcome = await ExtractAndCreateAsync(scope.ServiceProvider, db, file, ct);
        Record(file, outcome);

        await db.SaveChangesAsync(ct);

        await CleanUpIfAbandonedAsync(scope.ServiceProvider, file, ct);
    }

    /// <summary>Everything that turns one stored CV into a candidate and an application.
    ///
    /// <para><b>Notice the absence.</b> No <c>IgnoreQueryFilters()</c>, and no hand-written
    /// <c>c.TenantId == batchState.TenantId</c> predicate — which is what the old implementation
    /// needed on its candidate lookup, and exactly the shape ADR-0003 warns about: a filter
    /// applied by hand is a filter that can be forgotten. The worker entered the tenant, so these
    /// are ordinary queries.</para></summary>
    private async Task<BulkFileOutcome> ExtractAndCreateAsync(
        IServiceProvider provider, AppDbContext db, BulkUploadFile file, CancellationToken ct)
    {
        var batch = await db.BulkUploadBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == file.BulkUploadBatchId, ct);

        if (batch is null)
        {
            return BulkFileOutcome.Failed("The upload this file belonged to no longer exists.");
        }

        if (string.IsNullOrWhiteSpace(file.StorageKey))
        {
            return BulkFileOutcome.Failed("This file was never stored, so there was nothing to read.");
        }

        var storage = provider.GetRequiredService<IFileStorage>();
        var extractor = provider.GetRequiredService<IDocumentTextExtractor>();
        var clock = provider.GetRequiredService<TimeProvider>();

        using var stored = await storage.DownloadAsync(file.StorageKey, cancellationToken: ct);
        if (stored is null)
        {
            // Terminal. The bytes are gone and no amount of waiting brings them back; retrying
            // would only delay telling the recruiter to upload it again.
            return BulkFileOutcome.Failed(
                "The uploaded file is no longer in storage, so it could not be processed. Upload it again.");
        }

        // Buffered because the extractor may read the stream more than once, and because the
        // storage stream is a live HTTP response we would rather close early than hold open
        // through a slow parse.
        using var buffer = new MemoryStream();
        await stored.Content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var extraction = await extractor.ExtractTextAsync(buffer, file.FileName, file.ContentType, ct);

        var parsed = extraction.ParsedContactInfo;
        var email = ContactNormalizer.Email(parsed?.Email);
        var phone = ContactNormalizer.Phone(parsed?.Phone);

        var candidate = await FindOrCreateCandidateAsync(db, email, phone, parsed?.CandidateName, file, ct);

        var now = clock.GetUtcNow();

        var application = new JobApplication
        {
            TenantId = file.TenantId,
            JobPostingId = batch.JobPostingId,
            CandidateId = candidate.Id,
            Status = PipelineStatus.Sourced,
            Source = SourceChannel.Direct,
            AppliedAt = now,
            // The bytes are already in storage under this key — referenced, never re-uploaded.
            // A second copy would double storage for every CV and give two keys to keep in step.
            ResumeFileKey = file.StorageKey,
            ResumeFileName = file.FileName,
            ResumeExtractedText = extraction.ExtractedText,
            ResumeUploadedAt = now,
            IsZawgyiNormalized = extraction.IsZawgyiNormalized,
        };
        db.JobApplications.Add(application);

        // Module 5 reads this history to compute time-to-hire; an application that appears with
        // no arrival row is a gap nobody can reconstruct afterwards.
        db.ApplicationStageHistories.Add(new ApplicationStageHistory
        {
            TenantId = file.TenantId,
            JobApplicationId = application.Id,
            FromStatus = null,
            ToStatus = PipelineStatus.Sourced,
            // The recruiter who uploaded the batch, recorded at upload time — NOT whoever happens
            // to be around when the file is finally processed, and not null. ADR-0026 §4: a job
            // must attribute what it writes to an explicit actor.
            ChangedByUserId = batch.UploadedByUserId,
            ChangedAt = now,
            Note = "Created via Bulk CV Upload",
        });

        return BulkFileOutcome.Success(candidate.Id, application.Id, now);
    }

    /// <summary>Module 2.7 deduplication: one person who applies twice is one candidate.
    /// <para>A lookup, never a constraint — two people can share a household phone number, so a
    /// unique index here would reject real applicants (see <see cref="Candidate"/>).</para></summary>
    private static async Task<Candidate> FindOrCreateCandidateAsync(
        AppDbContext db, string? email, string? phone, string? parsedName, BulkUploadFile file, CancellationToken ct)
    {
        Candidate? candidate = null;

        if (email is not null || phone is not null)
        {
            candidate = await db.Candidates
                .Where(c => c.MergedIntoCandidateId == null
                            && ((email != null && c.Email == email) || (phone != null && c.Phone == phone)))
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        if (candidate is null)
        {
            var name = parsedName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                // Better than blank: the recruiter recognises their own file names, and a nameless
                // row in the pipeline is one nobody can act on.
                name = Path.GetFileNameWithoutExtension(file.FileName);
            }

            candidate = new Candidate
            {
                TenantId = file.TenantId,
                FullName = name,
                Email = email,
                Phone = phone,
                Source = SourceChannel.Direct,
            };
            db.Candidates.Add(candidate);
            return candidate;
        }

        // Fill gaps only. A second CV must not overwrite what the first one established — the
        // newer file is not automatically the more correct one.
        candidate.Email ??= email;
        candidate.Phone ??= phone;
        if (string.IsNullOrWhiteSpace(candidate.FullName) && !string.IsNullOrWhiteSpace(parsedName))
        {
            candidate.FullName = parsedName.Trim();
        }

        return candidate;
    }

    // ---------------------------------------------------------------- recording

    /// <summary>Records an outcome for a file whose own tenant scope could not be used. The second
    /// and last <c>IgnoreQueryFilters()</c> in this worker, and it touches only queue bookkeeping —
    /// never candidate data.</summary>
    private async Task RecordOutsideTenantScopeAsync(Guid fileId, BulkFileOutcome outcome, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var file = await db.BulkUploadFiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == fileId, ct);

            if (file is null)
            {
                _logger.LogError("CV {FileId} vanished before its outcome could be recorded.", fileId);
                return;
            }

            Record(file, outcome);
            await db.SaveChangesAsync(ct);

            // ⚠️ Also cleaned up here, not only on the in-scope path. This is the route a file
            // that simply cannot be parsed actually takes — the extractor throws — so cleaning up
            // only in ProcessInTenantScopeAsync would mean the common terminal failure leaves its
            // bytes behind every time. Found by a test that asserted the opposite.
            await CleanUpIfAbandonedAsync(scope.ServiceProvider, file, ct);
        }
        catch (Exception ex)
        {
            // Last resort. The row stays Queued and will be retried, which is the safe direction —
            // but it must be loud, because it means the queue can spin.
            _logger.LogError(ex, "Could not record an outcome for CV {FileId}.", fileId);
        }
    }

    private void Record(BulkUploadFile file, BulkFileOutcome outcome)
    {
        var now = _clock.GetUtcNow();

        switch (outcome.Kind)
        {
            case BulkFileOutcomeKind.Success:
                file.Status = BulkFileStatus.Success;
                file.CandidateId = outcome.CandidateId;
                file.JobApplicationId = outcome.JobApplicationId;
                file.CompletedAt = outcome.CompletedAt ?? now;
                file.LastError = null;
                break;

            case BulkFileOutcomeKind.Failed:
                file.Status = BulkFileStatus.Failed;
                file.LastError = outcome.Error;
                file.CompletedAt = now;
                break;

            case BulkFileOutcomeKind.Retry:
                file.LastError = outcome.Error;
                if (file.Attempts >= _options.MaxAttempts)
                {
                    file.Status = BulkFileStatus.Failed;
                    file.CompletedAt = now;
                    file.LastError =
                        $"Gave up after {file.Attempts} attempts. Last error: {outcome.Error}";
                }
                else
                {
                    file.Status = BulkFileStatus.Queued;
                    file.NextAttemptAt = now + BackoffFor(file.Attempts);
                }
                break;
        }
    }

    /// <summary>Removes the stored bytes of a file that will never become an application.
    ///
    /// <para>Best effort, and deliberately not allowed to change the row's outcome: a CV the
    /// recruiter has already been told about must not flip back to Queued because a delete
    /// failed. It matters because these are candidates' CVs — leaving one behind for every failed
    /// file quietly builds a store of personal data with no record pointing at it, which is
    /// precisely what the Module 7.4 retention policy exists to prevent.</para></summary>
    private async Task CleanUpIfAbandonedAsync(
        IServiceProvider provider, BulkUploadFile file, CancellationToken ct)
    {
        if (file.Status != BulkFileStatus.Failed || string.IsNullOrWhiteSpace(file.StorageKey))
        {
            return;
        }

        try
        {
            var storage = provider.GetRequiredService<IFileStorage>();
            await storage.DeleteAsync(file.StorageKey, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not remove the stored bytes of failed CV {FileId} at {StorageKey}.",
                file.Id, file.StorageKey);
        }
    }

    /// <summary>Exponential, capped. Attempt 1 waits the base delay, then it doubles.</summary>
    private TimeSpan BackoffFor(int attempts)
    {
        var exponent = Math.Max(0, attempts - 1);
        var factor = Math.Pow(2, Math.Min(exponent, 16));
        var delay = _options.BaseBackoff * factor;
        return delay > _options.MaxBackoff ? _options.MaxBackoff : delay;
    }
}

/// <summary>What happened to one CV.
/// <para>Three outcomes, not four: there is no "Suppressed" here. A CV is never correctly left
/// unprocessed — the reasons a file is not turned into a candidate are all failures.</para></summary>
internal readonly record struct BulkFileOutcome
{
    private BulkFileOutcome(
        BulkFileOutcomeKind kind, string? error, Guid? candidateId, Guid? applicationId, DateTimeOffset? completedAt)
    {
        Kind = kind;
        Error = error;
        CandidateId = candidateId;
        JobApplicationId = applicationId;
        CompletedAt = completedAt;
    }

    public BulkFileOutcomeKind Kind { get; }
    public string? Error { get; }
    public Guid? CandidateId { get; }
    public Guid? JobApplicationId { get; }
    public DateTimeOffset? CompletedAt { get; }

    public static BulkFileOutcome Success(Guid candidateId, Guid applicationId, DateTimeOffset at) =>
        new(BulkFileOutcomeKind.Success, null, candidateId, applicationId, at);

    /// <summary>Might work next time — a storage blip, a transient lock. Backed off and capped.</summary>
    public static BulkFileOutcome Retry(string error) =>
        new(BulkFileOutcomeKind.Retry, error, null, null, null);

    /// <summary>Will fail identically next time — the bytes are gone, the batch is gone. Terminal,
    /// because retrying is just a slower way to tell the recruiter to upload it again.</summary>
    public static BulkFileOutcome Failed(string error) =>
        new(BulkFileOutcomeKind.Failed, error, null, null, null);
}

internal enum BulkFileOutcomeKind
{
    Success,
    Retry,
    Failed,
}
