# Survey R1: Object Storage Abstraction Analysis & Technical Specification

**Target System:** RecruitOps Backend (`.NET 10 LTS`)  
**Author:** teamwork_preview_explorer (Survey R1)  
**Date:** 2026-08-07  
**Status:** Approved Architectural Survey & Implementation Plan  
**Related ADR:** [ADR-0013: Infrastructure, Cloudflare R2 & MinIO Abstraction](../../docs/decisions/ADR-0013-infrastructure-and-storage.md)

---

## 1. Executive Summary & Context

RecruitOps requires an Object Storage Abstraction to manage candidate resumes (CVs), application attachments, candidate photo avatars, and generated export documents. 

Per **ADR-0013**, RecruitOps deployments fall into two primary infrastructure topologies:
1. **Cloud-Hosted Installs (SaaS):** Backed by **Cloudflare R2** to eliminate egress bandwidth fees during heavy resume viewing by recruiters and hiring managers.
2. **On-Premise / Enterprise Installs:** Backed by **local MinIO** (or customer-provided S3 storage) to guarantee strict data sovereignty and local data residency.

To maintain a single, portable container image across both topologies, **the Application layer must interact exclusively with a clean `IFileStorage` abstraction**, while the **Infrastructure layer provides an S3-compatible implementation (`S3FileStorage`)**. Neither Cloudflare R2 nor MinIO APIs will ever be called directly outside of this abstraction.

---

## 2. Codebase Inspection & Current State Analysis

### 2.1 Project Structure & Existing Dependencies
The backend is structured according to Clean Architecture principles using .NET 10 LTS:
- `backend/src/Domain`: Core domain entities (User, Candidate, Requisition, etc.). Zero external package dependencies.
- `backend/src/Application`: Application services, interfaces, DTOs, request handlers. Depends only on `Domain`.
- `backend/src/Infrastructure`: EF Core PostgreSQL (`Npgsql`), System.IdentityModel.Tokens.Jwt, ASP.NET Core Identity. Depends on `Application` & `Domain`.
- `backend/src/Api`: ASP.NET Core Web API controllers, Swagger, Authentication/Authorization handlers.
- `backend/tests`: `RecruitOps.Domain.Tests` (51 tests) and `RecruitOps.Api.Tests` (218 tests) - total 269 passing tests.

### 2.2 Existing File Handling & Storage Status
- **Current File Handling:** There are currently **no object storage interfaces or implementations** in `backend/src/Application` or `backend/src/Infrastructure`.
- **Existing NuGet Packages:** `RecruitOps.Infrastructure.csproj` currently includes `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `System.IdentityModel.Tokens.Jwt`. **No S3 SDK (e.g. AWSSDK.S3 or Minio) is installed yet.**
- **Docker Compose Setup (`docker-compose.yml`):**
  A local S3-compatible MinIO service is already configured under the `storage` service key:
  ```yaml
  storage:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: ${MINIO_USER:-minioadmin}
      MINIO_ROOT_PASSWORD: ${MINIO_PASSWORD:-minioadmin}
    ports:
      - "9000:9000" # S3 API endpoint
      - "9001:9001" # Web Console
    volumes:
      - miniodata:/data
    healthcheck:
      test: ["CMD", "mc", "ready", "local"]
      interval: 10s
      timeout: 5s
      retries: 5
  ```

---

## 3. Package Selection Rationale: `AWSSDK.S3` vs `Minio`

| Package | AWS S3 | Cloudflare R2 | MinIO Local | License | Recommendation |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`AWSSDK.S3`** (3.7.x) | Native | Officially Recommended by Cloudflare | Fully Supported (Path Style) | Apache 2.0 | **SELECTED** |
| **`Minio`** (6.0.x) | Supported | Compatible via S3 API | Native | Apache 2.0 | Secondary / Not Needed |

### Selection Rationale:
1. **Standardization:** Cloudflare R2 uses the S3 API standard. Cloudflare's official .NET integration guide explicitly recommends `AWSSDK.S3`.
2. **Path-Style & Custom Endpoint Support:** `AWSSDK.S3` allows custom `ServiceURL` and `ForcePathStyle = true`, making it 100% compatible with local MinIO (`http://localhost:9000` or `http://storage:9000`).
3. **Presigned URL Generation:** `AWSSDK.S3` provides robust, built-in presigned URL generation for both `GET` (download) and `PUT` (direct browser upload).
4. **Permissive License:** Apache 2.0 license strictly adheres to the project's non-copyleft license requirement.

