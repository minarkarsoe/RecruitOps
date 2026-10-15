# Test Assertion Quality and Code Coverage Audit Report

**Project:** RecruitOps  
**Auditor:** Explorer M1 (`teamwork_preview_explorer_m1_1`)  
**Date:** 2026-07-29  
**Target Scope:** Backend tests (`backend/tests/**/*.cs`), Frontend tests (`frontend/**/*.test.*`), and Implementation Modules 1–3.

---

## 1. Executive Summary

This audit evaluated test quality, assertion validity, false positive risks, and code coverage across the RecruitOps backend and frontend repositories. 

Key Findings:
1. **Backend Integration Suite**: 18 API test classes and 3 Domain test classes provide good scenario coverage for core happy paths and security policy boundaries (e.g. department isolation, approver reach, blind scorecard scoring). However, several test assertion defects and false positives exist.
2. **Deceptive & Trivial Assertions**: 
   - A test named `Issued_Token_Grants_Access_To_Protected_Endpoint()` in `AuthLoginTests.cs` never makes a request to a protected endpoint (it only asserts that `/api/auth/login` returns a token).
   - Multiple tests use loose status assertions (`Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)`), obscuring validation failure semantics.
3. **EF Core Provider False Positives**:
   - `UsersController.cs` projects `u.Role.ToString()` inside an EF Core LINQ query on `GET /api/users`. While this passes when running against the EF Core `InMemory` test provider, EF Core 10 on PostgreSQL (`Npgsql`) throws a runtime exception (`Expression tree cannot be translated`).
4. **Massive Frontend Test Gaps**:
   - Out of 14 frontend pages and 6 major UI components, only **3 test files exist** (`ApplicationNotes.test.tsx`, `scorecard.test.ts`, `InterviewDetailPage.test.tsx`).
   - 11 out of 14 pages (including `LoginPage`, `DepartmentsPage`, `ApprovalChainsPage`, `RequisitionsPage`, `JobPostingsPage`, `ScorecardTemplatesPage`, and the Next.js public job application site) have **0% test coverage**.
5. **Untested Backend Modules & Stub Controllers**:
   - `JdTemplatesController` & `JdTemplateService`: **0% test coverage**.
   - `ApprovalChainService.GetChainsAsync` (`GET /api/approvalchains`) and `GetByIdAsync`: **0% test coverage**.
   - `CandidatesController`, `JobsController`, `PortalController` are unimplemented stub controllers returning empty arrays with `TODO` comments.

---

## 2. Test Assertion Quality Evaluation

### 2.1 Misleading & Trivial Assertions

#### Issue 1: Unverified Assertion in `Issued_Token_Grants_Access_To_Protected_Endpoint`
* **File:** `backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs` (Lines 50–60)
* **Code snippet:**
  ```csharp
  [Fact]
  public async Task Issued_Token_Grants_Access_To_Protected_Endpoint()
  {
      var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
          new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
      var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
      Assert.NotNull(body);
      Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
  }
  ```
* **Defect:** The test claims to verify that an issued token grants access to a protected endpoint, but it **never invokes any protected endpoint** with the bearer token! It only asserts that the token string is not null or whitespace. This creates false confidence regarding JWT authorization middleware execution.

#### Issue 2: Ambiguous / Loose Status Code Assertions
* **Files:**
  - `backend/tests/RecruitOps.Api.Tests/ScorecardBlindScoringTests.cs` (Line 238): `Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);`
  - `backend/tests/RecruitOps.Api.Tests/InterviewFlowTests.cs` (Line 144): `Assert.True(noPanel.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);`
  - `backend/tests/RecruitOps.Api.Tests/ScorecardTemplateResolutionTests.cs` (Line 108): `Assert.True(empty.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);`
* **Defect:** Allowing either `BadRequest` (400) or `Conflict` (409) conceals whether ASP.NET Core DTO model validation or service-layer domain rule validation is triggering. Tests should enforce the exact HTTP response status contract.

### 2.2 Tests Passing for Wrong Reasons & In-Memory Provider Artifacts

#### Issue 1: In-Memory Database Masks SQL Translation Failure
* **File:** `backend/src/Api/Controllers/UsersController.cs` (Line 50)
* **Source Code:**
  ```csharp
  var users = await _db.Users
      .AsNoTracking()
      .Where(u => u.IsActive)
      .OrderBy(u => u.DisplayName)
      .Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
      .ToListAsync(ct);
  ```
* **Defect:** In EF Core 10, projecting `Enum.ToString()` inside a LINQ query cannot be translated to SQL by the `Npgsql` PostgreSQL provider. `UserDirectoryTests.cs` passes because `CustomWebAppFactory` replaces PostgreSQL with the EF Core `InMemory` database provider, which evaluates C# expressions in memory. In a real PostgreSQL environment, calling `GET /api/users` throws a `InvalidOperationException`.

