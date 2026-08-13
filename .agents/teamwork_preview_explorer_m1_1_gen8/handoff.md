# Handoff Report: Milestone 1 — R1 Analytics & Metrics Backend APIs

## 1. Observation

### 1.1 Existing Backend Architecture & File Locations
- **Domain Layer (`backend/src/Domain/`)**:
  - `Entities/ApplicationStageHistory.cs` (lines 17–34): Append-only history entity storing stage transitions (`TenantId`, `JobApplicationId`, `FromStatus`, `ToStatus`, `ChangedAt`, `ChangedByUserId`, `Note`).
  - `Entities/JobApplication.cs` (lines 10–43): Stores application state (`TenantId`, `JobPostingId`, `CandidateId`, `Status` [enum `PipelineStatus`], `Source` [enum `SourceChannel`], `AppliedAt`).
  - `Entities/JobPosting.cs` (lines 13–48): Links to `DepartmentId` and `RequisitionId`, with status `JobStatus` (`Draft`, `Live`, `Closed`).
  - `Entities/Requisition.cs` (lines 8–30): Links to `DepartmentId`, with `RequisitionStatus` (`Draft`, `PendingApproval`, `Approved`, `Rejected`, `Cancelled`).
  - `Entities/UserDepartment.cs` (lines 7–12): Links `UserId` to `DepartmentId` for department scoping.
  - `RoleScope.cs` (lines 26–27): `IsDepartmentScoped(UserRole role) => role is UserRole.HiringManager`.
- **Application Layer (`backend/src/Application/`)**:
  - `Common/ICurrentUser.cs` (lines 4–30): Access to `UserId`, `Role`, `IsDepartmentScoped`, `IsExcludedFromCandidateData`, `IsSuperAdmin`.
  - `Common/IDepartmentAccess.cs` (lines 7–15): `AccessibleDepartmentIdsAsync(ct)` and `CanAccessAsync(departmentId, ct)`.
- **Infrastructure Layer (`backend/src/Infrastructure/`)**:
  - `Services/DepartmentAccess.cs` (lines 12–52): Resolves accessible `DepartmentId`s for scoped users via `UserDepartments` table, caches per request. Unscoped roles (`Admin`, `HrDirector`, `Recruiter`) bypass department restriction.
  - `Persistence/AppDbContext.cs` (lines 15–46): Exposes `DbSet<ApplicationStageHistory>`, `DbSet<JobApplication>`, `DbSet<JobPosting>`, `DbSet<Requisition>`, `DbSet<Department>`, `DbSet<UserDepartment>`.
  - `DependencyInjection.cs` (lines 20–110): Service registration container.
- **API Layer (`backend/src/Api/`)**:
  - `Auth/Policies.cs` (lines 4–27): `Policies.InternalUser` permits internal roles (`Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`). Endpoints using `InternalUser` must apply department scoping predicates explicitly per ADR-0003.
- **Tests (`backend/tests/RecruitOps.Api.Tests/`)**:
  - `CustomWebAppFactory.cs` (lines 19–222): Test fixture setting up in-memory DB, multi-tenant seeding (`TenantA`, `TenantB`), departments (`SalesDepartmentId`, `FinanceDepartmentId`), and scoped users (`HiringManagerUserId` owning Sales only).

---

## 2. Logic Chain

### 2.1 Interface & DTO Contracts Specification
To fulfill Requirement R1 (`GET /api/analytics/kpis`, `GET /api/analytics/time-to-hire`, `GET /api/analytics/conversion`, `GET /api/analytics/source-of-hire`), create `backend/src/Application/DTOs/AnalyticsDtos.cs` and `backend/src/Application/Interfaces/IAnalyticsService.cs`:

