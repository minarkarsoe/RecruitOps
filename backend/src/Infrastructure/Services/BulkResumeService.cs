using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 2.3 — takes a recruiter's fifty CVs and puts them on a durable queue.
///
/// <para><b>Rewritten onto ADR-0026, not extended.</b> What this replaced kept the batch — and the
/// raw uploaded bytes — in a <c>static ConcurrentDictionary</c> and started the work with
/// <c>_ = Task.Run(...)</c>. A restart did not degrade that; it erased it. The recruiter's fifty
/// files returned 404 with no way to learn whether any candidate had been created, an exception
/// inside the <c>Task.Run</c> was unobserved, and the files sat in RAM for the whole batch. The
/// ADR's own Context section is largely a description of this class.</para>
///
/// <para>What it does now: validate, put the bytes in object storage (ADR-0013), and write a
/// <see cref="BulkUploadBatch"/> plus one <see cref="BulkUploadFile"/> per file in a single
/// <c>SaveChangesAsync</c>. <see cref="BulkResumeWorker"/> does the rest, later, durably.</para>
/// </summary>
public class BulkResumeService : IBulkResumeService
{
    /// <summary>What <c>IDocumentTextExtractor</c> can actually read (ADR-0008 Phase 1).
    ///
    /// <para>Deliberately <b>not</b> configurable, unlike the size limit next to it. An operator
    /// who could add <c>.rtf</c> here would be enabling a format the extractor cannot parse, and
    /// every such file would fail three times before being given up on. The list is a fact about
    /// the code, not a deployment preference.</para></summary>
    private static readonly string[] AllowedExtensions = [".pdf", ".docx", ".png", ".jpg", ".jpeg"];

    private readonly AppDbContext _db;
    private readonly IDepartmentAccess _access;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;
    private readonly TimeProvider _clock;
    private readonly BulkResumeOptions _options;
    private readonly ILogger<BulkResumeService> _logger;

    public BulkResumeService(
        AppDbContext db,
        IDepartmentAccess access,
        ICurrentUser currentUser,
        IFileStorage storage,
        TimeProvider clock,
        IOptions<BulkResumeOptions> options,
        ILogger<BulkResumeService> logger)
    {
        _db = db;
        _access = access;
        _currentUser = currentUser;
        _storage = storage;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BulkUploadBatchResponseDto?> EnqueueBatchAsync(
        Guid jobPostingId,
        IReadOnlyList<BulkFileItemInput> files,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == jobPostingId, ct);

        if (posting is null)
        {
            _logger.LogWarning("Job posting {JobPostingId} not found.", jobPostingId);
            return null;
        }

        // ADR-0003 + ADR-0018, applied explicitly, on the way in. This is the ONLY place they
        // can be applied for this feature: the worker runs with no user, so it cannot ask "may
        // they reach it" later (ADR-0026 §4).
        if (!await CanReachCandidatesInAsync(posting.DepartmentId, ct))
        {
            _logger.LogWarning(
                "Department access denied for posting {JobPostingId}, department {DepartmentId}.",
                jobPostingId, posting.DepartmentId);
            return null;
        }

        var now = _clock.GetUtcNow();

        var batch = new BulkUploadBatch
        {
            TenantId = posting.TenantId,
            JobPostingId = jobPostingId,
            UploadedByUserId = currentUserId ?? _currentUser.UserId,
            CreatedAt = now,
        };

        var rows = new List<BulkUploadFile>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            rows.Add(await PrepareAsync(batch, files[i], i, now, ct));
        }

        _db.BulkUploadBatches.Add(batch);
        _db.BulkUploadFiles.AddRange(rows);
        await _db.SaveChangesAsync(ct);

