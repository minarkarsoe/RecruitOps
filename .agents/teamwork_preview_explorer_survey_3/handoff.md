# Handoff Report — Survey R3: Refresh Token Mechanism

**Agent:** teamwork_preview_explorer (Survey R3)  
**Date:** 2026-08-07  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_3`  
**Report Output:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_3\survey_r3.md`

---

## 1. Observation

- **Backend Auth Architecture:**
  - `User` entity (`backend/src/Domain/Entities/User.cs`): inherits `BaseEntity` (`Guid Id`, `DateTimeOffset CreatedAt`, `DateTimeOffset? UpdatedAt`) and `ITenantScoped` (`Guid TenantId`).
  - `JwtTokenService` (`backend/src/Infrastructure/Services/JwtTokenService.cs`): mints 8-hour HS256 JWT access tokens with claims (`sub`, `tenant_id`, `role`, `email`, `name`, `is_super_admin`).
  - `AuthService` (`backend/src/Infrastructure/Services/AuthService.cs`): handles login, queries user via `.IgnoreQueryFilters()`, uses dummy PBKDF2 hashing (`DummyCredential`) for timing attack prevention.
  - `AuthController` (`backend/src/Api/Controllers/AuthController.cs`): exposes `POST /api/auth/login`, enforces two-axis rate limiting per ADR-0016 (per-IP 60 req/60s, per-account 5 failures → 15 min lockout).
  - `AppDbContext` (`backend/src/Infrastructure/Persistence/AppDbContext.cs`): uses EF Core, applies `StampTenantAndTimestamps()` and global tenant query filters.
- **Frontend Auth & Types Architecture:**
  - `auth.ts` (`frontend/internal/src/lib/auth.ts`): stores `Session` in `sessionStorage` (`accessToken`, `expiresAtUtc`, `role`, `displayName`, `userId`, `permissions`). Drops expired sessions on `auth.get()`.
  - `api.ts` (`frontend/internal/src/lib/api.ts`): `apiFetch<T>` attaches `Authorization` header and handles 401 by clearing session and throwing `ApiError`.
  - `@recruitops/types` (`packages/types/src/index.ts`): defines shared DTO contracts for `LoginRequest` and `LoginResponse`.
- **Test Executions Observed:**
  - `dotnet test backend/RecruitOps.sln`: **269 tests passing** (51 `RecruitOps.Domain.Tests` + 218 `RecruitOps.Api.Tests`).
  - `npm run test` in `frontend/internal`: **233 tests passing** (25 test files).

---

## 2. Logic Chain

1. **Current System Deficit:** Access tokens last 8 hours with no refresh path. When expired, API returns 401, forcing full re-login and dropping active user context.
2. **Refresh Token Mechanics:** Adding a server-side `RefreshToken` entity (persisted via EF Core, linked to `User` and `TenantId`) enables token revocation and rotation.
3. **Security Constraints:** To prevent replay attacks, token rotation (invalidating old refresh token upon issuance of a new pair) and reuse detection (revoking all user sessions if a revoked token is presented) must be enforced server-side.
4. **Frontend Integration:** `apiFetch` in `frontend/internal/src/lib/api.ts` requires a 401 interceptor that calls `POST /api/auth/refresh` transparently and retries the original request with the new access token before falling back to login redirect.
5. **Contract Uniformity:** Updating `@recruitops/types` ensures both frontend and backend maintain type safety for `refreshToken` and `refreshTokenExpiresAtUtc`.

---

## 3. Caveats

- **Rate Limiting on Refresh Endpoint:** While `POST /api/auth/login` uses two-axis rate limiting, `POST /api/auth/refresh` should be protected against burst attacks (e.g. standard rate limiting).
- **Session Cleanup:** Over time, revoked/expired `RefreshToken` rows will accumulate in Postgres. A background worker or periodic cleanup job can be added later for DB housekeeping.

---

## 4. Conclusion

The complete architectural blueprint, entity specifications, EF Core migration design, API endpoint definitions, token rotation/reuse detection logic, frontend silent refresh interceptor design, and test suite verification mapping have been detailed in `survey_r3.md`.

---

## 5. Verification Method

To verify the investigation and analysis:
1. Inspect the survey report at `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_3\survey_r3.md`.
2. Run backend test suite:
   ```cmd
   dotnet test backend/RecruitOps.sln
   ```
3. Run frontend test suite:
   ```cmd
   cd frontend/internal && npm run test
   ```
4. Verify monorepo typecheck:
   ```cmd
   npm run typecheck
   ```