#### DTO Definitions (`AnalyticsDtos.cs`):
```csharp
namespace RecruitOps.Application.DTOs;

public record KpiMetricsDto(
    double AvgTimeToHireDays,
    int ActiveRequisitions,
    int TotalApplications,
    double OverallHireRate
);

public record StageDurationDto(
    string Stage,
    double AvgDays
);

public record DepartmentTimeDto(
    Guid DepartmentId,
    string DepartmentName,
    double AvgDays,
    int HiredCount
);

public record PostingTimeDto(
    Guid JobPostingId,
    string PostingTitle,
    double AvgDays,
    int HiredCount
);

public record TimeToHireAnalyticsDto(
    IReadOnlyList<StageDurationDto> StageDurations,
    IReadOnlyList<DepartmentTimeDto> DepartmentBreakdown,
    IReadOnlyList<PostingTimeDto> PostingBreakdown
);

public record StageFunnelItemDto(
    string Stage,
    int Count,
    double DropOffRate
);

public record ConversionFunnelAnalyticsDto(
    IReadOnlyList<StageFunnelItemDto> Funnel
);

public record SourceDistributionItemDto(
    string Source,
    int Count,
    double Percentage
);

public record SourceOfHireAnalyticsDto(
    IReadOnlyList<SourceDistributionItemDto> Sources
);
```

#### Application Interface (`IAnalyticsService.cs`):
```csharp
namespace RecruitOps.Application.Interfaces;

public interface IAnalyticsService
{
    Task<KpiMetricsDto> GetKpiMetricsAsync(CancellationToken ct = default);
    Task<TimeToHireAnalyticsDto> GetTimeToHireAsync(CancellationToken ct = default);
    Task<ConversionFunnelAnalyticsDto> GetConversionFunnelAsync(CancellationToken ct = default);
    Task<SourceOfHireAnalyticsDto> GetSourceOfHireAsync(CancellationToken ct = default);
}
```

---

### 2.2 Department Reach Scoping (ADR-0003) & Query Design
All queries must enforce ADR-0003 department scoping:
1. Check `_user.IsDepartmentScoped`.
2. If `true` (e.g. `HiringManager`), fetch `allowedIds = await _departmentAccess.AccessibleDepartmentIdsAsync(ct)`.
3. Filter `JobPosting` query by `allowedIds.Contains(p.DepartmentId)`. If `allowedIds` is empty, return empty result sets immediately.
4. Filter `JobApplication` query by `JobPostingId` matching the scoped postings query.
5. Filter `Requisition` query by `allowedIds.Contains(r.DepartmentId)`.

---

### 2.3 Analytics Calculations Implementation (`AnalyticsService.cs`)

#### 1. `GetKpiMetricsAsync`:
- **`activeRequisitions`**: Count of `Requisition` rows in scope with `Status == RequisitionStatus.Approved` (or `JobPosting` with `Status == JobStatus.Live`).
- **`totalApplications`**: Count of `JobApplication` rows matching scoped postings.
- **`avgTimeToHireDays`**: For hired applications (`Status == PipelineStatus.Hired`), calculate duration in days from `AppliedAt` (or earliest stage history record) to the `ApplicationStageHistory` record where `ToStatus == PipelineStatus.Hired` (`ChangedAt`). Average across hired applications, rounded to 1 decimal place. Returns `0.0` if no hires.
- **`overallHireRate`**: `totalApplications > 0 ? Math.Round((double)hiredCount / totalApplications * 100.0, 1) : 0.0`.

#### 2. `GetTimeToHireAsync`:
- **`stageDurations`**: Group transition durations by pipeline stage (`Sourced`, `Applied`, `Screening`, `Shortlisted`, `Interview`, `Offer`, `Hired`). For consecutive stage history entries for an application, calculate `(next.ChangedAt - current.ChangedAt).TotalDays`.
- **`departmentBreakdown`**: Group hired applications by `DepartmentId`, calculate average time-to-hire and hired count per department.
- **`postingBreakdown`**: Group hired applications by `JobPostingId`, calculate average time-to-hire and hired count per job posting.

#### 3. `GetConversionFunnelAsync`:
- Sequence of stages: `Sourced`, `Applied`, `Screening`, `Shortlisted`, `Interview`, `Offer`, `Hired`.
- For each stage:
  - `count`: Number of applications that reached or passed through this stage (or currently at/above this stage index).
  - `dropOffRate`: `0.0` for the initial stage; for stage $k > 0$, `(1.0 - count[k] / count[k-1]) * 100.0` (rounded to 1 decimal place), or `0.0` if `count[k-1] == 0`.

