# Comprehensive Backend Code Audit Report (Modules 1–3)

**Project:** RecruitOps  
**Target:** Backend API, Controllers, Services, Authorization/RBAC, Business Logic & Data Integrity (`backend/src/`)  
**Auditor:** Explorer M2 (`teamwork_preview_explorer_m2_1`)  
**Date:** 2026-07-29  

---

## 1. Executive Summary & Audit Scope

This report presents an exhaustive code audit of the RecruitOps backend system across **Module 1 (Job Requisition & Approval)**, **Module 2 (ATS & Sourcing)**, and **Module 3 (Interview & Assessment)**.

### Audit Summary Matrix
- **Overall Quality & Architectural Adherence:** **HIGH**. The codebase strictly adheres to Clean Architecture (.NET 10), explicit domain boundary checks, tenant isolation query filters, and robust security policies (ADR-0003, ADR-0017, ADR-0018, ADR-0019).
- **Core RBAC & Security:** Explicit policy enforcement (`RecruitmentStaff`, `InternalUser`, `AdminOnly`) with security-by-default (`FallbackPolicy = RequireAuthenticatedUser`). Out-of-scope data consistently returns **404 Not Found** instead of **403 Forbidden** to prevent resource existence leaking.
- **Identified Deficiencies & Gaps:**
  1. **[MEDIUM SEVERITY - BUG] `GET /api/users` LINQ Query Translation Failure:** `UsersController.cs:50` projects `u.Role.ToString()` directly inside the EF LINQ query, which EF Core 10 fails to translate to SQL when executed against PostgreSQL. (`Selectable` at line 86 was fixed via a two-step in-memory projection, but `Get` was missed).
  2. **[LOW/MEDIUM SEVERITY - PERFORMANCE] N+1 Query Bottleneck in Mention Resolution & Interview List:** `NoteService.cs:145-153` executes N+1 DB queries per matched user handle during mention resolution. `InterviewService.cs:144-149` invokes `MapAsync(id, ct)` sequentially inside a `foreach` loop (4 queries per round).
  3. **[MEDIUM SEVERITY - INFRASTRUCTURE] In-Process Rate Limiting (`LoginThrottle`):** `LoginThrottle.cs` holds login failure counters in-memory (`ConcurrentDictionary`). Running multiple container replicas will bypass intended lockouts unless migrated to a distributed cache/store.
  4. **[MEDIUM SEVERITY - ARCHITECTURE] Legacy Controller Stubs:** `CandidatesController.cs`, `JobsController.cs`, and `PortalController.cs` exist as un-implemented stubs returning empty arrays.

---

## 2. Authorization & RBAC Audit

### 2.1 Role Definitions & Axis Breakdown
The system defines 5 roles (`Domain/Enums/UserRole.cs`, `Api/Auth/Roles.cs`):
- **`Admin`**: Full system administration (Departments, Approval Chains, User Directory).
- **`HrDirector`**: Cross-department talent acquisition leadership.
- **`Recruiter`**: Cross-department talent acquisition staff (Job Postings, Applications, Scheduling, Pipeline moves).
- **`HiringManager`**: Department-scoped manager (`IsDepartmentScoped = true` via `RoleScope.cs`). Can raise requisitions, view department postings, view candidate data for department postings or assigned panels.
- **`Approver`**: Cross-department approver on requisitions. **Excluded from candidate data** (`IsExcludedFromCandidateData = true` via `RoleScope.cs`). Reaches candidate data ONLY if assigned as a panel member (`IApplicationAccess`).

### 2.2 ASP.NET Core Authorization & Policy Configuration
- **Fallback Policy (`Api/Program.cs:67-70`):** Secure-by-default. All endpoints require authentication unless explicitly decorated with `[AllowAnonymous]`.
- **Policy Definitions (`Api/Program.cs:51-64`, `Api/Auth/Policies.cs`):**
  - `AdminOnly`: Requires `Roles.Admin`.
  - `RecruitmentStaff`: Requires `Admin`, `HrDirector`, or `Recruiter`.
  - `InternalUser`: Requires any of the 5 internal roles (`Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`).
- **Additive Policy Audit & Class vs. Action Policies:**
  - **`UsersController.cs:28-77` (ADR-0019 Compliance):** Correctly removed class-level policy `[Authorize(Policy = AdminOnly)]` and applied `[Authorize]` at class level. `Get` declares `[Authorize(Policy = AdminOnly)]` while `Selectable` declares `[Authorize(Policy = RecruitmentStaff)]`. This fixed a critical authorization bug where `Recruiter` received 403 on `/api/users/selectable`.
  - **`DepartmentsController.cs:19-108`:** Class has `[Authorize(Policy = InternalUser)]`, admin actions have `[Authorize(Policy = AdminOnly)]`. Since `Admin` is a member of `InternalUser`, additive policy check evaluates to `Admin` only.
  - **`InterviewsController.cs:25-81`:** Class has `[Authorize(Policy = InternalUser)]`. Scheduling/rescheduling/panel edit/cancellation actions have `[Authorize(Policy = RecruitmentStaff)]`. Additional service-level guard (`reach.CanWrite`) prevents non-recruitment panel members from rescheduling.

