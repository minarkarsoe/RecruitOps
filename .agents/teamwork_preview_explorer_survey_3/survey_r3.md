# Technical Survey R3: Refresh Token Mechanism Architecture & Implementation Plan

**Author:** teamwork_preview_explorer (Survey R3)  
**Date:** 2026-08-07  
**Project:** RecruitOps SaaS Platform  
**Target Milestone:** R3 — Refresh Token Mechanism  

---

## 1. Executive Summary

This document provides a comprehensive technical survey and architectural blueprint for **Requirement 3 (R3): Refresh Token Mechanism** in the RecruitOps SaaS platform. 

Currently, RecruitOps issues a self-signed JWT access token with an 8-hour lifetime upon successful authentication (`POST /api/auth/login`). When this token expires, requests return HTTP 401, forcing the user to re-authenticate manually.

R3 extends the authentication pipeline to support **revocable server-side refresh tokens**, allowing the frontend SPA to silently renew access tokens without user interruption while enforcing secure token rotation and reuse detection.

---

## 2. Current Architecture & Codebase Analysis

### 2.1 Backend Authentication Flow
- **User Entity (`backend/src/Domain/Entities/User.cs`):**
  Inherits from `BaseEntity` (`Guid Id`, `DateTimeOffset CreatedAt`, `DateTimeOffset? UpdatedAt`) and implements `ITenantScoped` (`Guid TenantId`). Stores hashed credentials via ASP.NET Core `IPasswordHasher<User>`.
- **Token Generation (`backend/src/Infrastructure/Services/JwtTokenService.cs`):**
  Implements `ITokenService`. Mints self-issued HS256 JWT access tokens with claims: `sub` (UserId), `tenant_id`, `role`, `email`, `name`, `is_super_admin`. Has a fixed 8-hour lifetime (`LifetimeHours = 8`).
- **Auth Service (`backend/src/Infrastructure/Services/AuthService.cs`):**
  Handles `LoginAsync`. Queries `_db.Users.IgnoreQueryFilters()` by email to bypass initial tenant filters. Implements dummy credential verification (`DummyCredential`) to prevent response timing email enumeration. Resolves user permission sets via `IPermissionEvaluator`.
- **Auth Controller (`backend/src/Api/Controllers/AuthController.cs`):**
  Exposes `POST /api/auth/login` (`[AllowAnonymous]`). Rate-limited on two axes per ADR-0016:
  1. Per client IP via ASP.NET Core RateLimiter (`RateLimitPolicies.Login`: 60 req/60s).
  2. Per account via `ILoginThrottle` (5 failures → 15 minute lockout).
- **Database Context (`backend/src/Infrastructure/Persistence/AppDbContext.cs`):**
  Uses EF Core with PostgreSQL. Automatically stamps `TenantId` and `UpdatedAt` timestamps via `StampTenantAndTimestamps()` during `SaveChangesAsync()`. Global tenant query filters exist on all tenant-scoped entities.

### 2.2 Frontend Authentication & API Client
- **Session Storage (`frontend/internal/src/lib/auth.ts`):**
  Session object stored in `sessionStorage` (key: `'recruitops.session'`) containing `accessToken`, `expiresAtUtc`, `role`, `displayName`, `userId`, `isSuperAdmin`, `activeTenantId`, and `permissions`. `auth.get()` automatically drops sessions if `expiresAtUtc` has passed.
- **Shared Types (`packages/types/src/index.ts`):**
  Defines `LoginRequest` and `LoginResponse` shared across monorepo workspaces (`@recruitops/internal`, `@recruitops/types`, `@recruitops/ui`).
- **API Fetch Client (`frontend/internal/src/lib/api.ts`):**
  `apiFetch<T>` attaches `Authorization: Bearer <accessToken>` and `X-Tenant-Id: <activeTenantId>` headers. On HTTP 401 response, it clears `sessionStorage` and throws `ApiError(401, 'Your session has expired. Please sign in again.')`.

---

