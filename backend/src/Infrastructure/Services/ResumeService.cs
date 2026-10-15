using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class ResumeService : IResumeService
{
    private readonly AppDbContext _db;
    private readonly IApplicationAccess _access;
    private readonly IFileStorage _storage;
    private readonly IDocumentTextExtractor _extractor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResumeService> _logger;

    public ResumeService(
        AppDbContext db,
        IApplicationAccess access,
        IFileStorage storage,
        IDocumentTextExtractor extractor,
        TimeProvider timeProvider,
        ILogger<ResumeService> logger)
    {
        _db = db;
        _access = access;
        _storage = storage;
        _extractor = extractor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ResumeExtractionResultDto?> UploadAndExtractResumeAsync(
        Guid applicationId,
        IFormFile file,
        CancellationToken ct = default)
    {
        var reach = await _access.ResolveAsync(applicationId, ct);
        if (reach is null)
        {
            _logger.LogWarning("Access denied or application not found for ApplicationId {ApplicationId}", applicationId);
            return null;
        }

        var application = await _db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application is null)
        {
            return null;
        }

        using var memoryStream = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await file.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        // Perform text extraction, script normalization, and contact info parsing
        var extractionResult = await _extractor.ExtractTextAsync(
            memoryStream,
            file.FileName,
            file.ContentType,
            ct);

        // Upload original stream to Object Storage
        memoryStream.Position = 0;
        string fileKey = $"applications/{applicationId}/resume/{Guid.NewGuid()}_{file.FileName}";
        var uploadRequest = new UploadFileRequest(
            Key: fileKey,
            Content: memoryStream,
            ContentType: string.IsNullOrWhiteSpace(file.ContentType) ? GetContentType(file.FileName) : file.ContentType,
            ContentLength: file.Length
        );

        var uploadResponse = await _storage.UploadAsync(uploadRequest, ct);

        // Update JobApplication entity metadata
        var now = _timeProvider.GetUtcNow();
        application.ResumeFileKey = uploadResponse.Key;
        application.ResumeFileName = file.FileName;
        application.ResumeExtractedText = extractionResult.ExtractedText;
        application.ResumeUploadedAt = now;
        application.IsZawgyiNormalized = extractionResult.IsZawgyiNormalized;

        await _db.SaveChangesAsync(ct);

        return new ResumeExtractionResultDto(
            ApplicationId: applicationId,
            FileKey: uploadResponse.Key,
            FileName: file.FileName,
            FileSizeBytes: file.Length,
            ExtractedText: extractionResult.ExtractedText,
            OriginalText: extractionResult.OriginalText,
            DetectedLanguage: extractionResult.DetectedLanguage,
            IsZawgyiNormalized: extractionResult.IsZawgyiNormalized,
            ParsedContactInfo: extractionResult.ParsedContactInfo,
            ProcessedAt: now
        );
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> GetResumeFileAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var reach = await _access.ResolveAsync(applicationId, ct);
        if (reach is null)
        {
            return null;
        }

        var application = await _db.JobApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (application is null || string.IsNullOrEmpty(application.ResumeFileKey))
        {
            return null;
        }

        var storageObject = await _storage.DownloadAsync(application.ResumeFileKey, null, ct);
        if (storageObject is null)
        {
            return null;
        }

        string fileName = application.ResumeFileName ?? "resume";
        string contentType = !string.IsNullOrWhiteSpace(storageObject.ContentType)
            ? storageObject.ContentType
            : GetContentType(fileName);

        return (storageObject.Content, contentType, fileName);
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