#### 4. `GetSourceOfHireAsync`:
- Group applications by `Source` (`SourceChannel` enum: `Direct`, `Facebook`, `LinkedIn`, `Telegram`, `Referral`, `ExcelImport`).
- For each source: `source` (name string), `count`, `percentage` (`(double)count / totalApplications * 100.0`).

---

### 2.4 API Controller (`AnalyticsController.cs`)
Location: `backend/src/Api/Controllers/AnalyticsController.cs`
- Route: `[Route("api/analytics")]`
- Authorization: `[Authorize(Policy = Policies.InternalUser)]`
- Endpoints:
  - `GET /api/analytics/kpis` -> `GetKpis(CancellationToken ct)`
  - `GET /api/analytics/time-to-hire` -> `GetTimeToHire(CancellationToken ct)`
  - `GET /api/analytics/conversion` -> `GetConversion(CancellationToken ct)`
  - `GET /api/analytics/source-of-hire` -> `GetSourceOfHire(CancellationToken ct)`

---

### 2.5 DI Registration (`DependencyInjection.cs`)
In `backend/src/Infrastructure/DependencyInjection.cs`:
```csharp
services.AddScoped<IAnalyticsService, AnalyticsService>();
```

---

## 3. Caveats
- **Historical Data Backfill**: Metrics rely on `ApplicationStageHistory` records. Legacy applications created prior to stage history tracking will fall back to `JobApplication.AppliedAt` and `JobApplication.UpdatedAt` when calculating duration.
- **Approver Role Handling**: Under ADR-0018, `Approver` is excluded from candidate data (`IsExcludedFromCandidateData == true`). When an Approver calls analytics endpoints, department scoping or candidate data exclusion should safely return empty/zero metrics or be restricted as appropriate.
- **Empty Scope Safeguard**: If a `HiringManager` has no assigned departments, all queries immediately short-circuit to return empty lists/zero metrics without performing DB joins.

---

## 4. Conclusion
The proposed design for Milestone 1:
1. Adheres strictly to Clean Architecture (DTOs & interfaces in `RecruitOps.Application`, implementation in `RecruitOps.Infrastructure`, API controller in `RecruitOps.Api`).
2. Enforces ADR-0003 department reach scoping across all 4 analytics endpoints.
3. Derives metrics accurately from `ApplicationStageHistory` and domain entities.
4. Prepares a solid foundation for Milestone 2 (Custom Report Query & CSV Export) and Milestone 3 (Frontend Analytics Dashboard).

---

## 5. Verification Method

### 5.1 Command Line Verification
Run backend unit and integration tests:
```bash
dotnet test backend/RecruitOps.sln
```

### 5.2 Test Scenarios to Implement (`backend/tests/RecruitOps.Api.Tests/AnalyticsApiTests.cs`)
1. `Unauthenticated_GetKpis_Returns_401`: Verify unauthenticated request receives 401 Unauthorized.
2. `Admin_GetKpis_Returns_Aggregated_Metrics`: Verify Admin sees company-wide KPIs.
3. `HiringManager_GetKpis_Enforces_Department_Scoping`: Verify Hiring Manager only sees metrics for their assigned department (`SalesDepartmentId`).
4. `GetTimeToHire_Calculates_Stage_And_Department_Breakdown`: Verify stage durations and department breakdowns are accurately computed.
5. `GetConversionFunnel_Calculates_Counts_And_Dropoff`: Verify stage funnel counts and drop-off percentages.
6. `GetSourceOfHire_Calculates_Channel_Distribution`: Verify source distribution counts and percentages.
7. `Department_With_No_Applications_Returns_Zero_Metrics`: Verify zero division handling when no applications exist.
8. `HiringManager_With_No_Departments_Returns_Zero_Metrics`: Verify short-circuit behavior for unassigned hiring managers.
