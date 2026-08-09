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

public class S3FileStorageTests
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorage> _logger;
    private readonly FileStorageOptions _options;

    public S3FileStorageTests()
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

    [Fact]
    public async Task UploadAsync_StoresObjectAndReturnsResponseWithETagAndPublicUrl()
    {
        // Arrange
        var storage = CreateStorage();
        var contentString = "Sample CV Content";
        var contentBytes = Encoding.UTF8.GetBytes(contentString);
        using var stream = new MemoryStream(contentBytes);

        var request = new UploadFileRequest(
            Key: "resumes/candidate_101.pdf",
            Content: stream,
            ContentType: "application/pdf",
            Metadata: new Dictionary<string, string> { { "CandidateId", "101" } }
        );

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse
            {
                ETag = "\"e10adc3949ba59abbe56e057f20f883e\"",
                HttpStatusCode = HttpStatusCode.OK
            });

        // Act
        var response = await storage.UploadAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("resumes/candidate_101.pdf", response.Key);
        Assert.Equal("recruitops-cvs", response.BucketName);
        Assert.Equal("\"e10adc3949ba59abbe56e057f20f883e\"", response.ETag);
        Assert.Equal(contentBytes.Length, response.Size);
        Assert.Equal("http://localhost:9000/recruitops-cvs/resumes/candidate_101.pdf", response.PublicUrl);

        await _s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == "recruitops-cvs" &&
                r.Key == "resumes/candidate_101.pdf" &&
                r.ContentType == "application/pdf" &&
                r.Metadata["CandidateId"] == "101"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task DownloadAsync_ReturnsStorageObject_WhenObjectExists()
    {
        // Arrange
        var storage = CreateStorage();
        var fileKey = "resumes/candidate_101.pdf";
        var expectedBytes = Encoding.UTF8.GetBytes("PDF Document Payload");
        var responseStream = new MemoryStream(expectedBytes);

        var getObjectResponse = new GetObjectResponse
        {
            BucketName = "recruitops-cvs",
            Key = fileKey,
            ResponseStream = responseStream,
            ContentLength = expectedBytes.Length,
            ETag = "\"etag-12345\"",
            LastModified = DateTime.UtcNow
        };
        getObjectResponse.Headers.ContentType = "application/pdf";
        getObjectResponse.Metadata["CandidateId"] = "101";

        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(getObjectResponse);

        // Act
        var result = await storage.DownloadAsync(fileKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileKey, result.Key);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(expectedBytes.Length, result.ContentLength);
        Assert.Equal("\"etag-12345\"", result.ETag);
        Assert.Equal("101", result.Metadata["CandidateId"]);

        using var reader = new StreamReader(result.Content);
        var readContent = await reader.ReadToEndAsync();
        Assert.Equal("PDF Document Payload", readContent);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNull_WhenObjectNotFound()
    {
        // Arrange
        var storage = CreateStorage();
        var fileKey = "resumes/non_existent.pdf";

        var notFoundEx = new AmazonS3Exception("NoSuchKey")
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "NoSuchKey"
        };

        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(notFoundEx);

        // Act
        var result = await storage.DownloadAsync(fileKey);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_DeletesObjectAndReturnsTrue()
    {
        // Arrange
        var storage = CreateStorage();
        var fileKey = "resumes/candidate_101.pdf";

        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });

        // Act
        var success = await storage.DeleteAsync(fileKey);

        // Assert
        Assert.True(success);
        await _s3Client.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r => r.BucketName == "recruitops-cvs" && r.Key == fileKey),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetPresignedUrlAsync_GeneratesUrlAndRewritesAuthorityToPublicServiceUrl()
    {
        // Arrange
        var storage = CreateStorage();
        var request = new PresignedUrlRequest(
            Key: "resumes/candidate_101.pdf",
            ExpiresIn: TimeSpan.FromMinutes(30),
            AccessMode: PresignedUrlAccessMode.Read
        );

        _s3Client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("http://storage:9000/recruitops-cvs/resumes/candidate_101.pdf?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Signature=abcdef");

        // Act
        var presignedUrl = await storage.GetPresignedUrlAsync(request);

        // Assert
        Assert.NotNull(presignedUrl);
        Assert.StartsWith("http://localhost:9000/recruitops-cvs/resumes/candidate_101.pdf", presignedUrl);
        Assert.Contains("X-Amz-Signature=abcdef", presignedUrl);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenObjectExists()
    {
        // Arrange
        var storage = CreateStorage();
        var fileKey = "resumes/candidate_101.pdf";

        var metadataResponse = new GetObjectMetadataResponse
        {
            ContentLength = 1024,
            ETag = "\"meta-etag\"",
            LastModified = DateTime.UtcNow
        };
        metadataResponse.Headers.ContentType = "application/pdf";

        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(metadataResponse);

        // Act
        var exists = await storage.ExistsAsync(fileKey);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenObjectDoesNotExist()
    {
        // Arrange
        var storage = CreateStorage();
        var fileKey = "resumes/missing.pdf";

        var notFoundEx = new AmazonS3Exception("NotFound")
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "NotFound"
        };

        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Throws(notFoundEx);

        // Act
        var exists = await storage.ExistsAsync(fileKey);

        // Assert
        Assert.False(exists);
    }
}