#### Issue 2: Bypassing Production Authentication Scheme
* **File:** `backend/tests/RecruitOps.Api.Tests/TestAuthHandler.cs` (Lines 26–43)
* **Defect:** Almost all API integration tests authenticate via custom request headers (`X-Test-Tenant`, `X-Test-Roles`, `X-Test-UserId`) processed by `TestAuthHandler`. While this simplifies testing role and tenant scoping, it bypasses ASP.NET Core JWT Bearer authentication handler validation, token parsing, and claim mapping.

### 2.3 Unasserted Error / Exception Paths

#### Issue 1: Unchecked Validation Error Messages in Schema Unit Tests
* **File:** `backend/tests/RecruitOps.Domain.Tests/ApplicationFormSchemaTests.cs` (Lines 210–218)
* **Code:**
  ```csharp
  [Fact]
  public void An_Overlong_Text_Answer_Is_Refused()
  {
      var schema = """[{ "key": "note", "label": "Note", "type": "text" }]""";
      var answers = JsonSerializer.Serialize(new Dictionary<string, string>
      {
          ["note"] = new string('x', 5_000),
      });

      Assert.False(ApplicationFormSchema.TryValidateAnswers(schema, answers, out _, out _));
  }
  ```
* **Defect:** The test asserts `Assert.False(...)` but ignores the `out var error` output parameter. If validation fails due to JSON deserialization error or null reference rather than length restriction, the test still passes.

#### Issue 2: Missing Data Assertion on Public Salary Range
* **File:** `backend/tests/RecruitOps.Api.Tests/PublicApplicationTests.cs` (Lines 90–104)
* **Defect:** `Salary_Is_Hidden_Unless_The_Posting_Opted_In` only checks `Assert.NotNull(shownJob.SalaryRange)`. It does not assert that `SalaryMin` and `SalaryMax` match the expected values or formatting.

---

## 3. Untested Backend Code Paths Across Modules 1–3

| Module | Component / Service | Method / Path | Status | Risk Level |
|---|---|---|---|---|
| **Module 1.2** | `JdTemplatesController` & `JdTemplateService` | `GET /api/jdtemplates`, `POST /api/jdtemplates` | **0 Tests (Completely Untested)** | **High** |
| **Module 1.3** | `ApprovalChainService` & `ApprovalChainsController` | `GET /api/approvalchains`, `GET /api/approvalchains/{id}` | **0 Tests (Completely Untested)** | **Medium** |
| **Module 1.1** | `DepartmentService` | `SetActiveAsync(id, isActive=true)` when already active (throwing `InvalidOperationException`) | **Untested Error Path** | **Low** |
| **Module 1.4** | `UsersController` | `GET /api/users` SQL projection against PostgreSQL | **Fails at runtime (EF In-Memory mask)** | **High** |
| **Module 2.1** | `JobsController` | `GET /api/jobs` | Stub (`TODO`), return empty array | **High** |
| **Module 2.1** | `PortalController` | `GET /api/portal` | Stub (`TODO`), return empty array | **High** |
| **Module 2.7** | `CandidatesController` | `GET /api/candidates` | Stub (`TODO`), return empty array | **High** |
| **Module 3.1** | `InterviewService` | `RescheduleAsync` with invalid start time in past | **Untested Validation Path** | **Medium** |
| **Module 3.3** | `ScorecardService` | Out-of-range rating inputs in `SaveMineAsync` | **Untested Boundary Path** | **Medium** |

---

## 4. Untested Frontend Code Paths Across Modules 1–3

The frontend test suite contains only **3 test files**:
1. `frontend/internal/src/components/ApplicationNotes.test.tsx`
2. `frontend/internal/src/lib/scorecard.test.ts`
3. `frontend/internal/src/pages/InterviewDetailPage.test.tsx`

### 4.1 Completely Untested Frontend Pages & Components (0% Coverage)

#### Module 1 Frontend
- `LoginPage.tsx` & `lib/auth.ts`: Auth form submission, error handling, token persistence in `localStorage`, role extraction.
- `DepartmentsPage.tsx`: Department list rendering, creation modal, renaming, activation toggle, member management UI.
- `ApprovalChainsPage.tsx`: Approval chain listing, multi-step builder UI.
- `JdTemplatesPage.tsx`: Template list rendering, template creation form.
- `RequisitionsPage.tsx`: Requisition list filters, department scoping badge display.
- `RequisitionFormPage.tsx`: Form input validation, department dropdown loading, submission handling.
- `RequisitionDetailPage.tsx`: Timeline rendering, approval/rejection decision buttons, cancel action, status tags.
- `InboxPage.tsx`: Inbox pending requisition item rendering, empty state.

