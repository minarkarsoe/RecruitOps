namespace RecruitOps.Infrastructure.Options;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// S3 Endpoint URL.
    /// MinIO Dev: "http://localhost:9000" or "http://storage:9000" inside docker.
    /// Cloudflare R2: "https://<account_id>.r2.cloudflarestorage.com".
    /// </summary>
    public string ServiceUrl { get; set; } = "http://localhost:9000";

    /// <summary>
    /// External public URL for client-facing presigned URLs (useful for Docker container network isolation).
    /// Dev: "http://localhost:9000". Production: null or custom CDN domain.
    /// </summary>
    public string? PublicServiceUrl { get; set; }

    /// <summary>
    /// Primary storage bucket name (e.g. "recruitops-cvs").
    /// </summary>
    public string BucketName { get; set; } = "recruitops-cvs";

    /// <summary>
    /// Access Key ID (MinIO user or Cloudflare R2 Access Key ID).
    /// </summary>
    public string AccessKey { get; set; } = "minioadmin";

    /// <summary>
    /// Secret Access Key (MinIO password or Cloudflare R2 Secret Access Key).
    /// </summary>
    public string SecretKey { get; set; } = "minioadmin";

    /// <summary>
    /// Region string. MinIO: "us-east-1". Cloudflare R2: "auto".
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Force path style URLs (http://endpoint/bucket/key). Required for MinIO.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Auto-create bucket on startup/upload if missing (recommended for local dev).
    /// </summary>
    public bool AutoCreateBucket { get; set; } = true;
}
