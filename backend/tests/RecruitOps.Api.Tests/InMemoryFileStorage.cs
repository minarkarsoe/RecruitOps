using System.Collections.Concurrent;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Tests;

/// <summary>
/// Thread-safe in-memory double for IFileStorage to ensure tests run offline without S3/MinIO dependencies.
/// </summary>
public class InMemoryFileStorage : IFileStorage
{
    private class StoredItem
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
        public Dictionary<string, string>? Metadata { get; set; }
    }

    private readonly ConcurrentDictionary<string, StoredItem> _storage = new(StringComparer.Ordinal);

    public async Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }
        await request.Content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        var item = new StoredItem
        {
            Data = bytes,
            ContentType = request.ContentType,
            Metadata = request.Metadata != null ? new Dictionary<string, string>(request.Metadata) : null
        };

        _storage[request.Key] = item;

        string bucket = request.BucketName ?? "test-bucket";
        return new UploadFileResponse(
            request.Key,
            bucket,
            "test-etag",
            bytes.Length,
            $"http://localhost/{bucket}/{request.Key}"
        );
    }

    public Task<StorageObject?> DownloadAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        if (_storage.TryGetValue(fileKey, out var item))
        {
            var stream = new MemoryStream(item.Data);
            var obj = new StorageObject(
                fileKey,
                stream,
                item.ContentType,
                item.Data.Length,
                "test-etag",
                DateTimeOffset.UtcNow,
                item.Metadata ?? new Dictionary<string, string>()
            );
            return Task.FromResult<StorageObject?>(obj);
        }
        return Task.FromResult<StorageObject?>(null);
    }

    public Task<bool> DeleteAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        _storage.TryRemove(fileKey, out _);
        return Task.FromResult(true);
    }

    public Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default)
    {
        string bucket = request.BucketName ?? "test-bucket";
        return Task.FromResult($"http://localhost/{bucket}/{request.Key}");
    }

    public Task<bool> ExistsAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_storage.ContainsKey(fileKey));
    }

    public Task<FileMetadata?> GetMetadataAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        if (_storage.TryGetValue(fileKey, out var item))
        {
            var meta = new FileMetadata(
                fileKey,
                item.ContentType,
                item.Data.Length,
                "test-etag",
                DateTimeOffset.UtcNow,
                item.Metadata ?? new Dictionary<string, string>()
            );
            return Task.FromResult<FileMetadata?>(meta);
        }
        return Task.FromResult<FileMetadata?>(null);
    }
}