## 3. Refresh Token Architecture & Design Requirements

### 3.1 Domain Entity Design: `RefreshToken`
Create a new domain entity `backend/src/Domain/Entities/RefreshToken.cs`:

```csharp
using System;
using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>
/// Server-side persisted refresh token for silent auth renewal and session revocation.
/// </summary>
public class RefreshToken : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Cryptographically secure random string (base64/hex).</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Populated during token rotation to trace replacement history.</summary>
    public string? ReplacedByToken { get; set; }

    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}
```

### 3.2 Database Context & EF Core Migration
Update `backend/src/Infrastructure/Persistence/AppDbContext.cs`:
1. Add `DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();`
2. Configure entity mapping in `OnModelCreating`:

```csharp
builder.Entity<RefreshToken>(e =>
{
    e.Property(x => x.Token).IsRequired().HasMaxLength(256);
    e.HasIndex(x => x.Token).IsUnique();
    e.HasIndex(x => new { x.TenantId, x.UserId });

    e.HasOne(x => x.User)
     .WithMany()
     .HasForeignKey(x => x.UserId)
     .OnDelete(DeleteBehavior.Cascade);
});

builder.Entity<RefreshToken>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
```
3. Migration Command:
   Execute `dotnet ef migrations add AddRefreshTokenEntity --project backend/src/Infrastructure --startup-project backend/src/Api` to generate the migration file under `backend/src/Infrastructure/Migrations/`.

### 3.3 Application DTO & Service Contract Updates
1. **DTOs (`backend/src/Application/DTOs/`):**
   - Create `RefreshRequest.cs`:
     ```csharp
     namespace RecruitOps.Application.DTOs;
     public record RefreshRequest([Required] string RefreshToken);
     ```
   - Update `LoginResponse.cs`:
     ```csharp
     public record LoginResponse(
         string AccessToken,
         DateTimeOffset ExpiresAtUtc,
         string RefreshToken,
         DateTimeOffset RefreshTokenExpiresAtUtc,
         string Role,
         string DisplayName,
         Guid UserId,
         IReadOnlyCollection<string> Permissions);
     ```

2. **Interface `IAuthService` (`backend/src/Application/Interfaces/IAuthService.cs`):**
   ```csharp
   public interface IAuthService
   {
       Task<LoginResponse?> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default);
       Task<LoginResponse?> RefreshTokenAsync(RefreshRequest request, string? ipAddress = null, CancellationToken ct = default);
       Task<bool> RevokeTokenAsync(string refreshToken, string? ipAddress = null, CancellationToken ct = default);
   }
   ```

3. **Interface `ITokenService` (`backend/src/Application/Interfaces/ITokenService.cs`):**
   ```csharp
   public record TokenResult(
       string AccessToken,
       DateTimeOffset AccessTokenExpiresAtUtc,
       string RefreshToken,
       DateTimeOffset RefreshTokenExpiresAtUtc);

   public interface ITokenService
   {
       TokenResult CreateTokens(User user);
       string GenerateSecureRandomToken();
   }
   ```

### 3.4 Token Generation & Rotation Logic (`JwtTokenService.cs` & `AuthService.cs`)
- **Token Generation:** Use `RandomNumberGenerator.GetBytes(64)` encoded as Base64.
- **Refresh Token Lifetime:** 7 to 30 days (default: 14 days). Access Token lifetime can be set to 15-60 minutes (or retained at 8 hours).
- **Token Rotation & Reuse Protection in `RefreshTokenAsync`:**
  1. Retrieve `RefreshToken` entity using `.IgnoreQueryFilters().Include(r => r.User)` by token value.
  2. If token does not exist: Return `null` (401).
  3. **Reuse Detection Trigger:** If `token.IsRevoked == true`:
     - Potential stolen token reuse! Revoke all active refresh tokens belonging to `token.UserId` (`IsRevoked = true`, `RevokedAt = UtcNow`).
     - Return `null` (401).
  4. If `token.IsExpired == true`: Return `null` (401).
  5. If `token.User.IsActive == false`: Return `null` (401).
  6. **Rotate Token:**
     - Mark existing token as revoked: `token.IsRevoked = true`, `token.RevokedAt = DateTimeOffset.UtcNow`, `token.ReplacedByToken = newTokenString`.
     - Mint new JWT Access Token + new Refresh Token string.
     - Persist new `RefreshToken` entity in DB.
     - Fetch fresh permissions via `_permissions.GetUserPermissionsAsync(...)`.
     - Return updated `LoginResponse`.

