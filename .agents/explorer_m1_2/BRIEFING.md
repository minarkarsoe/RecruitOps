# BRIEFING — 2026-08-11T09:02:00Z

## Mission
Provide precise technical blueprint for Department Reach Scoping (ADR-0003 & ADR-0018) within SearchService.

## 🔒 My Identity
- Archetype: explorer
- Roles: technical investigator, blueprint designer
- Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_m1_2
- Original parent: 258a0dde-667b-4662-b08c-36ead83a8e7e
- Milestone: M1

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Precise technical blueprint for SearchService scoping

## Current Parent
- Conversation ID: 258a0dde-667b-4662-b08c-36ead83a8e7e
- Updated: 2026-08-11T09:02:00Z

## Investigation State
- **Explored paths**:
  - `docs/decisions/ADR-0003-department-scoping.md`
  - `docs/decisions/ADR-0018-approver-candidate-data-exclusion.md`
  - `backend/src/Domain/RoleScope.cs`
  - `backend/src/Application/Common/ICurrentUser.cs`
  - `backend/src/Application/Common/IDepartmentAccess.cs`
  - `backend/src/Api/Auth/CurrentUser.cs`
  - `backend/src/Infrastructure/Services/DepartmentAccess.cs`
  - `backend/src/Infrastructure/Services/ApplicationAccess.cs`
  - `backend/src/Infrastructure/Services/AnalyticsService.cs`
  - `backend/tests/RecruitOps.Api.Tests/ApproverReachTests.cs`
- **Key findings**:
  - `RoleScope` governs access along two distinct axes: `IsDepartmentScoped` (`HiringManager` only) and `IsExcludedFromCandidateData` (`Approver` only).
  - `ICurrentUser` exposes `IsDepartmentScoped` and `IsExcludedFromCandidateData`.
  - `IDepartmentAccess.AccessibleDepartmentIdsAsync(ct)` resolves department IDs for scoped roles.
  - `SearchService` must apply department scoping to Requisitions and JobPostings via `AllowedDepartmentIds.Contains(...)` for `HiringManager`.
  - For Candidates, `HiringManager` reaches candidates with applications in allowed departments OR where user is an interview participant.
  - `Approver` reaches Requisitions & JobPostings company-wide, but is strictly EXCLUDED from Candidates UNLESS candidate application has an interview where `Approver` is an interview participant (`ip.UserId == currentUserId`).
  - `Admin`, `HrDirector`, `Recruiter` are unscoped across the tenant for all categories.
- **Unexplored areas**: None. Codebase patterns and ADRs completely verified.

## Key Decisions Made
- Formulated exact LINQ queries and service architecture for `SearchService` integration.

## Artifact Index
- `analysis.md` — Detailed technical blueprint for Department Reach Scoping in SearchService.
- `handoff.md` — Structured 5-component handoff report.
