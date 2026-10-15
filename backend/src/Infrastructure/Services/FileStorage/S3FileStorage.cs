using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.DTOs;
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

    public async Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)
    {
        var bucket = ResolveBucket(request.BucketName);
        if (_options.AutoCreateBucket)
        {
            await EnsureBucketExistsAsync(bucket, cancellationToken);
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

        var resp = await _s3Client.PutObjectAsync(putReq, cancellationToken);

        _logger.LogInformation("Uploaded object {Key} to bucket {Bucket} (ETag: {ETag})",
            request.Key, bucket, resp.ETag);

        string? publicUrl = null;
        if (!string.IsNullOrEmpty(_options.PublicServiceUrl))
        {
            publicUrl = $"{_options.PublicServiceUrl.TrimEnd('/')}/{bucket}/{request.Key.TrimStart('/')}";
        }

        long size = request.ContentLength ?? (request.Content.CanSeek ? request.Content.Length : 0);

        return new UploadFileResponse(
            request.Key,
            bucket,
            resp.ETag ?? string.Empty,
            size,
            publicUrl
        );
    }

    public async Task<StorageObject?> DownloadAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        var bucket = ResolveBucket(bucketName);
        try
        {
            var getReq = new GetObjectRequest
            {
                BucketName = bucket,
                Key = fileKey
            };

            var resp = await _s3Client.GetObjectAsync(getReq, cancellationToken);
            var metadataDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyName in resp.Metadata.Keys)
            {
                var cleanKey = keyName.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase)
                    ? keyName.Substring("x-amz-meta-".Length)
                    : keyName;
                metadataDict[cleanKey] = resp.Metadata[keyName];
            }

            DateTimeOffset? lastModified = resp.LastModified == default || resp.LastModified == DateTime.MinValue
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(resp.LastModified, DateTimeKind.Utc));

            return new StorageObject(
                fileKey,
                resp.ResponseStream,
                resp.Headers.ContentType,
                resp.ContentLength,
                resp.ETag,
                lastModified,
                metadataDict
            );
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey" || ex.ErrorCode == "NotFound")
        {
            _logger.LogWarning("Object {Key} not found in bucket {Bucket}", fileKey, bucket);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        var bucket = ResolveBucket(bucketName);
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = fileKey
            }, cancellationToken);

            _logger.LogInformation("Deleted object {Key} from bucket {Bucket}", fileKey, bucket);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting object {Key} from bucket {Bucket}", fileKey, bucket);
            return false;
        }
    }

    public Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default)
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

        var url = _s3Client.GetPreSignedURL(presignedReq);

        // Rewrite inner Docker container URL (http://storage:9000) to public external URL (http://localhost:9000) for browser clients
        if (!string.IsNullOrEmpty(_options.PublicServiceUrl) && !string.IsNullOrEmpty(_options.ServiceUrl))
        {
            try
            {
                var internalUri = new Uri(_options.ServiceUrl);
                var publicUri = new Uri(_options.PublicServiceUrl);

                if (url.StartsWith(internalUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                {
                    url = publicUri.GetLeftPart(UriPartial.Authority) + url.Substring(internalUri.GetLeftPart(UriPartial.Authority).Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to rewrite presigned URL authority from {ServiceUrl} to {PublicServiceUrl}", _options.ServiceUrl, _options.PublicServiceUrl);
            }
        }

        return Task.FromResult(url);
    }

    public async Task<bool> ExistsAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(fileKey, bucketName, cancellationToken);
        return metadata != null;
    }

    public async Task<FileMetadata?> GetMetadataAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        var bucket = ResolveBucket(bucketName);
        try
        {
            var metaReq = new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = fileKey
            };
            var resp = await _s3Client.GetObjectMetadataAsync(metaReq, cancellationToken);

            var metaDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in resp.Metadata.Keys)
            {
                var cleanKey = k.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase)
                    ? k.Substring("x-amz-meta-".Length)
                    : k;
                metaDict[cleanKey] = resp.Metadata[k];
            }

            DateTimeOffset? lastModified = resp.LastModified == default || resp.LastModified == DateTime.MinValue
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(resp.LastModified, DateTimeKind.Utc));

            return new FileMetadata(
                fileKey,
                resp.Headers.ContentType,
                resp.ContentLength,
                resp.ETag,
                lastModified,
                metaDict
            );
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey" || ex.ErrorCode == "NotFound")
        {
            return null;
        }
    }

    private async Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken)
    {
        try
        {
            bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!exists)
            {
                _logger.LogInformation("Creating missing S3 bucket {Bucket}", bucketName);
                await _s3Client.PutBucketAsync(bucketName, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify or auto-create bucket {Bucket}", bucketName);
        }
    }
}