### 3.5 Controller Endpoint: `POST /api/auth/refresh`
Update `backend/src/Api/Controllers/AuthController.cs`:

```csharp
[HttpPost("refresh")]
public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request, CancellationToken ct)
{
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
    var result = await _auth.RefreshTokenAsync(request, ipAddress, ct);
    if (result is null)
    {
        return Unauthorized(new ProblemDetails
        {
            Title = "Unauthorized",
            Detail = "Invalid, expired, or revoked refresh token."
        });
    }
    return Ok(result);
}

[HttpPost("revoke")]
[Authorize]
public async Task<IActionResult> Revoke([FromBody] RefreshRequest request, CancellationToken ct)
{
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
    await _auth.RevokeTokenAsync(request.RefreshToken, ipAddress, ct);
    return NoContent();
}
```

---

## 4. Frontend & Shared Package Implementation Plan

### 4.1 Shared Types Package (`packages/types/src/index.ts`)
Update `packages/types/src/index.ts`:

```typescript
export interface RefreshRequest {
  refreshToken: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc?: string;
  role: UserRole;
  displayName: string;
  userId: string;
  isSuperAdmin?: boolean;
  tenantId?: string;
  activeTenantId?: string;
  activeTenantName?: string;
  permissions: string[];
}
```

### 4.2 Frontend Session Storage & Auth Helper (`frontend/internal/src/lib/auth.ts`)
Update `Session` interface & `auth` methods:

```typescript
export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc?: string;
  role: UserRole;
  displayName: string;
  userId: string;
  isSuperAdmin?: boolean;
  activeTenantId?: string;
  activeTenantName?: string;
  permissions?: string[];
}

export const auth = {
  get(): Session | null {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
      const session = JSON.parse(raw) as Session;
      // Do NOT drop session if access token is expired IF a refresh token exists!
      // Silent refresh in apiFetch will handle renewing the access token.
      if (!session.refreshToken && new Date(session.expiresAtUtc).getTime() <= Date.now()) {
        sessionStorage.removeItem(STORAGE_KEY);
        return null;
      }
      return session;
    } catch {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
  },

  set(response: LoginResponse): Session {
    const current = auth.get();
    const session: Session = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      refreshToken: response.refreshToken,
      refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc,
      role: response.role,
      displayName: response.displayName,
      userId: response.userId,
      isSuperAdmin: response.isSuperAdmin || response.role === 'SuperAdmin',
      activeTenantId: response.activeTenantId ?? response.tenantId ?? current?.activeTenantId,
      activeTenantName: response.activeTenantName ?? current?.activeTenantName,
      permissions: response.permissions,
    };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    return session;
  },

  clear(): void {
    sessionStorage.removeItem(STORAGE_KEY);
  }
};
```

### 4.3 Silent Refresh Interceptor (`frontend/internal/src/lib/api.ts`)
Update `apiFetch` in `frontend/internal/src/lib/api.ts` with transparent 401 interceptor & request deduplication:

