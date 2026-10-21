using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Api.Controllers;

/// <summary>Liveness and dependency health, for anyone — nginx proxies <c>/health</c> and
/// <c>/healthz</c> unauthenticated.
///
/// <para>So a failing check says <b>which</b> dependency is down in fixed words, never <b>why</b>: an
/// exception message carries hostnames, ports, bucket names and driver text ("Connection refused
/// (localhost:9000)" was seen). The why goes to the API log at error level, with the trace id that
/// the response also carries, so an operator gets from a failing probe to the line that explains
/// it.</para></summary>
[ApiController]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private const string DatabaseHealthy = "PostgreSQL database connected";
    private const string DatabaseUnavailable = "Database unavailable";
    private const string StorageHealthy = "Object storage bucket accessible";
    private const string StorageUnavailable = "Object storage unavailable";

    private readonly AppDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly IRequestOrigin _origin;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        AppDbContext dbContext,
        IFileStorage fileStorage,
        IRequestOrigin origin,
        ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _origin = origin;
        _logger = logger;
    }

    [HttpGet("/healthz")]
    [HttpGet("/health")]
    public async Task<IActionResult> GetHealthz(CancellationToken ct)
    {
        var traceId = _origin.TraceId;

        // 1. PostgreSQL Database Check
        var dbSw = Stopwatch.StartNew();
        bool dbHealthy = false;
        try
        {
            dbHealthy = await _dbContext.Database.CanConnectAsync(ct);
            dbSw.Stop();
            if (!dbHealthy)
            {
                // CanConnectAsync swallows most connection failures and answers false, so this is
                // the usual way the database is down — and it used to log nothing at all.
                _logger.LogError("Health check: PostgreSQL database unreachable (trace {TraceId})", traceId);
            }
        }
        catch (Exception ex)
        {
            dbSw.Stop();
            _logger.LogError(ex, "Health check: PostgreSQL database check failed (trace {TraceId})", traceId);
        }

        // 2. Object Storage Check via IFileStorage.ExistsAsync("__healthcheck__")
        var storageSw = Stopwatch.StartNew();
        bool storageHealthy = false;
        try
        {
            await _fileStorage.ExistsAsync("__healthcheck__", cancellationToken: ct);
            storageHealthy = true;
            storageSw.Stop();
        }
        catch (Exception ex)
        {
            storageSw.Stop();
            _logger.LogError(ex, "Health check: object storage check failed (trace {TraceId})", traceId);
        }

        // 3. Memory & Uptime Metrics
        var process = Process.GetCurrentProcess();
        var allocatedMemory = GC.GetTotalMemory(forceFullCollection: false);
        var workingSetMemory = process.WorkingSet64;
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        var overallHealthy = dbHealthy && storageHealthy;
        var response = new HealthCheckResponse
        {
            Status = overallHealthy ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            TraceId = traceId,
            Uptime = uptime.ToString(@"dd\.hh\:mm\:ss"),
            UptimeSeconds = Math.Round(uptime.TotalSeconds, 2),
            Memory = new MemoryMetrics
            {
                AllocatedBytes = allocatedMemory,
                WorkingSetBytes = workingSetMemory,
                WorkingSetMB = Math.Round(workingSetMemory / (1024.0 * 1024.0), 2)
            },
            Checks = new Dictionary<string, HealthCheckItem>
            {
                ["database"] = new HealthCheckItem
                {
                    Status = dbHealthy ? "Healthy" : "Unhealthy",
                    ResponseTimeMs = dbSw.ElapsedMilliseconds,
                    Details = dbHealthy ? DatabaseHealthy : DatabaseUnavailable
                },
                ["storage"] = new HealthCheckItem
                {
                    Status = storageHealthy ? "Healthy" : "Unhealthy",
                    ResponseTimeMs = storageSw.ElapsedMilliseconds,
                    Details = storageHealthy ? StorageHealthy : StorageUnavailable
                }
            }
        };

        return Ok(response);
    }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; }

    /// <summary>The id the API log lines for this check carry — the same value problem-details
    /// responses call <c>traceId</c>.</summary>
    public string? TraceId { get; set; }

    public string Uptime { get; set; } = string.Empty;
    public double UptimeSeconds { get; set; }
    public MemoryMetrics Memory { get; set; } = new();
    public Dictionary<string, HealthCheckItem> Checks { get; set; } = new();
}

public class MemoryMetrics
{
    public long AllocatedBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    public double WorkingSetMB { get; set; }
}

public class HealthCheckItem
{
    public string Status { get; set; } = "Healthy";
    public long ResponseTimeMs { get; set; }

    /// <summary>Fixed wording only — never an exception message (see <see cref="HealthController"/>).</summary>
    public string Details { get; set; } = string.Empty;
}
