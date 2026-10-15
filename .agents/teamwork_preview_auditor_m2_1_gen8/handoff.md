# Forensic Audit Report — Milestone 2 (Custom Report Builder & CSV Export API)

**Work Product**: Milestone 2 Backend Implementation (`AnalyticsController.cs`, `AnalyticsService.cs`, `AnalyticsDtos.cs`, `IAnalyticsService.cs`, `AnalyticsApiTests.cs`)
**Profile**: General Project (Development Mode)
**Verdict**: CLEAN

---

## 1. Observation

### Codebase & Implementation Audit
1. **`backend/src/Api/Controllers/AnalyticsController.cs`**:
   - `POST /api/analytics/reports/query` delegates directly to `_analyticsService.QueryReportAsync(request, ct)`.
   - `GET /api/analytics/reports/export` delegates directly to `_analyticsService.ExportReportCsvAsync(request, ct)` and returns `File(csvBytes, "text/csv", "report.csv")`.
   - Enforces `[Authorize(Policy = Policies.InternalUser)]` policy for internal roles.

2. **`backend/src/Infrastructure/Services/AnalyticsService.cs`**:
   - Implements `QueryReportAsync` and `ExportReportCsvAsync` using real LINQ queries via `AppDbContext` joining `JobApplications`, `JobPostings`, `Departments`, and `Candidates` with `AsNoTracking()`.
   - `GetAllowedDepartmentIdsAsync`: Enforces ADR-0018 candidate data exclusion for Approver roles and ADR-0003 department reach scoping for Hiring Managers.
   - `ResolveColumns`: Dynamically maps requested column keys to `AvailableColumns` (10 supported columns), preserving custom selection and header ordering, falling back to 7 default column keys when unassigned.
   - `ExportReportCsvAsync`: Implements RFC 4180 field escaping (`EscapeCsvField`) for quotes, commas, and newlines, and prepends UTF-8 Byte Order Mark (`0xEF, 0xBB, 0xBF`) via `new UTF8Encoding(true)` to ensure proper display of international text (e.g. Burmese names).

3. **`backend/src/Application/DTOs/AnalyticsDtos.cs` & `IAnalyticsService.cs`**:
   - Defines clean, strongly typed DTO contracts (`ReportQueryRequestDto`, `ReportQueryResultDto`).

4. **`backend/tests/RecruitOps.Api.Tests/AnalyticsApiTests.cs`**:
   - Includes 6 comprehensive integration tests for Milestone 2:
     - `Unauthenticated_Analytics_Endpoints_Return_401`
     - `Admin_QueryReport_With_Filtering_And_Custom_Columns`
     - `Admin_ExportReportCsv_Returns_Csv_File_With_Headers_And_Utf8_Bom`
     - `HiringManager_QueryReport_Enforces_Department_Scoping`
     - `Approver_Role_QueryReport_And_Export_Returns_Empty_Report`
     - `ExportReportCsv_Escapes_Special_Characters_Per_Rfc4180`
   - All tests execute actual HTTP requests against the web application factory and assert real database outputs, HTTP status codes, CSV string contents, UTF-8 BOM headers, and RFC 4180 escaping.

### Phase 1 Audit Check Results

| Check Name | Status | Details |
|------------|--------|---------|
| **1. Hardcoded Output Detection** | **PASS** | No hardcoded expected values or short-circuited outputs found in production code. |
| **2. Facade Detection** | **PASS** | `AnalyticsService` contains complete, genuine LINQ query processing and CSV generation logic. |
| **3. Pre-populated Artifact Detection** | **PASS** | No pre-existing logs, mock CSV files, or fake test artifacts found. |
| **4. Self-Certifying / Test Bypass Check** | **PASS** | Tests execute actual API endpoints against simulated database state and assert real CSV bytes and JSON structures. |
| **5. Execution Delegation Check** | **PASS** | Implementation uses standard C# EF Core and BCL `System.Text.Encoding` without illegal external tool delegation. |

---

## 2. Logic Chain

1. **Security & Scope Verification**:
   Inspection of `GetAllowedDepartmentIdsAsync` confirmed that Approvers are immediately blocked from retrieving candidate data (`(true, null)`), returning empty queries/CSV headers only. Hiring Managers are restricted to their assigned department IDs (`allowedDeptIds.Contains(...)`). Unassigned requests or cross-department queries by Hiring Managers return empty result sets as required by ADR-0003 and ADR-0018.

2. **Functional Logic Verification**:
   `FetchReportDataAsync` constructs an EF Core LINQ query filtering by date range (`DateFrom`, `DateTo`), department (`DepartmentId`), job posting (`JobPostingId`), and pipeline stages (`Stages`). Rows are evaluated through `ReportColumnDef` mapping functions to format strings dynamically.

3. **CSV Export Standard Compliance**:
   `ExportReportCsvAsync` applies `EscapeCsvField` to escape double quotes and wrap fields containing special characters in quotes per RFC 4180. UTF-8 BOM preamble `[0xEF, 0xBB, 0xBF]` is explicitly prepended to the binary output stream.

4. **Empirical Verification**:
   Executed `dotnet test backend/RecruitOps.sln`.
   - `RecruitOps.Domain.Tests.dll`: 51 passed, 0 failed.
   - `RecruitOps.Api.Tests.dll`: 336 passed, 0 failed.
   - **Total**: 387 tests passed cleanly (0 failures).

---

## 3. Caveats

**No caveats**: All implementation logic, security policies, dynamic column query handling, RFC 4180 CSV formatting, UTF-8 BOM encoding, and test assertions were independently verified.

---

## 4. Conclusion

**Verdict**: **CLEAN**

Milestone 2 (R2 Custom Report Builder & CSV Export API) work product is free of any integrity violations, hardcoding, dummy facades, or test tampering. It is fully implemented and passing all 387 unit and integration tests.

---

## 5. Verification Method

To independently verify this audit:
1. Run `dotnet test backend/RecruitOps.sln` to confirm all 387 backend tests pass cleanly.
2. Inspect the source files:
   - `backend/src/Api/Controllers/AnalyticsController.cs`
   - `backend/src/Infrastructure/Services/AnalyticsService.cs`
   - `backend/src/Application/DTOs/AnalyticsDtos.cs`
   - `backend/src/Application/Interfaces/IAnalyticsService.cs`
   - `backend/tests/RecruitOps.Api.Tests/AnalyticsApiTests.cs`
