## 2026-08-07T13:46:39Z
You are teamwork_preview_worker for Milestone 3 (Refresh Token Mechanism R3) of RecruitOps.
Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m3_1

Please complete the implementation of Milestone 3 according to the design specification:
1. Read the following specification files carefully before making any changes:
   - Original Request: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md
   - R3 Technical Survey & Blueprint: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_3\survey_r3.md

2. MANDATORY INTEGRITY WARNING:
   DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

3. Implementation Scope:
   - Backend Domain Entity: `backend/src/Domain/Entities/RefreshToken.cs` (inheriting BaseEntity, ITenantScoped).
   - EF Core AppDbContext Mapping & Query Filter: `backend/src/Infrastructure/Persistence/AppDbContext.cs`.
   - Migration: Generate EF Core migration for `AddRefreshTokenEntity` under `backend/src/Infrastructure/Migrations/`.
   - DTOs & Interfaces: `RefreshRequest.cs`, updated `LoginResponse.cs`, `IAuthService` (`RefreshTokenAsync`, `RevokeTokenAsync`), `ITokenService` (`TokenResult`, `GenerateSecureRandomToken`).
   - Token Generation & Rotation: Implement cryptographic random token generation in `JwtTokenService.cs`. Implement token rotation and reuse detection in `AuthService.cs` (if a revoked token is used, revoke all active tokens for that user!). Implement `RevokeTokenAsync`.
   - API Controller: `POST /api/auth/refresh` and `POST /api/auth/revoke` in `AuthController.cs`.
   - Monorepo Shared Types: Update `packages/types/src/index.ts` with `RefreshRequest` and updated `LoginResponse`.
   - Frontend Auth & API Client: Update `frontend/internal/src/lib/auth.ts` (session storage with refresh token, updated `auth.get()`, `auth.set()`, `auth.clear()`) and `frontend/internal/src/lib/api.ts` (`apiFetch` silent refresh interceptor on 401 with request deduplication and retry).
   - Test Suite: Add 5+ backend tests in `backend/tests/RecruitOps.Api.Tests/` covering valid refresh, expired refresh, revoked refresh, token reuse detection, login token pair generation, and explicit revocation. Update frontend tests in `frontend/internal/src/lib/auth.test.ts`.

4. Verification Requirements:
   - Execute `dotnet test backend/RecruitOps.sln` and ensure all backend tests pass (including existing 269 tests + new M3 tests).
   - Execute `npm run typecheck` in project root and ensure 0 TypeScript errors.
   - Execute `npm run test` in `frontend/internal` and ensure all frontend tests pass.

5. Deliverable:
   - Write a comprehensive handoff report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m3_1\handoff.md` detailing changes made, files modified, build/test execution results, and verification commands. Send a message to parent when complete.