**Required Package to Add:**
- Project: `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj`
- Package: `AWSSDK.S3` (version `3.7.400` or latest stable `3.7.*`).

---

## 4. Application Layer Design (`IFileStorage`)

The `IFileStorage` interface must be located in `backend/src/Application/Interfaces/IFileStorage.cs` (or `backend/src/Application/Common/IFileStorage.cs`).

### 4.1 Data Transfer Objects (`backend/src/Application/DTOs/Storage/StorageDtos.cs`)

```csharp
namespace RecruitOps.Application.DTOs.Storage;

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
```

### 4.2 Application Interface (`backend/src/Application/Interfaces/IFileStorage.cs`)

```csharp
using RecruitOps.Application.DTOs.Storage;

namespace RecruitOps.Application.Interfaces;

public interface IFileStorage
{
    /// <summary>
    /// Uploads a stream/file to object storage.
    /// </summary>
    Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Downloads an object stream and metadata by storage key. Returns null if object does not exist.
    /// </summary>
    Task<StorageObject?> DownloadAsync(string key, string? bucketName = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes an object by key. Returns true if successful or if object was already absent.
    /// </summary>
    Task<bool> DeleteAsync(string key, string? bucketName = null, CancellationToken ct = default);

    /// <summary>
    /// Generates a presigned temporary URL for direct upload or download.
    /// </summary>
    Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken ct = default);

    /// <summary>
    /// Checks if an object exists in storage by key.
    /// </summary>
    Task<bool> ExistsAsync(string key, string? bucketName = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves object metadata without downloading full stream payload.
    /// </summary>
    Task<FileMetadata?> GetMetadataAsync(string key, string? bucketName = null, CancellationToken ct = default);
}
```

---

## 5. Infrastructure Layer Implementation (`S3FileStorage`)

### 5.1 Configuration Options (`backend/src/Infrastructure/Options/FileStorageOptions.cs`)

```csharp
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
```

### 5.2 Service Implementation (`backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs`)

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.DTOs.Storage;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Options;

namespace RecruitOps.Infrastructure.Services.FileStorage;

