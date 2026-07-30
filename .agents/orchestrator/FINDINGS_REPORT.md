# RecruitOps Comprehensive Audit & End-to-End Verification Findings Report

**Project**: RecruitOps In-House Recruitment SaaS Platform (Modules 1–3)  
**Date**: 2026-07-29  
**Auditor**: Project Orchestrator  
**Status**: COMPLETE  

---

## Executive Summary

A comprehensive audit and end-to-end verification of the RecruitOps SaaS platform (Modules 1–3) was conducted across backend API controllers, business logic handlers, database query filters, authorization matrix policies, frontend SPA/SSR workflows, test assertion quality, and end-to-end integration flows.

### Overall Assessment Matrix
- **Core Architecture & Design**: **EXCELLENT**. Clean Architecture (.NET 10), explicit domain boundary checks, EF Core global query filters for multi-tenancy isolation (`ITenantScoped`), and strict RBAC enforcement (ADR-0003, ADR-0017, ADR-0018, ADR-0019).
- **Test Suite Execution**: **100% PASSING**.
  - Backend: **172 / 172 Passed** (39 Domain tests + 130 API tests + 3 new E2E Multi-Module tests).
  - Frontend: **27 / 27 Passed** across `@recruitops/internal` Vitest suite.
  - TypeScript: **0 errors** reported by `tsc --noEmit` across `@recruitops/internal` and `@recruitops/public`.
- **Known UI Gaps**: All 3 un-eyeballed UI gaps from `NEXT-SESSION.md` (panel picker role filtering, blind state enforcement, `.mention` Tailwind CSS build survival) are **VERIFIED & WORKING AS DESIGNED**.

---

## Severity-Categorized Audit Findings

### 🔴 Critical Severity (Must Fix Before Production Deployment)

#### 1. `GET /api/users` EF Core LINQ Projection SQL Translation Bug
- **Location**: `backend/src/Api/Controllers/UsersController.cs` (Line 50)
- **Description**: The endpoint projects `u.Role.ToString()` directly inside the EF LINQ query:
  ```csharp
  .Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
  ```
- **Impact**: In EF Core 10 with Npgsql (PostgreSQL), `Enum.ToString()` inside SQL projection expressions cannot be translated into SQL. Calling `GET /api/users` against a real PostgreSQL database throws an `InvalidOperationException`.
- **Note**: The test suite passed because `CustomWebAppFactory` uses EF Core `InMemory` provider, which evaluates C# LINQ in memory.
- **Remediation**: Refactor to load an anonymous object with `u.Role` in SQL, then map to `UserListItemDto` with `Role.ToString()` in memory (same two-step pattern used in `Selectable` lines 85–93):
  ```csharp
  var rows = await _db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.DisplayName).Select(u => new { u.Id, u.Email, u.DisplayName, u.Role }).ToListAsync(ct);
  var users = rows.Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString())).ToList();
  ```

---

### 🟡 Important Severity (Logic Gaps, Security & Operational Risks)

#### 2. Deceptive Assertion in `AuthLoginTests.cs`
- **Location**: `backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs` (Lines 50–60)
- **Description**: Test method `Issued_Token_Grants_Access_To_Protected_Endpoint()` asserts `AccessToken` string presence on login response, but **never invokes any protected endpoint** using the bearer token.
- **Impact**: If JWT middleware or claim validation were broken in `Program.cs`, this test would still pass.
- **Remediation**: Update test to send an authenticated `GET /api/departments` request using `Authorization: Bearer <AccessToken>` and assert HTTP 200 OK.

#### 3. Loose HTTP Status Assertions in Integration Tests
- **Location**: `ScorecardBlindScoringTests.cs:238`, `InterviewFlowTests.cs:144`, `ScorecardTemplateResolutionTests.cs:108`
- **Description**: Tests assert `Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)`.
- **Impact**: Conceals whether model validation (400) or domain business rules (409) triggered.
- **Remediation**: Replace loose status checks with explicit expected HTTP status codes.

#### 4. In-Process Login Lockout Storage (`LoginThrottle.cs`)
- **Location**: `backend/src/Infrastructure/Services/LoginThrottle.cs`
- **Description**: Holds login failure counters in an in-memory `ConcurrentDictionary`.
- **Impact**: Running multiple API replicas behind a load balancer bypasses lockout limits (effective limit = N × limit).
- **Remediation**: Migrate `ILoginThrottle` backing store to Redis or PostgreSQL table.

