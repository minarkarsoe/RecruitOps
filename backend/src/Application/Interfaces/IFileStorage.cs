using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IFileStorage
{
    /// <summary>
    /// Uploads a stream/file to object storage.
    /// </summary>
    Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an object stream and metadata by storage key. Returns null if object does not exist.
    /// </summary>
    Task<StorageObject?> DownloadAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object by key. Returns true if successful or if object was already absent.
    /// </summary>
    Task<bool> DeleteAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a presigned temporary URL for direct upload, download, or delete.
    /// </summary>
    Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an object exists in storage by key.
    /// </summary>
    Task<bool> ExistsAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves object metadata without downloading full stream payload.
    /// </summary>
    Task<FileMetadata?> GetMetadataAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default);
}
