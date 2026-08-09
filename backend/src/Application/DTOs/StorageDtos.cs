namespace RecruitOps.Application.DTOs;

public record UploadFileRequest(
    string Key,
    Stream Content,
    string ContentType,
    long? ContentLength = null,
    IDictionary<string, string>? Metadata = null,
    string? BucketName = null
);

public record UploadFileResponse(
    string Key,
    string BucketName,
    string ETag,
    long Size,
    string? PublicUrl = null
);

public record StorageObject(
    string Key,
    Stream Content,
    string ContentType,
    long ContentLength,
    string? ETag,
    DateTimeOffset? LastModified,
    IDictionary<string, string> Metadata
) : IAsyncDisposable, IDisposable
{
    public void Dispose() => Content.Dispose();
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public record FileMetadata(
    string Key,
    string ContentType,
    long ContentLength,
    string? ETag,
    DateTimeOffset? LastModified,
    IDictionary<string, string> Metadata
);

public enum PresignedUrlAccessMode
{
    Read,   // GET
    Upload, // PUT
    Delete  // DELETE
}

public record PresignedUrlRequest(
    string Key,
    TimeSpan ExpiresIn,
    PresignedUrlAccessMode AccessMode = PresignedUrlAccessMode.Read,
    string? ContentType = null,
    string? BucketName = null
);
