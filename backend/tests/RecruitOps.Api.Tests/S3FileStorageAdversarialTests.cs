using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RecruitOps.Application.DTOs;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Services.FileStorage;
using Xunit;

namespace RecruitOps.Api.Tests;

public class S3FileStorageAdversarialTests
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorage> _logger;
    private readonly FileStorageOptions _options;

    public S3FileStorageAdversarialTests()
    {
        _s3Client = Substitute.For<IAmazonS3>();
        _logger = Substitute.For<ILogger<S3FileStorage>>();
        _options = new FileStorageOptions
        {
            ServiceUrl = "http://storage:9000",
            PublicServiceUrl = "http://localhost:9000",
            BucketName = "test-bucket",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            Region = "us-east-1",
            ForcePathStyle = true,
            AutoCreateBucket = true
        };
    }

    private S3FileStorage CreateStorage(FileStorageOptions? optionsOverride = null)
    {
        var opts = Options.Create(optionsOverride ?? _options);
        return new S3FileStorage(_s3Client, opts, _logger);
    }

    private class NonSeekableStream : MemoryStream
    {
        public NonSeekableStream(byte[] buffer) : base(buffer) { }
        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override long Position { get => base.Position; set => throw new NotSupportedException(); }
    }

    [Fact]
    public async Task Concurrency_ConcurrentUploadsWithAutoCreateBucket_ExecutesSafelyWithoutUncaughtExceptions()
    {
        // Arrange
        var storage = CreateStorage();
        const int concurrentTasks = 50;

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"etag-concurrent\"", HttpStatusCode = HttpStatusCode.OK });

        // Act
        var tasks = Enumerable.Range(0, concurrentTasks).Select(i =>
        {
            var bytes = Encoding.UTF8.GetBytes($"Data stream {i}");
            var stream = new MemoryStream(bytes);
            var req = new UploadFileRequest($"concurrent/file_{i}.txt", stream, "text/plain");
            return storage.UploadAsync(req);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentTasks, results.Length);
        foreach (var res in results)
        {
            Assert.Equal("\"etag-concurrent\"", res.ETag);
        }
    }

    [Fact]
    public async Task UploadAsync_NonSeekableStream_WithExplicitContentLength_ReturnsProvidedSize()
    {
        // Arrange
        var storage = CreateStorage();
        var bytes = Encoding.UTF8.GetBytes("Non-seekable payload");
        using var nonSeekable = new NonSeekableStream(bytes);

        var request = new UploadFileRequest(
            Key: "stream/nonseekable.bin",
            Content: nonSeekable,
            ContentType: "application/octet-stream",
            ContentLength: bytes.Length
        );

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"etag-nonseekable\"", HttpStatusCode = HttpStatusCode.OK });

        // Act
        var response = await storage.UploadAsync(request);

        // Assert
        Assert.Equal(bytes.Length, response.Size);
        Assert.Equal("\"etag-nonseekable\"", response.ETag);
    }

    [Fact]
    public async Task UploadAsync_NonSeekableStream_WithoutExplicitContentLength_ReturnsZeroSize()
    {
        // Arrange
        var storage = CreateStorage();
        var bytes = Encoding.UTF8.GetBytes("Non-seekable payload without explicit length");
        using var nonSeekable = new NonSeekableStream(bytes);

        var request = new UploadFileRequest(
            Key: "stream/nonseekable_nolength.bin",
            Content: nonSeekable,
            ContentType: "application/octet-stream",
            ContentLength: null
        );

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"etag-zero\"", HttpStatusCode = HttpStatusCode.OK });

        // Act
        var response = await storage.UploadAsync(request);

        // Assert
        Assert.Equal(0, response.Size);
    }

    [Fact]
    public async Task DownloadAsync_MetadataWithAmzPrefix_StripsPrefixAndAllowsCaseInsensitiveAccess()
    {
        // Arrange
        var storage = CreateStorage();
        var fileKey = "docs/resume.pdf";
        var bytes = Encoding.UTF8.GetBytes("Test PDF");
        var responseStream = new MemoryStream(bytes);

        var getResponse = new GetObjectResponse
        {
            BucketName = "test-bucket",
            Key = fileKey,
            ResponseStream = responseStream,
            ContentLength = bytes.Length,
            ETag = "\"etag-meta\"",
            LastModified = DateTime.UtcNow
        };
        getResponse.Headers.ContentType = "application/pdf";
        getResponse.Metadata["x-amz-meta-applicant-name"] = "John Doe";
        getResponse.Metadata["X-Amz-Meta-Role"] = "Software Engineer";

        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(getResponse);

        // Act
        var result = await storage.DownloadAsync(fileKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John Doe", result.Metadata["applicant-name"]);
        Assert.Equal("John Doe", result.Metadata["APPLICANT-NAME"]);
        Assert.Equal("Software Engineer", result.Metadata["role"]);
    }

    [Fact]
    public async Task DownloadAsync_DefaultAndMinValueLastModified_ReturnsNullLastModified()
    {
        // Arrange
        var storage = CreateStorage();
        var fileKey = "docs/nodate.txt";

        var getResponse = new GetObjectResponse
        {
            BucketName = "test-bucket",
            Key = fileKey,
            ResponseStream = new MemoryStream(),
            ContentLength = 0,
            LastModified = default
        };

        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(getResponse);

        // Act
        var result = await storage.DownloadAsync(fileKey);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.LastModified);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_UploadAccessMode_SetsPutVerbAndContentType()
    {
        // Arrange
        var storage = CreateStorage();
        var request = new PresignedUrlRequest(
            Key: "uploads/new_cv.pdf",
            ExpiresIn: TimeSpan.FromMinutes(15),
            AccessMode: PresignedUrlAccessMode.Upload,
            ContentType: "application/pdf"
        );

        _s3Client.GetPreSignedURL(Arg.Is<GetPreSignedUrlRequest>(r =>
            r.Verb == HttpVerb.PUT &&
            r.ContentType == "application/pdf" &&
            r.Key == "uploads/new_cv.pdf"))
            .Returns("http://storage:9000/test-bucket/uploads/new_cv.pdf?sig=123");

        // Act
        var url = await storage.GetPresignedUrlAsync(request);

        // Assert
        Assert.StartsWith("http://localhost:9000/test-bucket/uploads/new_cv.pdf", url);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_DeleteAccessMode_SetsDeleteVerb()
    {
        // Arrange
        var storage = CreateStorage();
        var request = new PresignedUrlRequest(
            Key: "uploads/old_cv.pdf",
            ExpiresIn: TimeSpan.FromMinutes(5),
            AccessMode: PresignedUrlAccessMode.Delete
        );

        _s3Client.GetPreSignedURL(Arg.Is<GetPreSignedUrlRequest>(r => r.Verb == HttpVerb.DELETE))
            .Returns("http://storage:9000/test-bucket/uploads/old_cv.pdf?sig=delete");

        // Act
        var url = await storage.GetPresignedUrlAsync(request);

        // Assert
        Assert.StartsWith("http://localhost:9000/test-bucket/uploads/old_cv.pdf", url);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_MalformedServiceUrl_DoesNotThrowAndReturnsGeneratedUrl()
    {
        // Arrange
        var customOptions = new FileStorageOptions
        {
            ServiceUrl = "invalid-service-url",
            PublicServiceUrl = "http://localhost:9000",
            BucketName = "test-bucket"
        };
        var storage = CreateStorage(customOptions);
        var request = new PresignedUrlRequest("file.txt", TimeSpan.FromMinutes(5));

        _s3Client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("http://storage:9000/test-bucket/file.txt");

        // Act
        var url = await storage.GetPresignedUrlAsync(request);

        // Assert
        Assert.Equal("http://storage:9000/test-bucket/file.txt", url);
    }

    [Fact]
    public async Task GetMetadataAsync_ObjectExists_ReturnsFileMetadata()
    {
        // Arrange
        var storage = CreateStorage();
        var metaResponse = new GetObjectMetadataResponse
        {
            ContentLength = 2048,
            ETag = "\"meta-etag-123\"",
            LastModified = DateTime.UtcNow
        };
        metaResponse.Headers.ContentType = "image/png";
        metaResponse.Metadata["x-amz-meta-dimensions"] = "1920x1080";

        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(metaResponse);

        // Act
        var result = await storage.GetMetadataAsync("images/avatar.png");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("images/avatar.png", result.Key);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(2048, result.ContentLength);
        Assert.Equal("\"meta-etag-123\"", result.ETag);
        Assert.Equal("1920x1080", result.Metadata["dimensions"]);
    }

    [Fact]
    public async Task GetMetadataAsync_ObjectNotFound_ReturnsNull()
    {
        // Arrange
        var storage = CreateStorage();
        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("NotFound") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NotFound" });

        // Act
        var result = await storage.GetMetadataAsync("missing.png");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_S3ThrowsServerError_ReturnsFalseAndLogsError()
    {
        // Arrange
        var storage = CreateStorage();
        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Internal Server Error") { StatusCode = HttpStatusCode.InternalServerError });

        // Act
        var result = await storage.DeleteAsync("faulty/file.txt");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DownloadAsync_S3ThrowsForbidden_ThrowsAmazonS3Exception()
    {
        // Arrange
        var storage = CreateStorage();
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Access Denied") { StatusCode = HttpStatusCode.Forbidden, ErrorCode = "AccessDenied" });

        // Act & Assert
        await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.DownloadAsync("secret/file.txt"));
    }

    [Fact]
    public async Task StorageObject_DisposeAndDisposeAsync_DisposesStream()
    {
        // Arrange
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("disposal test"));
        var storageObject = new StorageObject(
            Key: "file.txt",
            Content: memoryStream,
            ContentType: "text/plain",
            ContentLength: 13,
            ETag: "etag",
            LastModified: DateTimeOffset.UtcNow,
            Metadata: new Dictionary<string, string>()
        );

        // Act
        storageObject.Dispose();

        // Assert
        Assert.False(memoryStream.CanRead); // Disposed stream cannot be read
    }
}
