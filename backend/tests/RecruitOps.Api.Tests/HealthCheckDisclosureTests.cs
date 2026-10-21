using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RecruitOps.Api.Controllers;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary><c>/health</c> and <c>/healthz</c> answer anyone — nginx proxies them unauthenticated — so
/// a failing check says which dependency is down, never why. The response used to carry the raw
/// exception message ("Connection refused (localhost:9000)" was seen on the production stack); the
/// detail belongs in the API log, under the trace id the response gives the operator.
///
/// <para>Each failure below carries the kind of text a real one does — a host, a port, a user name —
/// and each test asserts on the raw body, not just the field it expects, so the text cannot reappear
/// under a new key.</para></summary>
public class HealthCheckDisclosureTests : IClassFixture<CustomWebAppFactory>
{
    private const string StorageFailure = "Connection refused (minio.internal.example:9000) for access key recruitops-api";
    private const string DatabaseFailure = "Failed to connect to 10.20.30.40:5432 as user recruitops_owner";

    private readonly CustomWebAppFactory _factory;

    public HealthCheckDisclosureTests(CustomWebAppFactory factory) => _factory = factory;

    private (HttpClient Client, CapturingLoggerProvider Logs) ClientWith(Action<IServiceCollection> replace)
    {
        var logs = new CapturingLoggerProvider();
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(replace);
        });
        return (factory.CreateClient(), logs);
    }

    private static void UseDatabase(IServiceCollection services, Exception? failure)
    {
        services.RemoveAll<AppDbContext>();
        services.AddScoped<AppDbContext>(sp => new UnreachableDbContext(
            sp.GetRequiredService<DbContextOptions<AppDbContext>>(),
            sp.GetRequiredService<ICurrentTenant>(),
            failure));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/healthz")]
    public async Task A_Storage_Failure_Says_Which_Not_Why_And_Logs_Why(string path)
    {
        var (client, logs) = ClientWith(services =>
            services.AddScoped<IFileStorage>(_ => new FailingFileStorage(new InvalidOperationException(StorageFailure))));

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // unchanged: the body says Unhealthy
        Assert.DoesNotContain("minio.internal.example", body);
        Assert.DoesNotContain("recruitops-api", body);
        Assert.DoesNotContain("Connection refused", body);

        var health = (await response.Content.ReadFromJsonAsync<HealthCheckResponse>())!;
        Assert.Equal("Unhealthy", health.Status);
        Assert.Equal("Unhealthy", health.Checks["storage"].Status);
        Assert.Equal("Object storage unavailable", health.Checks["storage"].Details);
        Assert.Equal("Healthy", health.Checks["database"].Status); // positive control

        // The why is in the log, findable by the trace id the response handed out.
        Assert.False(string.IsNullOrEmpty(health.TraceId));
        var entry = Assert.Single(logs.Entries, e => e.Exception?.Message == StorageFailure);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(health.TraceId!, entry.Message);
    }

    [Fact]
    public async Task A_Database_Exception_Says_Which_Not_Why_And_Logs_Why()
    {
        var (client, logs) = ClientWith(services => UseDatabase(services, new InvalidOperationException(DatabaseFailure)));

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("10.20.30.40", body);
        Assert.DoesNotContain("recruitops_owner", body);
        Assert.DoesNotContain("Failed to connect", body);

        var health = (await response.Content.ReadFromJsonAsync<HealthCheckResponse>())!;
        Assert.Equal("Unhealthy", health.Checks["database"].Status);
        Assert.Equal("Database unavailable", health.Checks["database"].Details);
        Assert.Equal("Healthy", health.Checks["storage"].Status); // positive control

        var entry = Assert.Single(logs.Entries, e => e.Exception?.Message == DatabaseFailure);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(health.TraceId!, entry.Message);
    }

    [Fact]
    public async Task A_Database_That_Will_Not_Connect_Is_Logged_Too()
    {
        // CanConnectAsync answers false for most connection failures rather than throwing — the
        // usual way the database is down, and it used to log nothing.
        var (client, logs) = ClientWith(services => UseDatabase(services, failure: null));

        var health = (await client.GetFromJsonAsync<HealthCheckResponse>("/health"))!;

        Assert.Equal("Unhealthy", health.Checks["database"].Status);
        Assert.Equal("Database unavailable", health.Checks["database"].Details);
        Assert.Single(logs.Entries, e => e.Level == LogLevel.Error
            && e.Category == typeof(HealthController).FullName && e.Message.Contains(health.TraceId!));
    }

    /// <summary>The fixture's in-memory context, except that asking whether the database is reachable
    /// throws <c>failure</c> — or answers false when there is none.</summary>
    private sealed class UnreachableDbContext : AppDbContext
    {
        private readonly Exception? _failure;
        private DatabaseFacade? _database;

        public UnreachableDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant tenant, Exception? failure)
            : base(options, tenant) => _failure = failure;

        public override DatabaseFacade Database => _database ??= new UnreachableDatabase(this, _failure);
    }

    private sealed class UnreachableDatabase : DatabaseFacade
    {
        private readonly Exception? _failure;

        public UnreachableDatabase(DbContext context, Exception? failure) : base(context) => _failure = failure;

        public override Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
            => _failure is null ? Task.FromResult(false) : Task.FromException<bool>(_failure);
    }

    private sealed class FailingFileStorage : IFileStorage
    {
        private readonly Exception _failure;

        public FailingFileStorage(Exception failure) => _failure = failure;

        public Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<UploadFileResponse>(_failure);

        public Task<StorageObject?> DownloadAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => Task.FromException<StorageObject?>(_failure);

        public Task<bool> DeleteAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => Task.FromException<bool>(_failure);

        public Task<bool> ExistsAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => Task.FromException<bool>(_failure);

        public Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<string>(_failure);

        public Task<FileMetadata?> GetMetadataAsync(string fileKey, string? bucketName = null, CancellationToken cancellationToken = default)
            => Task.FromException<FileMetadata?>(_failure);
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

        public void Dispose() { }

        private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
        }
    }
}