public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly FileStorageOptions _options;
    private readonly ILogger<S3FileStorage> _logger;

    public S3FileStorage(
        IAmazonS3 s3Client,
        IOptions<FileStorageOptions> options,
        ILogger<S3FileStorage> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    private string ResolveBucket(string? overrideBucket)
        => string.IsNullOrWhiteSpace(overrideBucket) ? _options.BucketName : overrideBucket;

    public async Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken ct = default)
    {
        var bucket = ResolveBucket(request.BucketName);
        if (_options.AutoCreateBucket)
        {
            await EnsureBucketExistsAsync(bucket, ct);
        }

        var putReq = new PutObjectRequest
        {
            BucketName = bucket,
            Key = request.Key,
            InputStream = request.Content,
            ContentType = request.ContentType,
            AutoCloseStream = false
        };

        if (request.Metadata != null)
        {
            foreach (var (k, v) in request.Metadata)
            {
                putReq.Metadata[k] = v;
            }
        }

        var resp = await _s3Client.PutObjectAsync(putReq, ct);
        
        _logger.LogInformation("Uploaded object {Key} to bucket {Bucket} (ETag: {ETag})", 
            request.Key, bucket, resp.ETag);

        string? publicUrl = null;
        if (!string.IsNullOrEmpty(_options.PublicServiceUrl))
        {
            publicUrl = $"{_options.PublicServiceUrl.TrimEnd('/')}/{bucket}/{request.Key}";
        }

        return new UploadFileResponse(
            request.Key,
            bucket,
            resp.ETag,
            request.Content.CanSeek ? request.Content.Length : 0,
            publicUrl
        );
    }

    public async Task<StorageObject?> DownloadAsync(string key, string? bucketName = null, CancellationToken ct = default)
    {
        var bucket = ResolveBucket(bucketName);
        try
        {
            var getReq = new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            };

            var resp = await _s3Client.GetObjectAsync(getReq, ct);
            var metadataDict = new Dictionary<string, string>();
            foreach (var keyName in resp.Metadata.Keys)
            {
                metadataDict[keyName] = resp.Metadata[keyName];
            }

            return new StorageObject(
                key,
                resp.ResponseStream,
                resp.Headers.ContentType,
                resp.ContentLength,
                resp.ETag,
                resp.LastModified,
                metadataDict
            );
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Object {Key} not found in bucket {Bucket}", key, bucket);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string key, string? bucketName = null, CancellationToken ct = default)
    {
        var bucket = ResolveBucket(bucketName);
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = key
            }, ct);

            _logger.LogInformation("Deleted object {Key} from bucket {Bucket}", key, bucket);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting object {Key} from bucket {Bucket}", key, bucket);
            return false;
        }
    }

    public Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken ct = default)
    {
        var bucket = ResolveBucket(request.BucketName);
        var verb = request.AccessMode switch
        {
            PresignedUrlAccessMode.Upload => HttpVerb.PUT,
            PresignedUrlAccessMode.Delete => HttpVerb.DELETE,
            _ => HttpVerb.GET
        };

        var presignedReq = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = request.Key,
            Verb = verb,
            Expires = DateTime.UtcNow.Add(request.ExpiresIn)
        };

        if (!string.IsNullOrEmpty(request.ContentType) && request.AccessMode == PresignedUrlAccessMode.Upload)
        {
            presignedReq.ContentType = request.ContentType;
        }

        var url = _s3Client.GetPreSignedUrl(presignedReq);

        // Rewrite inner Docker container URL (http://storage:9000) to public external URL (http://localhost:9000) for browser clients
        if (!string.IsNullOrEmpty(_options.PublicServiceUrl) && !string.IsNullOrEmpty(_options.ServiceUrl))
        {
            var internalUri = new Uri(_options.ServiceUrl);
            var publicUri = new Uri(_options.PublicServiceUrl);

            if (url.StartsWith(internalUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                url = publicUri.GetLeftPart(UriPartial.Authority) + url.Substring(internalUri.GetLeftPart(UriPartial.Authority).Length);
            }
        }

        return Task.FromResult(url);
    }

    public async Task<bool> ExistsAsync(string key, string? bucketName = null, CancellationToken ct = default)
    {
        var metadata = await GetMetadataAsync(key, bucketName, ct);
        return metadata != null;
    }

    public async Task<FileMetadata?> GetMetadataAsync(string key, string? bucketName = null, CancellationToken ct = default)
    {
        var bucket = ResolveBucket(bucketName);
        try
        {
            var metaReq = new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = key
            };
            var resp = await _s3Client.GetObjectMetadataAsync(metaReq, ct);

            var metaDict = new Dictionary<string, string>();
            foreach (var k in resp.Metadata.Keys)
            {
                metaDict[k] = resp.Metadata[k];
            }

            return new FileMetadata(
                key,
                resp.Headers.ContentType,
                resp.ContentLength,
                resp.ETag,
                resp.LastModified,
                metaDict
            );
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        try
        {
            bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!exists)
            {
                _logger.LogInformation("Creating missing S3 bucket {Bucket}", bucketName);
                await _s3Client.PutBucketAsync(bucketName, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify or auto-create bucket {Bucket}", bucketName);
        }
    }
}
```

---

## 6. Settings & Environment Variables Matrix

### 6.1 `appsettings.json` (Base Settings)
Add the following block to `backend/src/Api/appsettings.json`:

```json
{
  "FileStorage": {
    "ServiceUrl": "http://localhost:9000",
    "PublicServiceUrl": "http://localhost:9000",
    "BucketName": "recruitops-cvs",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "Region": "us-east-1",
    "ForcePathStyle": true,
    "AutoCreateBucket": true
  }
}
```

### 6.2 Environment Variable Matrix for Cloudflare R2 vs MinIO

| Environment Variable | Local MinIO (Docker Compose) | Cloudflare R2 (Hosted Production) |
| :--- | :--- | :--- |
| `FileStorage__ServiceUrl` | `http://storage:9000` | `https://<ACCOUNT_ID>.r2.cloudflarestorage.com` |
| `FileStorage__PublicServiceUrl` | `http://localhost:9000` | `https://media.recruitops.com` (or empty) |
| `FileStorage__BucketName` | `recruitops-dev` | `recruitops-prod-cvs` |
| `FileStorage__AccessKey` | `minioadmin` (or `${MINIO_USER}`) | `<R2_ACCESS_KEY_ID>` |
| `FileStorage__SecretKey` | `minioadmin` (or `${MINIO_PASSWORD}`) | `<R2_SECRET_ACCESS_KEY>` |
| `FileStorage__Region` | `us-east-1` | `auto` |
| `FileStorage__ForcePathStyle` | `true` | `true` (or `false`) |
| `FileStorage__AutoCreateBucket` | `true` | `false` |

