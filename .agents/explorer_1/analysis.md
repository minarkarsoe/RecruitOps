# Technical Analysis — RecruitOps Flow 3 Requirement R1 (Backend & Security Middleware)

## Executive Summary
This analysis details the technical architecture, file locations, class structures, middleware configuration, and integration testing strategy for Requirement R1 of RecruitOps Flow 3 (Deployment & Operational Readiness).

The core deliverables for Requirement R1 are:
1. **Operational Health Check Endpoint (`GET /healthz`)**: Detailed JSON health metrics including PostgreSQL database connectivity, Object Storage (`IFileStorage`) bucket connectivity, process memory usage, and application uptime.
2. **Rate Limiting Middleware**: ASP.NET Core rate limiting (10 requests/min per IP) configured for sensitive anonymous endpoints (`POST /api/auth/login` and `POST /api/public/applications` / `POST /api/public/jobs/{token}/apply`).
3. **Security Headers Middleware**: Global response header injection for `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and `Content-Security-Policy`.

---

## 1. System Baseline & Verification Status
- **Test Command**: `dotnet test backend/RecruitOps.sln`
- **Current Suite Status**: **454 tests passing** (51 Domain + 403 Api).
- **Execution Time**: ~11 seconds total.
- **Zero Failures / Zero Skipped**.

---

## 2. Component Architectural Design & File Specifications

### 2.1 Health Check Endpoint (`GET /healthz`)

#### File Location
`backend/src/Api/Controllers/HealthController.cs`

#### Responsibilities
- Provide a light-weight, unauthenticated endpoint at `GET /healthz`.
- Probe PostgreSQL database connectivity via `AppDbContext.Database.CanConnectAsync(cancellationToken)`.
- Probe Object Storage connectivity via `IFileStorage.ExistsAsync("__healthcheck__", cancellationToken)`.
- Retrieve process memory metrics via `GC.GetTotalMemory(false)` and `Process.GetCurrentProcess().WorkingSet64`.
- Calculate application uptime via `DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()`.
- Return HTTP 200 OK with structured JSON containing status, timestamp, uptime, memory, and individual check results.

#### Proposed Code Design
```csharp
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
    public async Task<IActionResult> GetHealthz(CancellationToken ct)
    {
        var startTime = Stopwatch.GetTimestamp();

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

        // 2. Object Storage Check
        var storageSw = Stopwatch.StartNew();
        bool storageHealthy = false;
        string storageDetails;
        try
        {
            // Non-destructive probe call against configured bucket
            await _fileStorage.ExistsAsync("__health_probe__", cancellationToken: ct);
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
```

---

### 2.2 Rate Limiting Middleware Adjustment (10 reqs/min)

#### Target Files
1. `backend/src/Api/Auth/LoginRateLimitOptions.cs`
2. `backend/src/Api/appsettings.json` and `backend/src/Api/appsettings.Development.json`
3. `backend/src/Api/Controllers/PublicJobsController.cs` (Ensure `POST /api/public/applications` or `/api/public/jobs/{token}/apply` has `[EnableRateLimiting(RateLimitPolicies.PublicApply)]`)

#### Configuration Changes
- `LoginRateLimitOptions.PermitLimit` default changed to `10` (from `60`).
- `PublicApplyRateLimitOptions.PermitLimit` default changed to `10` (from `120`).
- `appsettings.json` updated:
```json
  "RateLimit": {
    "Login": {
      "PermitLimit": 10,
      "WindowSeconds": 60
    },
    "PublicApply": {
      "PermitLimit": 10,
      "WindowSeconds": 60
    }
  }
```

#### Routing & Policy Binding
- `POST /api/auth/login` uses policy `RateLimitPolicies.Login` (`"login"`).
- `POST /api/public/jobs/{token}/apply` and optional route `POST /api/public/applications` use policy `RateLimitPolicies.PublicApply` (`"public-apply"`).
- Rate limiter returns HTTP `429 Too Many Requests` with `Retry-After` header when limit (>10 requests in 60s window per IP) is exceeded.

---

### 2.3 Security Headers Middleware

#### File Location
`backend/src/Api/Middleware/SecurityHeadersMiddleware.cs`

#### Required Security Headers
1. `X-Content-Type-Options`: `nosniff`
2. `X-Frame-Options`: `DENY`
3. `Referrer-Policy`: `strict-origin-when-cross-origin`
4. `Content-Security-Policy`: `default-src 'self'; frame-ancestors 'none'; object-src 'none';`

#### Proposed Code Design
```csharp
namespace RecruitOps.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; object-src 'none';";

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
```

#### Program.cs Middleware Pipeline Order
In `backend/src/Api/Program.cs`:
```csharp
var app = builder.Build();

if (behindProxy) app.UseForwardedHeaders();

app.UseSecurityHeaders(); // Added early so ALL responses receive security headers
app.UseCors(DevCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

---

## 3. Integration Testing Strategy

#### File Location
`backend/tests/RecruitOps.Api.Tests/OperationalHealthAndSecurityTests.cs`

#### Proposed Test Cases (8+ Tests)
1. `Get_Healthz_Returns_200_OK_With_Healthy_Status`: Asserts HTTP 200, valid status `"Healthy"`, DB check `"Healthy"`, Storage check `"Healthy"`, memory > 0, uptime > 0.
2. `Get_Healthz_Is_Publicly_Accessible_Without_Authentication`: Asserts no token required for `/healthz`.
3. `Security_Headers_Are_Present_On_Healthz_Response`: Asserts 4 security headers present on `/healthz`.
4. `Security_Headers_Are_Present_On_Auth_Login_Response`: Asserts 4 security headers present on `/api/auth/login`.
5. `Security_Headers_Are_Present_On_404_NotFound_Response`: Asserts 4 security headers present on invalid endpoint response.
6. `Rate_Limiting_Middleware_Blocks_Excessive_Login_Requests_With_429`: Configures factory with `PermitLimit = 10`, fires 11 requests, asserts 11th returns 429.
7. `Rate_Limiting_Middleware_Blocks_Excessive_Public_Apply_Requests_With_429`: Configures factory with `PublicApply PermitLimit = 10`, fires 11 requests, asserts 11th returns 429.
8. `Healthz_Response_Contains_Memory_And_Uptime_Metrics`: Asserts structure of memory and uptime fields.

---

## 4. Implementation File Mapping Summary

| Target Action | File Path | Scope |
|---|---|---|
| Create | `backend/src/Api/Controllers/HealthController.cs` | Health check endpoint `GET /healthz` |
| Create | `backend/src/Api/Middleware/SecurityHeadersMiddleware.cs` | Security headers middleware |
| Modify | `backend/src/Api/Program.cs` | Register `app.UseSecurityHeaders()` |
| Modify | `backend/src/Api/Auth/LoginRateLimitOptions.cs` | Adjust default limits to 10 reqs/min |
| Modify | `backend/src/Api/appsettings.json` | Update `PermitLimit` values to 10 |
| Modify | `backend/src/Api/appsettings.Development.json` | Update `PermitLimit` values to 10 |
| Create | `backend/tests/RecruitOps.Api.Tests/OperationalHealthAndSecurityTests.cs` | Add 8 new xUnit integration tests |
