# Detailed Changes Log — Worker 1 (Backend Operational Readiness & Startup RBAC Seeding)

## 1. Operational Health Check Endpoint (`GET /healthz`)
- **File Created**: `backend/src/Api/Controllers/HealthController.cs`
- **Implementation**:
  - Endpoint `GET /healthz` annotated with `[AllowAnonymous]`.
  - Database connectivity check via `_dbContext.Database.CanConnectAsync(ct)`.
  - Storage connectivity check via `_fileStorage.ExistsAsync("__healthcheck__", cancellationToken: ct)`.
  - Memory metrics capture using `GC.GetTotalMemory(false)` and `Process.GetCurrentProcess().WorkingSet64` (and MB calculation).
  - Uptime calculation (`DateTime.UtcNow - process.StartTime.ToUniversalTime()`).
  - Returns HTTP 200 OK with `HealthCheckResponse` JSON structure containing overall `status`, `timestamp`, `uptime`, `memory`, and `checks` dictionary.

## 2. Rate Limiting Configuration
- **Files Modified/Created**:
  - `backend/src/Api/Auth/LoginRateLimitOptions.cs` (Updated default `PermitLimit = 10`)
  - `backend/src/Api/Auth/PublicApplyRateLimitOptions.cs` (Created/updated default `PermitLimit = 10`)
  - `backend/src/Api/appsettings.json` (Updated `RateLimit.Login.PermitLimit = 10` and `RateLimit.PublicApply.PermitLimit = 10`)
  - `backend/src/Api/appsettings.Development.json` (Added `RateLimit` section with `PermitLimit = 10` for both Login and PublicApply)
  - `backend/tests/RecruitOps.Api.Tests/CustomWebAppFactory.cs` (Set `RateLimit:PublicApply:PermitLimit = 10000` alongside `Login` to allow test suite scenarios to run without hitting rate limiters)

## 3. Security Headers Middleware
- **File Created**: `backend/src/Api/Middleware/SecurityHeadersMiddleware.cs`
- **Implementation**:
  - ASP.NET Core middleware injecting the following HTTP response headers on every request:
    - `X-Content-Type-Options: nosniff`
    - `X-Frame-Options: DENY`
    - `Referrer-Policy: strict-origin-when-cross-origin`
    - `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'; object-src 'none';`
- **File Modified**: `backend/src/Api/Program.cs`
  - Registered `app.UseSecurityHeaders()` in the HTTP request pipeline before CORS and rate limiting.

## 4. Unconditional Startup RBAC Seeding
- **File Modified**: `backend/src/Infrastructure/Persistence/DbInitializer.cs`
  - Added `public static async Task SeedPermissionsAndRolesAsync(IServiceProvider services, CancellationToken ct = default)` overload.
- **File Modified**: `backend/src/Api/Program.cs`
  - Called `await DbInitializer.SeedPermissionsAndRolesAsync(app.Services);` unconditionally on startup so canonical permissions (39) and system roles (7) exist in all environments (including Production).

## 5. Integration Test Suite
- **File Created**: `backend/tests/RecruitOps.Api.Tests/OperationalHealthAndSecurityTests.cs`
- **Added 10 new xUnit integration tests**:
  1. `Get_Healthz_Returns_200_OK_With_Healthy_Status`
  2. `Get_Healthz_Is_Publicly_Accessible_Without_Authentication`
  3. `Get_Healthz_Returns_Valid_Health_Metrics_Format`
  4. `Get_Healthz_Database_And_Storage_Checks_Are_Healthy`
  5. `Security_Headers_Are_Present_On_Healthz_Response`
  6. `Security_Headers_Are_Present_On_Auth_Login_Response`
  7. `Security_Headers_Are_Present_On_404_NotFound_Response`
  8. `Rate_Limiting_Middleware_Blocks_Excessive_Login_Requests_With_429`
  9. `Rate_Limiting_Middleware_Blocks_Excessive_Public_Apply_Requests_With_429`
  10. `Rate_Limiting_Response_Contains_RetryAfter_Header`

## 6. Verification Summary
- Baseline tests passing: 454 (51 Domain + 403 Api)
- New tests added: 10
- Final test suite: **464 tests passing** (51 Domain + 413 Api), 0 failed, 0 skipped across `dotnet test backend/RecruitOps.sln`.