#### 5. Untested Backend Services & Stub Controllers
- **Location**: `JdTemplatesController.cs`, `ApprovalChainService.cs` (`GET /api/approvalchains`), `CandidatesController.cs`, `JobsController.cs`, `PortalController.cs`
- **Description**: `JdTemplatesController` and `GET /api/approvalchains` have 0% test coverage. `CandidatesController`, `JobsController`, and `PortalController` are stub controllers returning empty arrays (`TODO`).
- **Remediation**: Add unit/integration tests for JD templates and approval chain queries. Remove deprecated stubs (`JobsController`, `PortalController`) or implement `CandidatesController`.

---

### 🟢 Minor Severity (Performance, Style & Code Maintenance)

#### 6. N+1 Query Bottleneck in Mention Resolution & Interview Mapping
- **Location**: `Infrastructure/Services/NoteService.cs:145-153`, `InterviewService.cs:144-149`
- **Description**: `ResolveMentionsAsync` executes N+1 DB queries per matched user handle. `ListForApplicationAsync` calls `MapAsync` sequentially in a `foreach` loop.
- **Remediation**: Batch load user department access and panel participation into HashSets before mapping.

#### 7. Massive Frontend Test Suite Gap (80%+ Untested)
- **Location**: `frontend/internal` and `frontend/public`
- **Description**: Only 3 component test files exist (`scorecard.test.ts`, `ApplicationNotes.test.tsx`, `InterviewDetailPage.test.tsx`). 11 of 14 pages and the Next.js public site have 0% test coverage.
- **Remediation**: Add React Testing Library component tests for key pages (`LoginPage`, `RequisitionFormPage`, `ApplicationForm.tsx`).

#### 8. Compiler & Framework Warnings
- **Warnings**:
  - CS8604 in `ApplicationFormSchema.cs(102,27)`: Possible null reference argument.
  - ASPDEPR005 in `Program.cs(147,9)`: `KnownNetworks` obsolete (use `KnownIPNetworks`).
  - NU1903 package vulnerability: `System.Security.Cryptography.Xml` 10.0.6.

---

## Status of Known Gaps (from `FEATURE-STATUS.md`)

| Gap ID | Feature / Component | Description | Audit Status | Verification Finding |
|---|---|---|---|---|
| **GAP-01** | User Directory API | `GET /api/users` `enum.ToString()` Postgres translation error | 🔴 **CONFIRMED BUG** | Fails in SQL projection. Needs two-step in-memory projection refactor. |
| **GAP-02** | Selectable Users Endpoint | `GET /api/users/selectable` RBAC policy check | 🟢 **FIXED / VERIFIED** | Class policy updated to bare `[Authorize]`, action requires `RecruitmentStaff`. Recruiter returns 200 OK. |
| **GAP-03** | Panel Picker Population | Recruiter vs Manager/Approver role visibility | 🟢 **FIXED / VERIFIED** | `ApplicationDebrief.tsx` calls endpoint only when `isRecruitmentStaff(role)` is `true`. Emails stripped per ADR-0019. |
| **GAP-04** | Blind State Enforcement | Scorecard visibility before interviewer submission | 🟢 **FIXED / VERIFIED** | `InterviewDetailPage.tsx` hides other scorecards until user submits. Verified in unit, frontend, and E2E tests. |
| **GAP-05** | `.mention` CSS Purging | Tailwind build CSS purge survival | 🟢 **FIXED / VERIFIED** | `.mention` rule in `index.css` is root CSS outside `@layer`, surviving Tailwind purge cleanly. |
| **GAP-06** | E2E Journey Coverage | Connected flow from requisition to scorecard | 🟢 **COMPLETED** | `FullUserJourneyIntegrationTests.cs` added (3 tests, 9 steps, 100% pass). |

---

## Actionable Pre-Production Checklist

1. [ ] **Apply `GET /api/users` fix**: Update `UsersController.cs:50` to project `UserListItemDto` in memory after fetching anonymous SQL result.
2. [ ] **Update `AuthLoginTests.cs`**: Add protected endpoint HTTP GET call with bearer token to prevent false positive test pass.
3. [ ] **Upgrade `System.Security.Cryptography.Xml`**: Resolve NU1903 package security warning.
4. [ ] **Replace In-Memory `LoginThrottle`**: Migrate lockout state to Redis before scaling horizontally.
5. [ ] **Expand Frontend Tests**: Add Vitest RTL test cases for `RequisitionFormPage` and `ApplicationForm.tsx`.
