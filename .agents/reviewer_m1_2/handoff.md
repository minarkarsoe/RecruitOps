# Review and Handoff Report — Milestone 1 Security & Department Reach Scoping

**Reviewer**: `reviewer_m1_2` (teamwork_preview_reviewer)  
**Date**: 2026-08-11  
**Verdict**: **APPROVE**

---

## 1. Observation

### 1.1 Scoping Predicates in `SearchService.cs`
- **Location**: `backend/src/Infrastructure/Services/SearchService.cs` lines 135–229
- **Resolution mechanism**: `ResolveScopeContextAsync` reads `_user.IsExcludedFromCandidateData` and `_user.IsDepartmentScoped` from `ICurrentUser` (backed by `RoleScope.cs`).
- **Hiring Manager Scoping (ADR-0003)**:
  - `SearchCandidatesAsync` (line 203): When `scope.IsDepartmentScoped` is `true` (`HiringManager`), candidates are filtered to those having job applications in `scope.AllowedDepartmentIds` OR where the user is an active interview panel participant (`_db.InterviewParticipants`).
  - `SearchJobPostingsAsync` (line 330): Filters `postingQuery = postingQuery.Where(p => scope.AllowedDepartmentIds.Contains(p.DepartmentId))`.
  - `SearchRequisitionsAsync` (line 394): Filters `reqQuery = reqQuery.Where(r => scope.AllowedDepartmentIds.Contains(r.DepartmentId))`.
- **Approver Candidate Data Exclusion (ADR-0018)**:
  - `SearchCandidatesAsync` (line 178): When `scope.IsExcludedFromCandidateData` is `true` (`Approver`), the service queries `_db.InterviewParticipants` for application IDs assigned to the user.
  - If `participantAppIds` is empty, `SearchCandidatesAsync` immediately returns `new List<SearchResultItemDto>()` (0 matches).
  - If the Approver is on an interview panel for a specific candidate application, only that candidate is accessible.
  - `SearchJobPostingsAsync` and `SearchRequisitionsAsync` remain accessible company-wide for `Approver` as specified in ADR-0003/ADR-0018 so headcount approval routing functions properly.
- **Admin / HR Director / Recruiter Reach**:
  - Both `scope.IsExcludedFromCandidateData` and `scope.IsDepartmentScoped` evaluate to `false`.
  - Full company-wide reach across Candidates, Job Postings, and Requisitions is maintained.

### 1.2 Authorization Attributes on `SearchController.cs`
- **Location**: `backend/src/Api/Controllers/SearchController.cs` line 17
- **Attribute**: `[Authorize(Policy = Policies.InternalUser)]`
- **Enforcement**: Blocks unauthenticated requests with HTTP 401 Unauthorized (`Test1_Unauthenticated_Search_Returns_401`) and restricts access strictly to internal authenticated roles (`Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`).

### 1.3 Test Suite Execution Results
- Executed command: `dotnet test backend/RecruitOps.sln`
- **Results**:
  - `RecruitOps.Domain.Tests.dll`: **51 Passed, 0 Failed, 0 Skipped**
  - `RecruitOps.Api.Tests.dll`: **360 Passed, 0 Failed, 0 Skipped**
  - **Total**: **411 Passed, 0 Failed, 0 Skipped**

### 1.4 Integrity Audit
- No hardcoded test results, facade implementations, or bypass shortcuts were found in `SearchService.cs`, `SearchController.cs`, `RoleScope.cs`, or test fixtures.
- Search service executes authentic LINQ queries against EF Core DB context and performs real Zawgyi-to-Unicode normalization via `IMyanmarScriptNormalizer`.

---

## 2. Logic Chain

1. **ADR-0003 Department Reach Compliance**:
   - Observation: `SearchService.cs` limits `JobPosting` and `Requisition` queries using `AllowedDepartmentIds` for department-scoped users (`HiringManager`).
   - Inference: Hiring Managers cannot observe requisitions or job postings outside their assigned department.
   - Observation: Candidate search queries cross-reference job applications within `AllowedDepartmentIds` or interview panel participation.
   - Inference: Hiring Managers cannot reach candidate profiles or extracted CV text belonging to other departments unless assigned to an interview panel for that application.

2. **ADR-0018 Approver Exclusion Compliance**:
   - Observation: `RoleScope.IsExcludedFromCandidateData(UserRole.Approver)` returns `true`.
   - Observation: `SearchCandidatesAsync` returns 0 results when `IsExcludedFromCandidateData` is true and the user has no entries in `InterviewParticipants`.
   - Inference: Approvers cannot search or view candidate data company-wide, resolving the security risk identified in ADR-0018 while preserving requisition approval access.

3. **Controller & API Security**:
   - Observation: `SearchController.cs` is decorated with `[Authorize(Policy = Policies.InternalUser)]`.
   - Inference: Anonymous or external client requests to `/api/search` are rejected with HTTP 401.

4. **Verification & Test Suite**:
   - Observation: `dotnet test backend/RecruitOps.sln` executes 411 unit and integration tests cleanly with 0 failures.
   - Inference: Search implementation and security scoping work as specified without breaking existing domain or API contracts.

---

## 3. Caveats

- **Test Fixture Seeding Resilience**: In `Milestone1EmpiricalAccessControlAndBoundaryTests.cs` (line 292), `.First(p => p.DepartmentId == ...)` is called on seeded `JobPostings`. If test fixture execution order creates an edge condition where `SeedTestData()` early-exits without adding postings, `.First()` can throw a sequence empty exception. Replacing `.First(...)` with `.FirstOrDefault(...)` in the test file would improve test fixture idempotency (though actual production code in `SearchService.cs` uses safe `.Contains()` LINQ predicates and handles empty sequences gracefully).

---

## 4. Conclusion

**Verdict**: **APPROVE**

Milestone 1 Security & Department Reach Scoping (ADR-0003 & ADR-0018) is correctly implemented and verified:
1. Hiring Managers are restricted to candidate, requisition, and posting data within their permitted department scope (or interview panels).
2. Approvers have candidate data exclusion enforced, returning 0 candidate search matches unless listed on an interview panel.
3. `SearchController.cs` correctly requires `Policies.InternalUser`.
4. All 411 backend tests (`dotnet test backend/RecruitOps.sln`) pass cleanly.

---

## 5. Verification Method

To independently verify this review:
1. Run `dotnet test backend/RecruitOps.sln` to execute the full test suite.
2. Inspect `backend/src/Infrastructure/Services/SearchService.cs` (methods `SearchCandidatesAsync`, `SearchJobPostingsAsync`, `SearchRequisitionsAsync`) for scope filtering logic.
3. Inspect `backend/src/Domain/RoleScope.cs` for `IsDepartmentScoped` and `IsExcludedFromCandidateData` predicates.
4. Inspect `backend/src/Api/Controllers/SearchController.cs` for `[Authorize(Policy = Policies.InternalUser)]`.
5. Run the specific search tests: `dotnet test backend/RecruitOps.sln --filter "FullyQualifiedName~Search"`.