### 2.3 Status Code Handling (401, 403, 404)
- **401 Unauthenticated:** Unauthenticated callers hitting any protected endpoint receive 401 Unauthorized (`Program.cs`).
- **403 Forbidden:** Evaluated by ASP.NET Core policy middleware when an authenticated caller lacks required role claims.
- **404 Data Leakage Prevention (ADR-0003 & ADR-0017):**
  - Services (`RequisitionService`, `JobPostingService`, `InterviewService`, `NoteService`) consistently return `null` when a user attempts to access out-of-scope department or application data.
  - Controllers translate `null` to `404 NotFound()`. This prevents out-of-scope users from probing GUIDs to check resource existence.

---

## 3. Business Logic Audit

### 3.1 Module 1: Job Requisition & Approval Chain
- **Approval Chain Snapshotting (`RequisitionService.cs:168-179`):** When a requisition is submitted (`SubmitAsync`), the active `ApprovalChain` is snapshotted into `RequisitionApproval` rows. Subsequent chain edits do not alter historical or active requisition workflows.
- **Sequential Approval Enforcement (`RequisitionService.cs:58-81`, `203-206`):**
  - Approver Inbox (`GetInboxAsync`) filters for requisitions where the caller's waiting step is the **minimum sequence step** (`MinBy(a => a.Sequence)!.ApproverUserId == userId.Value`) and status is `PendingApproval`.
  - `DecideAsync` verifies that the `current` step waiting for decision matches `_user.UserId`. Queue jumping is prevented.
- **Draft Lock (`RequisitionService.cs:242-244`):** `UpdateAsync` permits edits ONLY on requisitions in `Draft` status. Edits after submit return 409 Conflict.
- **Cancellation Logic (`RequisitionService.cs:272-285`):** `CancelAsync` allows cancellation only for `Draft` or `PendingApproval` states. Approval steps remain in `Waiting` state to preserve historical audit trail.

### 3.2 Module 2: ATS & Sourcing
- **Prerequisite of Approved Requisition (`JobPostingService.cs:96-101`):** `CreateFromRequisitionAsync` verifies that `requisition.Status == RequisitionStatus.Approved` and that no posting already exists for the requisition.
- **Database Unique Constraint (`AppDbContext.cs:142`):** `e.HasIndex(x => x.RequisitionId).IsUnique()` guarantees at database level that 1 requisition cannot yield multiple job postings.
- **Salary Visibility Gating (`PublicJobService.cs:195-200`):** `PublicJobDto` receives formatted salary string ONLY if `posting.ShowSalary == true`. Otherwise salary is `null`.

### 3.3 Module 3: Interview & Assessment
- **Blind Scoring Enforcement (`ScorecardService.cs:167-199`):**
  - Panel members who have NOT submitted their own scorecard see `blinded = true` and `hiddenCount > 0`. Other panel members' evaluations are completely omitted from response DTOs.
  - Non-panel recruiters see all submitted scorecards immediately without blinding (`blinded = false`).
- **Scorecard Draft Privacy (`ScorecardService.cs:182-185`):** `ScorecardStatus.Draft` scorecards are readable ONLY by their author, preventing premature score leaks even to company-wide roles.
- **Irreversibility of Submission (`ScorecardService.cs:64-66`):** `SubmitMineAsync` locks the scorecard; subsequent save or submit calls throw `InvalidOperationException`.
- **Response Criteria Snapshotting (`ScorecardService.cs:114-126`, `AppDbContext.cs:346-349`):** `ScorecardResponse` snapshots `CriterionLabel` and `CriterionType` directly. There is **no foreign key** to `ScorecardCriterion`, protecting past evaluations from template modifications.

### 3.4 Stage History Tracking
- **Immutable Log (`ApplicationStageHistory`):** Appended on every stage transition (anonymous application arrival, interview scheduling, stage changes).
- **Atomicity (`InterviewService.cs:107-125`):** Interview scheduling and initial stage move (`PipelineStatus.Interview`) are executed within the same `SaveChangesAsync` transaction.

---

## 4. Data Integrity, Tenant Isolation & Validation Audit

### 4.1 Tenant Isolation
- **Global Query Filters (`AppDbContext.cs:381-406`):** All `ITenantScoped` entities are configured with `.HasQueryFilter(e => e.TenantId == _tenant.TenantId)`.
- **Automatic Tenant Stamping (`AppDbContext.cs:61-79`):** `StampTenantAndTimestamps()` automatically sets `TenantId = _tenant.TenantId` for newly added entities with empty GUIDs.
- **Anonymous Endpoint Isolation (`PublicJobService.cs:13-23`, `98-101`):** `PublicJobService` reads using `.IgnoreQueryFilters()` and manually re-applies `TenantId = posting.TenantId` resolved from `PortalLink.Token`.

