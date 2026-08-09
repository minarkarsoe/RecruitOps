using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

internal class BatchStateHolder
{
    public Guid BatchId { get; set; }
    public Guid JobPostingId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public BulkBatchStatus Status { get; set; } = BulkBatchStatus.Queued;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<BatchItemStateHolder> Items { get; set; } = new();
    public object LockObject { get; } = new();
}

internal class BatchItemStateHolder
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public BulkFileStatus Status { get; set; } = BulkFileStatus.Queued;
    public string? ErrorMessage { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid? CandidateId { get; set; }
}

public class BulkResumeService : IBulkResumeService, Application.Common.Interfaces.IBulkResumeService
{
    private static readonly ConcurrentDictionary<Guid, BatchStateHolder> Batches = new();

    private readonly AppDbContext _db;
    private readonly IDepartmentAccess _access;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkResumeService> _logger;

    public BulkResumeService(
        AppDbContext db,
        IDepartmentAccess access,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<BulkResumeService> logger)
    {
        _db = db;
        _access = access;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<BulkUploadBatchResponseDto?> EnqueueBatchAsync(
        Guid jobPostingId,
        IReadOnlyList<BulkFileItemInput> files,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.AsNoTracking().FirstOrDefaultAsync(p => p.Id == jobPostingId, ct);
        if (posting is null)
        {
            _logger.LogWarning("Job posting {JobPostingId} not found.", jobPostingId);
            return null;
        }

        if (!await _access.CanAccessAsync(posting.DepartmentId, ct))
        {
            _logger.LogWarning("Department access denied for posting {JobPostingId}, department {DepartmentId}.", jobPostingId, posting.DepartmentId);
            return null;
        }

        var effectiveUserId = currentUserId ?? _currentUser.UserId;
        var batchId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow();

        var batchState = new BatchStateHolder
        {
            BatchId = batchId,
            JobPostingId = jobPostingId,
            TenantId = posting.TenantId,
            UploadedByUserId = effectiveUserId,
            Status = BulkBatchStatus.Queued,
            TotalFiles = files.Count,
            ProcessedFiles = 0,
            SuccessCount = 0,
            SkippedCount = 0,
            FailedCount = 0,
            CreatedAt = now,
            Items = files.Select(f => new BatchItemStateHolder
            {
                FileName = f.FileName,
                Content = f.Content,
                ContentType = f.ContentType,
                Status = BulkFileStatus.Queued
            }).ToList()
        };

        Batches[batchId] = batchState;

        // Launch background task processing asynchronously (non-blocking)
        _ = Task.Run(async () => await ProcessBatchAsync(batchId));

        return new BulkUploadBatchResponseDto(
            BatchId: batchId,
            JobPostingId: jobPostingId,
            TotalFiles: files.Count,
            Status: BulkBatchStatus.Queued.ToString(),
            CreatedAt: now
        );
    }

    public async Task<BulkBatchStatusDto?> GetBatchStatusAsync(
        Guid jobPostingId,
        Guid batchId,
        CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.AsNoTracking().FirstOrDefaultAsync(p => p.Id == jobPostingId, ct);
        if (posting is null)
        {
            return null;
        }

        if (!await _access.CanAccessAsync(posting.DepartmentId, ct))
        {
            return null;
        }

        if (!Batches.TryGetValue(batchId, out var batchState) || batchState.JobPostingId != jobPostingId)
        {
            return null;
        }

        lock (batchState.LockObject)
        {
            return new BulkBatchStatusDto(
                BatchId: batchState.BatchId,
                JobPostingId: batchState.JobPostingId,
                Status: batchState.Status.ToString(),
                TotalFiles: batchState.TotalFiles,
                ProcessedFiles: batchState.ProcessedFiles,
                SuccessCount: batchState.SuccessCount,
                SkippedCount: batchState.SkippedCount,
                FailedCount: batchState.FailedCount,
                CreatedAt: batchState.CreatedAt,
                CompletedAt: batchState.CompletedAt,
                Items: batchState.Items.Select(i => new BulkFileItemStatusDto(
                    FileName: i.FileName,
                    Status: i.Status.ToString(),
                    ErrorMessage: i.ErrorMessage,
                    ApplicationId: i.ApplicationId,
                    CandidateId: i.CandidateId
                )).ToList()
            );
        }
    }

    private async Task ProcessBatchAsync(Guid batchId)
    {
        if (!Batches.TryGetValue(batchId, out var batchState))
        {
            return;
        }

        lock (batchState.LockObject)
        {
            batchState.Status = BulkBatchStatus.Processing;
        }

        var allowedExtensions = new[] { ".pdf", ".docx", ".png", ".jpg", ".jpeg" };

        foreach (var item in batchState.Items)
        {
            lock (batchState.LockObject)
            {
                item.Status = BulkFileStatus.Processing;
            }

            // 1. File Size Validation (Max 10MB)
            if (item.Content.Length == 0 || item.Content.Length > 10 * 1024 * 1024)
            {
                lock (batchState.LockObject)
                {
                    item.Status = BulkFileStatus.Failed;
                    item.ErrorMessage = "File size exceeds maximum limit of 10MB.";
                    batchState.FailedCount++;
                    batchState.ProcessedFiles++;
                }
                continue;
            }

            // 2. Extension Validation
            var ext = Path.GetExtension(item.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                lock (batchState.LockObject)
                {
                    item.Status = BulkFileStatus.Failed;
                    item.ErrorMessage = "Unsupported file extension. Allowed formats: .pdf, .docx, .png, .jpg, .jpeg.";
                    batchState.FailedCount++;
                    batchState.ProcessedFiles++;
                }
                continue;
            }

            // 3. Process valid file item inside a fresh DI scope
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
                var extractor = scope.ServiceProvider.GetRequiredService<IDocumentTextExtractor>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                using var ms = new MemoryStream(item.Content);

                // Extract text & auto-normalize Zawgyi
                var extractionResult = await extractor.ExtractTextAsync(
                    ms,
                    item.FileName,
                    string.IsNullOrWhiteSpace(item.ContentType) ? GetContentType(item.FileName) : item.ContentType
                );

                var parsed = extractionResult.ParsedContactInfo;
                string? email = ContactNormalizer.Email(parsed?.Email);
                string? phone = ContactNormalizer.Phone(parsed?.Phone);

                // Deduplicate candidate via Email or Phone
                Candidate? candidate = null;
                if (email != null || phone != null)
                {
                    candidate = await db.Candidates
                        .IgnoreQueryFilters()
                        .Where(c => c.TenantId == batchState.TenantId
                                    && c.MergedIntoCandidateId == null
                                    && ((email != null && c.Email == email) || (phone != null && c.Phone == phone)))
                        .OrderBy(c => c.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                if (candidate == null)
                {
                    string candidateName = parsed?.CandidateName?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(candidateName))
                    {
                        candidateName = Path.GetFileNameWithoutExtension(item.FileName);
                    }

                    candidate = new Candidate
                    {
                        TenantId = batchState.TenantId,
                        FullName = candidateName,
                        Email = email,
                        Phone = phone,
                        Source = SourceChannel.Direct
                    };
                    db.Candidates.Add(candidate);
                    await db.SaveChangesAsync();
                }
                else
                {
                    if (candidate.Email == null && email != null) candidate.Email = email;
                    if (candidate.Phone == null && phone != null) candidate.Phone = phone;
                    if (string.IsNullOrWhiteSpace(candidate.FullName) && !string.IsNullOrWhiteSpace(parsed?.CandidateName))
                    {
                        candidate.FullName = parsed.CandidateName.Trim();
                    }
                    await db.SaveChangesAsync();
                }

                var now = clock.GetUtcNow();

                // Create JobApplication
                var application = new JobApplication
                {
                    TenantId = batchState.TenantId,
                    JobPostingId = batchState.JobPostingId,
                    CandidateId = candidate.Id,
                    Status = PipelineStatus.Sourced,
                    Source = SourceChannel.Direct,
                    AppliedAt = now,
                };
                db.JobApplications.Add(application);
                await db.SaveChangesAsync();

                // Upload to Object Storage
                ms.Position = 0;
                string fileKey = $"applications/{application.Id}/resume/{Guid.NewGuid()}_{item.FileName}";
                string contentType = string.IsNullOrWhiteSpace(item.ContentType) ? GetContentType(item.FileName) : item.ContentType;

                var uploadReq = new UploadFileRequest(
                    Key: fileKey,
                    Content: ms,
                    ContentType: contentType,
                    ContentLength: item.Content.Length
                );
                var uploadResp = await storage.UploadAsync(uploadReq);

                // Update application resume properties
                application.ResumeFileKey = uploadResp.Key;
                application.ResumeFileName = item.FileName;
                application.ResumeExtractedText = extractionResult.ExtractedText;
                application.ResumeUploadedAt = now;
                application.IsZawgyiNormalized = extractionResult.IsZawgyiNormalized;

                // Log ApplicationStageHistory
                var history = new ApplicationStageHistory
                {
                    TenantId = batchState.TenantId,
                    JobApplicationId = application.Id,
                    FromStatus = null,
                    ToStatus = PipelineStatus.Sourced,
                    ChangedByUserId = batchState.UploadedByUserId,
                    ChangedAt = now,
                    Note = "Created via Bulk CV Upload"
                };
                db.ApplicationStageHistories.Add(history);
                await db.SaveChangesAsync();

                lock (batchState.LockObject)
                {
                    item.Status = BulkFileStatus.Success;
                    item.ApplicationId = application.Id;
                    item.CandidateId = candidate.Id;
                    batchState.SuccessCount++;
                    batchState.ProcessedFiles++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk resume file {FileName} in batch {BatchId}", item.FileName, batchId);
                lock (batchState.LockObject)
                {
                    item.Status = BulkFileStatus.Failed;
                    item.ErrorMessage = ex.Message;
                    batchState.FailedCount++;
                    batchState.ProcessedFiles++;
                }
            }
        }

        lock (batchState.LockObject)
        {
            batchState.Status = BulkBatchStatus.Completed;
            batchState.CompletedAt = _timeProvider.GetUtcNow();
        }
    }

    private static string GetContentType(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}
