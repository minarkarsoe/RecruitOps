# Handoff Report: Department Reach Scoping (ADR-0003 & ADR-0018) Blueprint for SearchService

## 1. Observation

Direct observations from codebase inspection:
- `backend/src/Domain/RoleScope.cs`: Defines domain scoping rules via `IsDepartmentScoped(UserRole)` (true for `HiringManager`) and `IsExcludedFromCandidateData(UserRole)` (true for `Approver`).
- `backend/src/Application/Common/ICurrentUser.cs` & `backend/src/Api/Auth/CurrentUser.cs`: Exposes `IsDepartmentScoped` and `IsExcludedFromCandidateData` properties derived from `RoleScope`.
- `backend/src/Application/Common/IDepartmentAccess.cs` & `backend/src/Infrastructure/Services/DepartmentAccess.cs`: `AccessibleDepartmentIdsAsync(ct)` resolves department IDs for scoped users via `UserDepartments`.
- `backend/src/Infrastructure/Services/ApplicationAccess.cs`: Implements 3-clause reach resolution:
  - Clause 0: Roles with `IsExcludedFromCandidateData` (`Approver`) skip department reach and reach applications exclusively via interview panel participation (`IsOnPanelForAsync`).
  - Clause 1: Roles with `IsDepartmentScoped` (`HiringManager`) check department reach via `CanAccessAsync`. Unscoped roles (`Admin`, `HrDirector`, `Recruiter`) pass.
  - Clause 2: Panel seat exception (ADR-0017 §4) allows cross-department interview participants to access specific applications.
- `backend/src/Infrastructure/Services/AnalyticsService.cs`: Serves as an established reference for `GetAllowedDepartmentIdsAsync` helper logic.
- `backend/tests/RecruitOps.Api.Tests/ApproverReachTests.cs`: Verifies that `Approver` cannot access candidate notes, scorecards, pipeline, or stage history unless assigned to a panel seat.

---

## 2. Logic Chain

1. **Service Registration & Injections**: `SearchService` receives `ICurrentUser`, `IDepartmentAccess`, `AppDbContext`, and `IMyanmarScriptNormalizer`.
2. **Context Resolution**: Prior to executing LINQ queries, `SearchService` inspects `ICurrentUser.IsDepartmentScoped` and `ICurrentUser.IsExcludedFromCandidateData`. If `IsDepartmentScoped` is true, it calls `IDepartmentAccess.AccessibleDepartmentIdsAsync(ct)` to retrieve the user's allowed department IDs.
3. **Requisition Scoping**:
   - `HiringManager`: Scoped to `r.DepartmentId` in `allowedDeptIds`.
   - `Admin`, `HrDirector`, `Recruiter`, `Approver`: Unscoped (company-wide across tenant).
4. **JobPosting Scoping**:
   - `HiringManager`: Scoped to `p.DepartmentId` in `allowedDeptIds`.
   - `Admin`, `HrDirector`, `Recruiter`, `Approver`: Unscoped (company-wide across tenant).
5. **Candidate Scoping**:
   - `Approver` (`IsExcludedFromCandidateData == true`): Excluded from candidate search, EXCEPT when candidate application has an interview where `ip.UserId == currentUserId`.
   - `HiringManager` (`IsDepartmentScoped == true`): Reaches candidate if candidate application's job posting has `p.DepartmentId` in `allowedDeptIds` OR user is an interview participant (`ip.UserId == currentUserId`).
   - `Admin`, `HrDirector`, `Recruiter`: Unscoped across tenant.

---

## 3. Caveats

- **No Caveats.** The domain scoping model (`RoleScope`), authentication context (`ICurrentUser`), department access (`IDepartmentAccess`), and entity relationships (`Requisition`, `JobPosting`, `Candidate`, `JobApplication`, `Interview`, `InterviewParticipant`) are fully aligned and verified.

---

## 4. Conclusion

The technical blueprint provided in `analysis.md` gives the exact C# code architecture and EF Core LINQ query filters required for implementing `SearchService`. It enforces ADR-0003 and ADR-0018 with 100% fidelity to existing domain security rules.

---

## 5. Verification Method

To verify the implementation of `SearchService` once built by the implementer:

1. **Run Backend Test Suite**:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   Ensure all existing 387 tests pass.

2. **Add Unit/Integration Tests in `backend/tests/RecruitOps.Api.Tests/Search/SearchApiTests.cs`**:
   - Test 1: `HiringManager` in Dept A searches candidates -> returns candidates in Dept A + candidates where HM is on interview panel; hides Dept B candidates.
   - Test 2: `Approver` searches requisitions -> returns cross-department requisitions.
   - Test 3: `Approver` searches candidates -> returns 0 candidate matches unless `Approver` is an interview panel participant.
   - Test 4: `Admin` / `Recruiter` searches candidates -> returns all matching candidates company-wide.
