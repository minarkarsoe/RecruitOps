using Microsoft.AspNetCore.Http;
using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IResumeService
{
    /// <summary>
    /// Validates, uploads, extracts text, and stores a candidate resume for an application.
    /// Returns null if the application is not found or department access is denied.
    /// </summary>
    Task<ResumeExtractionResultDto?> UploadAndExtractResumeAsync(
        Guid applicationId,
        IFormFile file,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves stored resume file metadata and stream for streaming download.
    /// Returns null if not found or access is denied.
    /// </summary>
    Task<(Stream Stream, string ContentType, string FileName)?> GetResumeFileAsync(
        Guid applicationId,
        CancellationToken ct = default);
}