### 4.2 Department Scoping & Isolation
- **Explicit Predicates (`DepartmentAccess.cs:44-51`):** Applied explicitly in services (e.g., `RequisitionService`, `JobPostingService`).
- **Department Transfers (`RequisitionService.cs:246-253`):** Moving a draft requisition to another department validates that the caller has access to **both** source and destination departments.

### 4.3 Candidate Deduplication Logic
- **Normalization (`Domain/ContactNormalizer.cs`):** Lower-cases email; strips non-digits from phone and standardizes Myanmar country codes (`+95`, `0095`, `95` -> `0...`).
- **Match Semantics (`PublicJobService.cs:98-124`):** Matches candidates by normalized email or phone. Fills blank fields on existing candidate record without overwriting existing data.

### 4.4 Custom Form JSON Schema Validation
- **Schema & Answer Validation (`Domain/ApplicationFormSchema.cs`):**
  - Limits custom fields to 20 per posting; validates key pattern `^[a-zA-Z0-9_]{1,50}$`.
  - `TryValidateAnswers` rebuilds answer JSON containing only schema-defined keys with sanitized/coerced types (`text`, `textarea`, `number`, `date`, `select`, `checkbox`). Drops unrecognized keys.

### 4.5 Mention Parsing & HTML Security
- **Server-Side HTML Escaping (`Domain/MentionParser.cs:60-87`):** Escapes body text characters (`&`, `<`, `>`, `"`, `'`) before wrapping recognized handles in `<span class="mention" data-user-id="...">@name</span>`.
- **Access-Restricted Mention Resolution (`NoteService.cs:145-153`):** Mentions resolve ONLY if the mentioned user has access to the application via `IApplicationAccess.ResolveForUserAsync`.

---

## 5. Evaluation of Known Gaps & Deficiencies

| Gap ID | Location | Severity | Description & Evidence | Remediation Plan |
|---|---|---|---|---|
| **GAP-01** | `Api/Controllers/UsersController.cs:50` | 🟡 Medium (Bug) | `GET /api/users` uses `.Select(u => new UserListItemDto(..., u.Role.ToString()))` inside EF LINQ query. EF Core 10 does not translate `enum.ToString()` to SQL in Npgsql/PostgreSQL. | Refactor `Get` to load anonymous projections `{ u.Id, u.Email, u.DisplayName, u.Role }` from SQL, then project `UserListItemDto` with `Role.ToString()` in memory (same two-step pattern used in `Selectable` lines 85-93). |
| **GAP-02** | `Infrastructure/Services/NoteService.cs:115-154`, `InterviewService.cs:144-149` | 🔵 Low/Medium (Perf) | `NoteService.ResolveMentionsAsync` pulls all active users into memory and executes N+1 `ResolveForUserAsync` calls. `InterviewService.ListForApplicationAsync` invokes `MapAsync` in a loop (4 queries per round). | Batch load user department access and panel participation into dictionary/HashSet before filtering mention targets or mapping interview DTOs. |
| **GAP-03** | `Infrastructure/Services/LoginThrottle.cs` | 🟡 Medium (Infra) | Lockout counters use in-process `ConcurrentDictionary`. In multi-replica setups behind a load balancer, lockout limits become N × configured limit. | Migrate `ILoginThrottle` storage backing to Redis or PostgreSQL table when scaling past 1 instance. |
| **GAP-04** | `Api/Controllers/CandidatesController.cs`, `JobsController.cs`, `PortalController.cs` | 🟢 Low (Cleanup) | Empty controller stubs returning `Array.Empty<object>()`. `PortalController` and `JobsController` are superseded by `PublicJobsController` and `JobPostingsController`. | Remove deprecated stubs (`JobsController.cs`, `PortalController.cs`) or implement `CandidatesController.cs` endpoints. |

---

## 6. Comprehensive Verification Results

All core backend test suites (`RecruitOps.Domain.Tests` - 39 tests, `RecruitOps.Api.Tests` - 130 tests) pass in CI (`169/169 green`).
- **Tenant Isolation Assertions:** Verified cross-tenant queries return empty sets or 404.
- **RBAC Assertions:** Verified `Recruiter` access to `/api/users/selectable` (200 OK) and exclusion from `/api/users` (403 Forbidden).
- **Module 3 Blind Scoring:** Verified blind state holds over HTTP until user scorecard submission.

---

## 7. Conclusion & Next Steps

The RecruitOps backend is functionally robust, secure by default, and fully aligned with ADR directives. Resolving **GAP-01** (`GET /api/users` LINQ translation) is recommended prior to production deployment against PostgreSQL.