### 6.3 Update to `docker-compose.yml`
Under `services.api.environment` in `docker-compose.yml`:
```yaml
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Default: "Host=db;Port=5432;Database=${POSTGRES_DB:-recruitops};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}"
      Jwt__Issuer: recruitops
      Jwt__Audience: recruitops-api
      Jwt__Key: ${JWT_KEY:?set JWT_KEY in .env - must be at least 32 characters}
      FileStorage__ServiceUrl: "http://storage:9000"
      FileStorage__PublicServiceUrl: "http://localhost:9000"
      FileStorage__BucketName: "recruitops-dev"
      FileStorage__AccessKey: ${MINIO_USER:-minioadmin}
      FileStorage__SecretKey: ${MINIO_PASSWORD:-minioadmin}
      FileStorage__ForcePathStyle: "true"
```

---

## 7. Dependency Injection Registration

Update `backend/src/Infrastructure/DependencyInjection.cs`:

```csharp
// Register FileStorageOptions
services.Configure<FileStorageOptions>(config.GetSection(FileStorageOptions.SectionName));

// Register IAmazonS3 SDK Client
services.AddSingleton<IAmazonS3>(sp =>
{
    var options = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
    var s3Config = new AmazonS3Config
    {
        ServiceURL = options.ServiceUrl,
        ForcePathStyle = options.ForcePathStyle,
        AuthenticationRegion = options.Region
    };
    return new AmazonS3Client(options.AccessKey, options.SecretKey, s3Config);
});

// Register IFileStorage
services.AddScoped<IFileStorage, S3FileStorage>();
```

---

## 8. Verification Strategy & Test Specifications

To meet Acceptance Criteria R1 (at least 3 integration/unit tests covering upload, download, delete operations), we specify unit/integration tests in `tests/RecruitOps.Api.Tests/Storage/S3FileStorageTests.cs` (or mock-backed storage tests).

### 8.1 Test Specifications

1. **Upload File Test (`UploadAsync_ShouldStoreObjectAndReturnETag`):**
   - Uploads a test PDF stream `sample_cv.pdf` with metadata (`CandidateId: 101`).
   - Asserts response contains matching key, non-null ETag, and correct size.

2. **Download File Test (`DownloadAsync_ShouldRetrieveUploadedContent`):**
   - Uploads content, then calls `DownloadAsync`.
   - Asserts returned `StorageObject` stream content matches uploaded bytes verbatim and contentType matches `application/pdf`.

3. **Delete File Test (`DeleteAsync_ShouldRemoveObject`):**
   - Uploads an object, calls `DeleteAsync`, and subsequently calls `ExistsAsync` or `DownloadAsync`.
   - Asserts `ExistsAsync` returns `false` and `DownloadAsync` returns `null`.

4. **Presigned URL Generation Test (`GetPresignedUrlAsync_ShouldGenerateValidUrl`):**
   - Calls `GetPresignedUrlAsync` for `Read` and `Upload` modes.
   - Asserts returned string is a valid URL containing signature parameters and key path.

---

## 9. Implementation Checklist for Task Implementer

- [ ] Add `AWSSDK.S3` (3.7.*) package to `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj`.
- [ ] Create DTO records in `backend/src/Application/DTOs/Storage/StorageDtos.cs`.
- [ ] Create `IFileStorage` in `backend/src/Application/Interfaces/IFileStorage.cs`.
- [ ] Create `FileStorageOptions` in `backend/src/Infrastructure/Options/FileStorageOptions.cs`.
- [ ] Create `S3FileStorage` in `backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs`.
- [ ] Add `FileStorageOptions` & `IFileStorage` registrations in `backend/src/Infrastructure/DependencyInjection.cs`.
- [ ] Update `backend/src/Api/appsettings.json` and `docker-compose.yml`.
- [ ] Implement unit/integration tests in `tests/RecruitOps.Api.Tests/Storage/S3FileStorageTests.cs`.
- [ ] Run `dotnet test backend/RecruitOps.sln` to confirm all 269 existing tests + new storage tests pass.