        return new BulkUploadBatchResponseDto(
            BatchId: batch.Id,
            JobPostingId: jobPostingId,
            TotalFiles: files.Count,
            Status: BulkBatchStatus.Queued.ToString(),
            CreatedAt: now);
    }

    public async Task<BulkBatchStatusDto?> GetBatchStatusAsync(
        Guid jobPostingId,
        Guid batchId,
        CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == jobPostingId, ct);

        if (posting is null) return null;
        if (!await CanReachCandidatesInAsync(posting.DepartmentId, ct)) return null;

        var batch = await _db.BulkUploadBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId && b.JobPostingId == jobPostingId, ct);

        // Null covers "no such batch", "not this posting's batch" and "another tenant's batch"
        // alike — 404 for all three, so existence is never leaked.
        if (batch is null) return null;

        var rows = await _db.BulkUploadFiles.AsNoTracking()
            .Where(f => f.BulkUploadBatchId == batchId)
            .OrderBy(f => f.Ordinal)
            .ToListAsync(ct);

        return Summarise(batch, rows);
    }

    /// <summary>Department scoping (ADR-0003) <b>plus</b> the candidate-data exclusion
    /// (ADR-0018), through one door so neither entry point can get only half of it.
    ///
    /// <para>⚠️ <b>This class shipped with only half.</b> Both entry points called
    /// <c>CanAccessAsync</c> on its own, which answers "does this role work across departments" —
    /// and an Approver does, on the requisition axis, which is what ADR-0003 was arguing about.
    /// Asked about a <i>candidate</i>, that same <c>true</c> let an Approver POST fifty CVs into
    /// any posting in the company and read the batch back. Confirmed against the running API on
    /// 2026-08-26: <c>POST /api/jobpostings/{id}/resumes/bulk</c> as the Finance approver, against
    /// a Sales posting, returned <b>200 OK</b> with a batch id.</para>
    ///
    /// <para>The bug is the one ADR-0018 was written about, reintroduced in newer code with the
    /// corrected version sitting in <c>PipelineService.CanReachCandidatesInAsync</c> — which was
    /// the argument for making this a shared helper rather than a rule each service remembers.
    /// That argument won: the rule now lives on <see cref="IDepartmentAccess"/> and this is a
    /// one-line forward.</para>
    /// </summary>
    private Task<bool> CanReachCandidatesInAsync(Guid departmentId, CancellationToken ct)
        => _access.CanReachCandidatesInAsync(departmentId, ct);

    // ---------------------------------------------------------------- enqueue helpers

    /// <summary>Turns one uploaded file into a row — storing its bytes first, unless it is
    /// rejected outright.
    ///
    /// <para><b>Validation happens here, not in the worker.</b> Uploading several megabytes into
    /// object storage in order to refuse them a minute later is work nobody asked for, and the
    /// recruiter finds out about an unsupported file immediately instead of after a poll.</para></summary>
    private async Task<BulkUploadFile> PrepareAsync(
        BulkUploadBatch batch, BulkFileItemInput file, int ordinal, DateTimeOffset now, CancellationToken ct)
    {
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? GuessContentType(file.FileName)
            : file.ContentType;

        var row = new BulkUploadFile
        {
            TenantId = batch.TenantId,
            BulkUploadBatchId = batch.Id,
            Ordinal = ordinal,
            FileName = Truncate(file.FileName, 255),
            ContentType = Truncate(contentType, 255),
            SizeBytes = file.Content.LongLength,
            Status = BulkFileStatus.Queued,
            NextAttemptAt = now,
            CreatedAt = now,
        };

        var rejection = Reject(file);
        if (rejection is not null)
        {
            row.Status = BulkFileStatus.Failed;
            row.LastError = rejection;
            row.CompletedAt = now;
            return row;
        }

        try
        {
            using var content = new MemoryStream(file.Content, writable: false);

            // ⚠️ The key carries NO part of the uploaded file name — only ids and a validated
            // extension. A candidate-supplied name in an object key is how a stray "../" or a
            // control character becomes somebody else's problem in whichever storage backend a
            // customer runs. The name the recruiter sees lives in FileName, on the row.
            var key = $"bulk-uploads/{batch.Id}/{row.Id}{Path.GetExtension(row.FileName).ToLowerInvariant()}";

            var uploaded = await _storage.UploadAsync(
                new UploadFileRequest(key, content, row.ContentType, file.Content.LongLength), ct);

            row.StorageKey = uploaded.Key;
        }
        catch (Exception ex)
        {
            // Terminal, not retryable, and the distinction is forced: a retry would need the
            // bytes, and the bytes only existed inside this request. Failing the one row keeps
            // the rest of the batch alive, which is the behaviour a recruiter needs when storage
            // hiccups on file 7 of 50.
            _logger.LogError(ex, "Could not store {FileName} for batch {BatchId}.", row.FileName, batch.Id);

            row.Status = BulkFileStatus.Failed;
            row.LastError = "This file could not be stored, so it was not processed. Upload it again.";
            row.CompletedAt = now;
        }

        return row;
    }

    /// <summary>The reason to refuse this file, or null to accept it.</summary>
    private string? Reject(BulkFileItemInput file)
    {
        if (file.Content.LongLength == 0)
        {
            return "The file is empty.";
        }

        if (file.Content.LongLength > _options.MaxFileSizeBytes)
        {
            // Wording kept: "exceeds maximum limit" is asserted by the suite and, more to the
            // point, is what the upload panel already shows.
            var megabytes = _options.MaxFileSizeBytes / (1024d * 1024d);
            return $"File size exceeds maximum limit of {megabytes:0.#}MB.";
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return "Unsupported file extension. Allowed formats: .pdf, .docx, .png, .jpg, .jpeg.";
        }

        return null;
    }

    // ---------------------------------------------------------------- status

    /// <summary>Computes the batch's state from its files. Nothing here is stored, so nothing here
    /// can disagree with the rows it describes.</summary>
    private static BulkBatchStatusDto Summarise(BulkUploadBatch batch, List<BulkUploadFile> rows)
    {
        var processed = rows.Count(IsTerminal);
        var success = rows.Count(f => f.Status == BulkFileStatus.Success);
        var skipped = rows.Count(f => f.Status == BulkFileStatus.Skipped);
        var failed = rows.Count(f => f.Status == BulkFileStatus.Failed);

        var status = processed switch
        {
            0 => BulkBatchStatus.Queued,
            _ when processed < rows.Count => BulkBatchStatus.Processing,
            // Everything is terminal. "Completed" means at least one CV actually became a
            // candidate; a batch where every single file failed is not a completed batch, and
            // telling a recruiter it is would be the least useful thing this endpoint could say.
            _ when success > 0 || skipped > 0 => BulkBatchStatus.Completed,
            _ => BulkBatchStatus.Failed,
        };

        DateTimeOffset? completedAt = processed == rows.Count && rows.Count > 0
            ? rows.Max(f => f.CompletedAt)
            : null;

        return new BulkBatchStatusDto(
            BatchId: batch.Id,
            JobPostingId: batch.JobPostingId,
            Status: status.ToString(),
            TotalFiles: rows.Count,
            ProcessedFiles: processed,
            SuccessCount: success,
            SkippedCount: skipped,
            FailedCount: failed,
            CreatedAt: batch.CreatedAt,
            CompletedAt: completedAt,
            Items: rows.Select(f => new BulkFileItemStatusDto(
                FileName: f.FileName,
                Status: f.Status.ToString(),
                ErrorMessage: f.LastError,
                ApplicationId: f.JobApplicationId,
                CandidateId: f.CandidateId)).ToList());
    }

    private static bool IsTerminal(BulkUploadFile file) =>
        file.Status is BulkFileStatus.Success or BulkFileStatus.Failed or BulkFileStatus.Skipped;

    // ---------------------------------------------------------------- small helpers

    internal static string GuessContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream",
        };

    /// <summary>The column is capped, and a browser will happily send a 4 KB file name. Truncating
    /// beats a <c>DbUpdateException</c> that loses the other forty-nine files with it.</summary>
    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