```typescript
let refreshPromise: Promise<LoginResponse | null> | null = null;

async function performSilentRefresh(refreshToken: string): Promise<LoginResponse | null> {
  if (!refreshPromise) {
    refreshPromise = fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
      .then(async (res) => {
        if (!res.ok) return null;
        const data = (await res.json()) as LoginResponse;
        auth.set(data);
        return data;
      })
      .catch(() => null)
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  let session = auth.get();

  let res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(session?.activeTenantId ? { 'X-Tenant-Id': session.activeTenantId } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (res.status === 401 && session?.refreshToken && path !== '/auth/refresh') {
    const refreshed = await performSilentRefresh(session.refreshToken);
    if (refreshed) {
      // Retry original request with new access token
      res = await fetch(`${BASE}${path}`, {
        ...init,
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${refreshed.accessToken}`,
          ...(refreshed.activeTenantId ?? session.activeTenantId
            ? { 'X-Tenant-Id': refreshed.activeTenantId ?? session.activeTenantId }
            : {}),
          ...(init?.headers ?? {}),
        },
      });
    } else {
      auth.clear();
      throw new ApiError(401, 'Your session has expired. Please sign in again.');
    }
  }

  if (res.status === 401) {
    auth.clear();
    throw new ApiError(401, 'Your session has expired. Please sign in again.');
  }

  if (!res.ok) {
    throw new ApiError(res.status, await readError(res));
  }

  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
```

---

## 5. Test Suite Mapping & Verification Framework

### 5.1 Current Test Suite Status
The existing test suite was executed and verified cleanly:
- **Backend Tests:** Total **269 tests passing** across 2 projects (`dotnet test backend/RecruitOps.sln`):
  - `RecruitOps.Domain.Tests.dll`: **51 passed**, 0 failed
  - `RecruitOps.Api.Tests.dll`: **218 passed**, 0 failed
- **Frontend Tests:** Total **189 tests passing** across 19 test files (`npm run test` in `frontend/internal`):
  - `src/lib/auth.test.ts` (11 tests)
  - `src/lib/ai.test.ts` (24 tests)
  - `src/features/...` and `src/components/...` (154 tests)
- **TypeScript Typecheck:** Clean with **0 errors** (`npm run typecheck`).

### 5.2 Required Test Coverage Additions for R3
Per R3 acceptance criteria, at least **5 new backend unit/integration tests** must be added to `backend/tests/RecruitOps.Api.Tests/AuthRefreshTokenTests.cs`:

| Test Name | Verification Objective | Expected Result |
|---|---|---|
| `Login_Returns_Valid_RefreshToken` | Assert `POST /api/auth/login` returns a non-empty `refreshToken` in response payload and persists it in DB. | HTTP 200 + RefreshToken populated |
| `RefreshToken_ValidToken_ReturnsNewTokenPair` | Call `POST /api/auth/refresh` with active token. | HTTP 200 + new `accessToken` & `refreshToken` pair |
| `RefreshToken_ExpiredToken_Returns401` | Call `POST /api/auth/refresh` with an expired token. | HTTP 401 Unauthorized |
| `RefreshToken_RevokedToken_Returns401` | Call `POST /api/auth/refresh` with a revoked token. | HTTP 401 Unauthorized |
| `RefreshToken_ReuseDetection_RevokesAllUserTokens` | Present a previously rotated/revoked token. Assert all active refresh tokens for the user are automatically revoked. | HTTP 401 Unauthorized + active tokens revoked |
| `RevokeToken_ExplicitLogout_RevokesToken` | Call `POST /api/auth/revoke`. Assert token status in DB becomes `IsRevoked = true`. | HTTP 204 No Content |

On the frontend, update `frontend/internal/src/lib/auth.test.ts` to add test cases for storing, retrieving, and clearing refresh tokens, as well as handling silent refresh flow.

---

## 6. Verification Steps for Implementation Phase

1. **Backend Verification:**
   - Execute `dotnet build backend/src/Api` (Ensure clean compilation).
   - Run `dotnet test backend/RecruitOps.sln` (Verify all 269 existing tests + new refresh token tests pass).
2. **Frontend Verification:**
   - Run `npm run typecheck` (Ensure 0 TypeScript errors across monorepo workspaces).
   - Run `npm run test` in `frontend/internal` (Verify all 189 existing tests + new frontend auth tests pass).
3. **Docker Verification:**
   - Run `docker compose up --build` to confirm containers start up cleanly with database migrations applied.
