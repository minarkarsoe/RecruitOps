using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Controllers;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

public class Challenger1AdversarialTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public Challenger1AdversarialTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Healthz_Metrics_Are_Mathematically_Consistent_And_Recent()
    {
        var client = _factory.CreateClient();
        var nowBefore = DateTime.UtcNow;

        var response = await client.GetAsync("/healthz");

        var nowAfter = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);

        // 1. Timestamp freshness
        Assert.True(content!.Timestamp >= nowBefore.AddSeconds(-5) && content.Timestamp <= nowAfter.AddSeconds(5),
            $"Timestamp {content.Timestamp} is out of expected UTC range [{nowBefore} - {nowAfter}]");

        // 2. Memory metrics mathematical consistency
        double expectedMb = Math.Round(content.Memory.WorkingSetBytes / (1024.0 * 1024.0), 2);
        Assert.Equal(expectedMb, content.Memory.WorkingSetMB);
        Assert.True(content.Memory.AllocatedBytes > 0);
        Assert.True(content.Memory.WorkingSetBytes > 0);

        // 3. Uptime consistency
        Assert.True(content.UptimeSeconds >= 0);
        Assert.False(string.IsNullOrWhiteSpace(content.Uptime));

        // 4. Individual check timing sanity
        Assert.True(content.Checks["database"].ResponseTimeMs >= 0);
        Assert.True(content.Checks["storage"].ResponseTimeMs >= 0);
    }

    [Fact]
    public async Task RateLimiting_Isolate_IP_Partitions_Correctly()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Login:PermitLimit"] = "10"
                });
            });
        });

        var clientIpA = customFactory.CreateClient();
        
        // Client IP A consumes 10 permits
        for (int i = 0; i < 10; i++)
        {
            var res = await clientIpA.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = $"ipa{i}@example.com", Password = "wrong" });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, res.StatusCode);
        }

        // 11th request from IP A is blocked
        var resBlocked = await clientIpA.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "ipa11@example.com", Password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, resBlocked.StatusCode);
    }

    [Fact]
    public async Task Security_Headers_Are_Present_On_429_RateLimited_Responses()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Login:PermitLimit"] = "2"
                });
            });
        });

        var client = customFactory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "user1@example.com", Password = "pwd" });
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "user2@example.com", Password = "pwd" });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "user3@example.com", Password = "pwd" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        // Verify security headers on 429 response
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("default-src 'self'; frame-ancestors 'none'; object-src 'none';", response.Headers.GetValues("Content-Security-Policy").First());
    }

    [Fact]
    public async Task Healthz_Returns_200_With_Unhealthy_Status_When_Storage_Fails()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Register a failing IFileStorage mock
                services.AddScoped<IFileStorage>(_ => new FailingFileStorageMock());
            });
        });

        var client = customFactory.CreateClient();
        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);
        Assert.Equal("Unhealthy", content!.Status);
        Assert.Equal("Unhealthy", content.Checks["storage"].Status);
        Assert.Contains("Storage error", content.Checks["storage"].Details);
        Assert.Equal("Healthy", content.Checks["database"].Status);
    }

    private class FailingFileStorageMock : IFileStorage
    {
        public Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated MinIO S3 outage");

        public Task<StorageObject?> DownloadAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated MinIO S3 outage");

        public Task<bool> DeleteAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated MinIO S3 outage");

        public Task<bool> ExistsAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated MinIO S3 outage");

        public Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated MinIO S3 outage");

        public Task<FileMetadata?> GetMetadataAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated MinIO S3 outage");
    }
}
