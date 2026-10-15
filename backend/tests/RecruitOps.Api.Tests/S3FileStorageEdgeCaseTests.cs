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

public class S3FileStorageEdgeCaseTests
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorage> _logger;
    private readonly FileStorageOptions _options;

    public S3FileStorageEdgeCaseTests()
    {
        _s3Client = Substitute.For<IAmazonS3>();
        _logger = Substitute.For<ILogger<S3FileStorage>>();
        _options = new FileStorageOptions
        {
            ServiceUrl = "http://storage:9000",
            PublicServiceUrl = "http://localhost:9000",
            BucketName = "recruitops-cvs",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            Region = "us-east-1",
            ForcePathStyle = true,
            AutoCreateBucket = false
        };
    }

    private S3FileStorage CreateStorage(FileStorageOptions? optionsOverride = null)
    {
        var opts = Options.Create(optionsOverride ?? _options);
        return new S3FileStorage(_s3Client, opts, _logger);
    }

    // --- Category 1: Null / Empty File Key ---

    [Fact]
    public async Task UploadAsync_WithNullKey_PublicUrlThrowsNullReferenceException_IfPublicUrlConfigured()
    {
        var storage = CreateStorage();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var request = new UploadFileRequest(
            Key: null!,
            Content: stream,
            ContentType: "application/octet-stream"
        );

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"etag\"", HttpStatusCode = HttpStatusCode.OK });

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(async () =>
        {
            await storage.UploadAsync(request);
        });
    }

    [Fact]
    public async Task UploadAsync_WithNullKey_NoPublicUrl_SucceedsWithNullKeyInResponse()
    {
        var options = new FileStorageOptions
        {
            BucketName = "test-bucket",
            PublicServiceUrl = null
        };
        var storage = CreateStorage(options);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var request = new UploadFileRequest(
            Key: null!,
            Content: stream,
            ContentType: "application/octet-stream"
        );

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"etag\"", HttpStatusCode = HttpStatusCode.OK });

        var response = await storage.UploadAsync(request);
        Assert.Null(response.Key);
        Assert.Null(response.PublicUrl);
    }

    [Fact]
    public async Task DownloadAsync_WithNullKey_PassesNullToS3Client()
    {
        var storage = CreateStorage();
        _s3Client.GetObjectAsync(Arg.Is<GetObjectRequest>(r => r.Key == null), Arg.Any<CancellationToken>())
            .Throws(new ArgumentNullException("Key"));

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await storage.DownloadAsync(null!);
        });
    }

    [Fact]
    public async Task DeleteAsync_WithNullKey_ReturnsFalse_WhenS3ClientThrowsException()
    {
        var storage = CreateStorage();
        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentNullException("Key"));

        var result = await storage.DeleteAsync(null!);
        Assert.False(result);
    }

    // --- Category 2: Binary Data Handling & Non-Seekable Streams ---

    [Fact]
    public async Task UploadAsync_HandlesBinaryData_WithExplicitContentLength()
    {
        var storage = CreateStorage();
        byte[] binaryData = new byte[] { 0x00, 0xFF, 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x7F };
        using var stream = new MemoryStream(binaryData);

        var request = new UploadFileRequest(
            Key: "binary/data.bin",
            Content: stream,
            ContentType: "application/octet-stream",
            ContentLength: binaryData.Length
        );

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"bin-etag\"", HttpStatusCode = HttpStatusCode.OK });

        var response = await storage.UploadAsync(request);
        Assert.Equal(binaryData.Length, response.Size);
    }

    private class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;
        public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task UploadAsync_WithNonSeekableStream_AndNoContentLength_ReturnsSizeZeroInResponse()
    {
        var storage = CreateStorage();
        byte[] data = Encoding.UTF8.GetBytes("Non seekable payload");
        using var stream = new NonSeekableStream(data);

        var request = new UploadFileRequest(
            Key: "stream/nonseekable.dat",
            Content: stream,
            ContentType: "application/octet-stream"
        );

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"etag-nonseekable\"", HttpStatusCode = HttpStatusCode.OK });

        var response = await storage.UploadAsync(request);
        Assert.Equal(0, response.Size);
    }

    // --- Category 3: Presigned URL Generation Parameters ---

    [Fact]
    public async Task GetPresignedUrlAsync_SupportsUploadAccessMode_WithContentType()
    {
        var storage = CreateStorage();
        var request = new PresignedUrlRequest(
            Key: "uploads/new_cv.pdf",
            ExpiresIn: TimeSpan.FromMinutes(15),
            AccessMode: PresignedUrlAccessMode.Upload,
            ContentType: "application/pdf"
        );

        _s3Client.GetPreSignedURL(Arg.Is<GetPreSignedUrlRequest>(r =>
            r.Verb == HttpVerb.PUT && r.ContentType == "application/pdf"))
            .Returns("http://storage:9000/recruitops-cvs/uploads/new_cv.pdf?sig=123");

        var url = await storage.GetPresignedUrlAsync(request);
        Assert.StartsWith("http://localhost:9000/recruitops-cvs/uploads/new_cv.pdf", url);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_SupportsDeleteAccessMode()
    {
        var storage = CreateStorage();
        var request = new PresignedUrlRequest(
            Key: "uploads/old_cv.pdf",
            ExpiresIn: TimeSpan.FromMinutes(5),
            AccessMode: PresignedUrlAccessMode.Delete
        );

        _s3Client.GetPreSignedURL(Arg.Is<GetPreSignedUrlRequest>(r => r.Verb == HttpVerb.DELETE))
            .Returns("http://storage:9000/recruitops-cvs/uploads/old_cv.pdf?sig=del");

        var url = await storage.GetPresignedUrlAsync(request);
        Assert.StartsWith("http://localhost:9000/recruitops-cvs/uploads/old_cv.pdf", url);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithPublicServiceUrlHavingPath_DropsPathInAuthorityRewrite()
    {
        var options = new FileStorageOptions
        {
            ServiceUrl = "http://storage:9000",
            PublicServiceUrl = "http://localhost:9000/custom-path",
            BucketName = "recruitops-cvs"
        };
        var storage = CreateStorage(options);
        var request = new PresignedUrlRequest(
            Key: "file.txt",
            ExpiresIn: TimeSpan.FromMinutes(10)
        );

        _s3Client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("http://storage:9000/recruitops-cvs/file.txt?sig=abc");

        var url = await storage.GetPresignedUrlAsync(request);
        // Note: Uri.GetLeftPart(UriPartial.Authority) strips "/custom-path"
        Assert.Equal("http://localhost:9000/recruitops-cvs/file.txt?sig=abc", url);
    }

    // --- Category 4: Cancellation Token Handling ---

    [Fact]
    public async Task DeleteAsync_SwallowsOperationCanceledException_AndReturnsFalse_InsteadOfPropagating()
    {
        var storage = CreateStorage();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException(cts.Token));

        // Act
        var result = await storage.DeleteAsync("test.pdf", cancellationToken: cts.Token);

        // Assert: DeleteAsync caught Exception, logged error, and returned false instead of throwing OperationCanceledException!
        Assert.False(result);
    }

    [Fact]
    public async Task UploadAsync_WhenAutoCreateBucketIsEnabled_SwallowsCancellationInEnsureBucketExists()
    {
        var options = new FileStorageOptions
        {
            BucketName = "auto-bucket",
            AutoCreateBucket = true
        };
        var storage = CreateStorage(options);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        using var stream = new MemoryStream();
        var request = new UploadFileRequest(Key: "file.txt", Content: stream, ContentType: "text/plain");

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException(cts.Token));

        // EnsureBucketExistsAsync will log a warning when OperationCanceledException is thrown, but swallow it, and proceed to PutObjectAsync
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await storage.UploadAsync(request, cts.Token);
        });
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithPreCancelledToken_DoesNotThrowCancellationException()
    {
        var storage = CreateStorage();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new PresignedUrlRequest(Key: "file.txt", ExpiresIn: TimeSpan.FromMinutes(5));
        _s3Client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("http://storage:9000/recruitops-cvs/file.txt");

        // Act - does not check cancellationToken, returns result
        var url = await storage.GetPresignedUrlAsync(request, cts.Token);
        Assert.NotNull(url);
    }

    // --- Category 5: Exists Check Behavior for Missing Objects & Exception Variations ---

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_ForVarious404AndNoSuchKeyErrorCodes()
    {
        var storage = CreateStorage();

        // 1. ErrorCode NoSuchKey
        _s3Client.GetObjectMetadataAsync(Arg.Is<GetObjectMetadataRequest>(r => r.Key == "k1"), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" });

        // 2. ErrorCode NotFound
        _s3Client.GetObjectMetadataAsync(Arg.Is<GetObjectMetadataRequest>(r => r.Key == "k2"), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NotFound" });

        // 3. StatusCode NotFound with different ErrorCode
        _s3Client.GetObjectMetadataAsync(Arg.Is<GetObjectMetadataRequest>(r => r.Key == "k3"), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "CustomNotFound" });

        Assert.False(await storage.ExistsAsync("k1"));
        Assert.False(await storage.ExistsAsync("k2"));
        Assert.False(await storage.ExistsAsync("k3"));
    }

    [Fact]
    public async Task ExistsAsync_ThrowsAmazonS3Exception_WhenStatusCodeIsForbidden()
    {
        var storage = CreateStorage();
        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Access Denied") { StatusCode = HttpStatusCode.Forbidden, ErrorCode = "AccessDenied" });

        await Assert.ThrowsAsync<AmazonS3Exception>(async () =>
        {
            await storage.ExistsAsync("forbidden.pdf");
        });
    }

    [Fact]
    public async Task GetMetadataAsync_ReturnsNull_WhenObjectIsNotFound()
    {
        var storage = CreateStorage();
        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" });

        var meta = await storage.GetMetadataAsync("missing.pdf");
        Assert.Null(meta);
    }
}