#### Module 2 Frontend
- `JobPostingsPage.tsx`: Job postings table, status filters.
- `JobPostingDetailPage.tsx`: Posting configuration, publishing toggle, candidate Kanban pipeline board, candidate stage dragging/moving.
- `FormFieldBuilder.tsx`: Dynamic form builder UI, field key/label/type addition, validation error display.
- Next.js Public App (`frontend/public/app/jobs/[token]/page.tsx` & `ApplicationForm.tsx`): Public job page rendering, custom dynamic field rendering, application submission form, success state.

#### Module 3 Frontend
- `ScorecardTemplatesPage.tsx`: Template resolution scope selector (Company vs Department vs Job Posting), criteria ordering UI.
- `ApplicationDebrief.tsx`: Blind scoring summary cards, decision highlights.

#### Infrastructure & Shared Utilities
- `AppLayout.tsx`: User profile dropdown, navigation tabs by role, tenant context switcher.
- `RequireAuth.tsx`: Route guard redirecting unauthenticated users to `/login`.
- `lib/api.ts`: Core HTTP client fetch wrapper, status code handling, JSON parsing, error throwing.

---

## 5. Summary Table: Test Coverage Matrix

| Area | Module | Backend Test File | Frontend Test File | Coverage Status | Key Defect / Gap |
|---|---|---|---|---|---|
| **Auth & Login** | 1.1 | `AuthLoginTests.cs`, `LoginThrottleTests.cs`, `JwtTokenServiceTests.cs` | None | Partial (Backend ok, Frontend 0%) | `Issued_Token_Grants_Access...` does not call protected endpoint |
| **Departments** | 1.1 | `DepartmentAdminTests.cs`, `DepartmentIsolationTests.cs` | None | Partial (Backend ok, Frontend 0%) | Frontend `DepartmentsPage.tsx` untested |
| **JD Templates** | 1.2 | None | None | **0% (Backend & Frontend)** | `JdTemplatesController` & `JdTemplateService` completely untested |
| **Requisitions** | 1.3 | `RequisitionApprovalFlowTests.cs`, `RequisitionScopingTests.cs` | None | Partial (Backend ok, Frontend 0%) | Requisition pages and inbox UI untested |
| **Approval Chains**| 1.3 | `RequisitionApprovalFlowTests.cs` (Partial) | None | Partial | `GET /api/approvalchains` endpoints untested |
| **Users Directory**| 1.4 | `UserDirectoryTests.cs` | None | Partial | `GET /api/users` crashes on Postgres (`Enum.ToString()` in LINQ) |
| **Job Postings** | 2.1 | `JobPostingFlowTests.cs` | None | Partial | Frontend pipeline board untested |
| **Public Apply** | 2.1/2.2| `PublicApplicationTests.cs`, `ApplicationFormSchemaTests.cs` | None | Partial | Next.js public application form untested |
| **Candidates** | 2.7 | None | None | **0%** | `CandidatesController` is empty stub |
| **Interviews** | 3.1 | `InterviewFlowTests.cs` | `InterviewDetailPage.test.tsx` | High | Good scenario coverage |
| **Scorecards** | 3.3 | `ScorecardBlindScoringTests.cs`, `ScorecardTemplateResolutionTests.cs` | `scorecard.test.ts` | High | Loose status assertions (400 vs 409) |
| **Notes & Mentions**| 3.4 | `ApplicationNoteTests.cs`, `MentionParserTests.cs` | `ApplicationNotes.test.tsx` | High | Frontend test lacks user interaction testing |

---

## 6. Recommendations for Test Suite Remediation

1. **Fix Misleading Assertions**:
   - Update `AuthLoginTests.cs`: Add an actual call to a protected endpoint (e.g. `GET /api/departments`) using the returned `AccessToken` in `Issued_Token_Grants_Access_To_Protected_Endpoint()`.
   - Replace loose status assertions (`is HttpStatusCode.BadRequest or HttpStatusCode.Conflict`) with exact expected HTTP status codes.
2. **Fix PostgreSQL LINQ Incompatibility**:
   - Refactor `UsersController.cs` line 50 to project `Enum.ToString()` in memory after materialization (matching `Selectable` method), preventing runtime crashes on PostgreSQL.
3. **Add Tests for Missing Backend Services**:
   - Add `JdTemplateTests.cs` testing `GET /api/jdtemplates` and `POST /api/jdtemplates`.
   - Add tests for `GET /api/approvalchains` and `GET /api/approvalchains/{id}` in `RequisitionApprovalFlowTests.cs`.
4. **Expand Frontend Unit & Component Test Suite**:
   - Implement component/page test files using React Testing Library & Vitest for `LoginPage`, `DepartmentsPage`, `RequisitionFormPage`, `RequisitionDetailPage`, `JobPostingDetailPage`, and `ApplicationForm.tsx`.
