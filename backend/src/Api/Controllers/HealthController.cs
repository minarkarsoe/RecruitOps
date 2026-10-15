using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        AppDbContext dbContext,
        IFileStorage fileStorage,
        ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    [HttpGet("/healthz")]
    [HttpGet("/health")]
    public async Task<IActionResult> GetHealthz(CancellationToken ct)
    {
        // 1. PostgreSQL Database Check
        var dbSw = Stopwatch.StartNew();
        bool dbHealthy = false;
        string dbDetails;
        try
        {
            dbHealthy = await _dbContext.Database.CanConnectAsync(ct);
            dbSw.Stop();
            dbDetails = dbHealthy ? "PostgreSQL database connected" : "Database connection refused";
        }
        catch (Exception ex)
        {
            dbSw.Stop();
            dbDetails = $"Database error: {ex.Message}";
            _logger.LogError(ex, "Health check failed for PostgreSQL database");
        }

        // 2. Object Storage Check via IFileStorage.ExistsAsync("__healthcheck__")
        var storageSw = Stopwatch.StartNew();
        bool storageHealthy = false;
        string storageDetails;
        try
        {
            await _fileStorage.ExistsAsync("__healthcheck__", cancellationToken: ct);
            storageHealthy = true;
            storageSw.Stop();
            storageDetails = "Object storage bucket accessible";
        }
        catch (Exception ex)
        {
            storageSw.Stop();
            storageDetails = $"Storage error: {ex.Message}";
            _logger.LogError(ex, "Health check failed for Object Storage");
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
                    Details = dbDetails
                },
                ["storage"] = new HealthCheckItem
                {
                    Status = storageHealthy ? "Healthy" : "Unhealthy",
                    ResponseTimeMs = storageSw.ElapsedMilliseconds,
                    Details = storageDetails
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
    public string Details { get; set; } = string.Empty;
}
