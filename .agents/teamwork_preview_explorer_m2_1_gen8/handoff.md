# Handoff Report — Milestone 2: Custom Report Builder & CSV Export API

## 1. Observation

### Existing Codebase Structure
- **DTOs (`backend/src/Application/DTOs/AnalyticsDtos.cs`)**:
  Currently contains Milestone 1 records (`KpiMetricsDto`, `StageDurationDto`, `DepartmentTimeDto`, `PostingTimeDto`, `TimeToHireAnalyticsDto`, `StageFunnelItemDto`, `ConversionFunnelAnalyticsDto`, `SourceDistributionItemDto`, `SourceOfHireAnalyticsDto`).
- **Interface (`backend/src/Application/Interfaces/IAnalyticsService.cs`)**:
  Currently defines 4 KPI methods (`GetKpiMetricsAsync`, `GetTimeToHireAsync`, `GetConversionFunnelAsync`, `GetSourceOfHireAsync`).
- **Service (`backend/src/Infrastructure/Services/AnalyticsService.cs`)**:
  - Implements `GetAllowedDepartmentIdsAsync(ct)` (lines 24–41) which enforces ADR-0018 (Approver data exclusion: returns `(true, null)`) and ADR-0003 (Department reach scoping for `IsDepartmentScoped` roles like Hiring Manager).
  - Uses EF Core `AppDbContext` to query `JobApplications`, `JobPostings`, `Departments`, `Candidates`, and `ApplicationStageHistories`.
- **Controller (`backend/src/Api/Controllers/AnalyticsController.cs`)**:
  Has route `[Route("api/analytics")]` with `[Authorize(Policy = Policies.InternalUser)]` (lines 13–15). Exposes 4 `[HttpGet]` endpoints (`kpis`, `time-to-hire`, `conversion`, `source-of-hire`).

## 2. Logic Chain

1. **Parameter Handling & Binding**:
   - `ReportQueryRequestDto` needs fields:
     - `DateFrom`: `DateTimeOffset?`
     - `DateTo`: `DateTimeOffset?`
     - `DepartmentId`: `Guid?`
     - `JobPostingId`: `Guid?`
     - `Stages`: `List<PipelineStatus>?`
     - `Columns`: `List<string>?`
   - For `POST /api/analytics/reports/query`, parameters are passed in the JSON request body (`[FromBody] ReportQueryRequestDto request`).
   - For `GET /api/analytics/reports/export`, parameters are passed via query string (`[FromQuery] ReportQueryRequestDto request`).

2. **Security & Scoping (ADR-0003 & ADR-0018)**:
   - Call existing `GetAllowedDepartmentIdsAsync(ct)`:
     - If `denied` (Approver role per ADR-0018): Return empty result (`ReportQueryResultDto` with empty rows, or CSV with header line only).
     - If `allowedDeptIds` is restricted (Hiring Manager per ADR-0003): If request explicitly passed a `DepartmentId` not in `allowedDeptIds`, return empty result. Otherwise, restrict LINQ query to `allowedDeptIds.Contains(posting.DepartmentId)`.
     - If `allowedDeptIds` is null (Admin, HR Director, Recruiter): Filter by `DepartmentId` if specified in request.

3. **LINQ Query Construction & Performance**:
   - Query `_db.JobApplications.AsNoTracking()` joined with `_db.JobPostings.AsNoTracking()`, `_db.Departments.AsNoTracking()`, and `_db.Candidates.AsNoTracking()`.
   - Apply filters conditionally:
     - `DateFrom`: `x.Application.AppliedAt >= request.DateFrom.Value`
     - `DateTo`: `x.Application.AppliedAt <= request.DateTo.Value`
     - `DepartmentId`: `x.Posting.DepartmentId == departmentId`
     - `JobPostingId`: `x.Posting.Id == request.JobPostingId.Value`
     - `Stages`: `request.Stages.Contains(x.Application.Status)`
   - Order results by `x.Application.AppliedAt` descending.

4. **Column Mapping & Tabular Projection**:
   - Available column dictionary:
     - `candidateName`: "Candidate Name" -> `x.Candidate.FullName`
     - `candidateEmail`: "Candidate Email" -> `x.Candidate.Email ?? ""`
     - `candidatePhone`: "Candidate Phone" -> `x.Candidate.Phone ?? ""`
     - `jobTitle`: "Job Title" -> `x.Posting.Title`
     - `department`: "Department" -> `x.Department.Name`
     - `stage`: "Stage" -> `x.Application.Status.ToString()`
     - `source`: "Source" -> `x.Application.Source.ToString()`
     - `appliedAt`: "Applied Date" -> `x.Application.AppliedAt.ToString("yyyy-MM-dd HH:mm:ss")`
     - `resumeFileName`: "Resume File" -> `x.Application.ResumeFileName ?? ""`
     - `applicationId`: "Application ID" -> `x.Application.Id.ToString()`
   - Default column selection (if `request.Columns` is null or empty):
     `["candidateName", "candidateEmail", "jobTitle", "department", "stage", "source", "appliedAt"]`.
   - `ReportQueryResultDto` returns `Headers` (`string[]` of formatted display labels) and `Rows` (`List<Dictionary<string, object?>>` mapping column keys to values).

5. **CSV Formatting & Stream Encoding**:
   - Headers line: comma-separated list of column display headers.
   - Data lines: comma-separated values evaluated from each matching record.
   - RFC 4180 escaping: fields containing `,`, `"`, `\n`, or `\r` wrapped in double quotes `"..."`, with existing internal double quotes doubled `""`.
   - UTF-8 BOM encoding: byte array prepended with UTF-8 BOM (`new UTF8Encoding(true)`) to ensure Microsoft Excel correctly displays Unicode characters (e.g. Burmese names).
   - Controller returns `File(csvBytes, "text/csv", "report.csv")`, which sets `Content-Type: text/csv` and `Content-Disposition: attachment; filename=report.csv`.

## 3. Caveats

- **Timezone handling**: Date filters are passed as `DateTimeOffset?`. All database timestamps (`AppliedAt`) are stored as UTC `DateTimeOffset`. Callers should pass ISO 8601 strings (e.g., `2026-08-01T00:00:00Z`).
- **Query limits**: If reports become very large, pagination or max row limits (e.g. 5,000 rows) can be added. Currently, reports return all matching records within the tenant.
- **Approver Data Exclusion**: Per ADR-0018, users with `IsExcludedFromCandidateData == true` must receive an empty report result rather than an error or 403, keeping error behavior uniform across analytics.

## 4. Conclusion & Proposed Implementation

Milestone 2 implementation requires changes across 4 backend files:

### Code Snippets / Proposed Changes

#### A. `backend/src/Application/DTOs/AnalyticsDtos.cs`
Add the following DTOs:

```csharp
public record ReportQueryRequestDto(
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    Guid? DepartmentId = null,
    Guid? JobPostingId = null,
    List<PipelineStatus>? Stages = null,
    List<string>? Columns = null
);

public record ReportQueryResultDto(
    IReadOnlyList<string> Headers,
    IReadOnlyList<Dictionary<string, object?>> Rows
);
```

#### B. `backend/src/Application/Interfaces/IAnalyticsService.cs`
Add the two method contracts:

```csharp
Task<ReportQueryResultDto> QueryReportAsync(ReportQueryRequestDto query, CancellationToken ct = default);
Task<byte[]> ExportReportCsvAsync(ReportQueryRequestDto query, CancellationToken ct = default);
```

#### C. `backend/src/Infrastructure/Services/AnalyticsService.cs`
Add column definitions, `QueryReportAsync`, `ExportReportCsvAsync`, and CSV escaping helper:

```csharp
private class ReportColumnDef
{
    public string Key { get; }
    public string Header { get; }
    public Func<dynamic, string> Evaluator { get; }

    public ReportColumnDef(string key, string header, Func<dynamic, string> evaluator)
    {
        Key = key;
        Header = header;
        Evaluator = evaluator;
    }
}

// Column definition map
private static readonly List<(string Key, string Header, Func<dynamic, string> Evaluator)> AvailableColumns = new()
{
    ("candidateName", "Candidate Name", x => (string)x.Candidate.FullName),
    ("candidateEmail", "Candidate Email", x => (string)(x.Candidate.Email ?? "")),
    ("candidatePhone", "Candidate Phone", x => (string)(x.Candidate.Phone ?? "")),
    ("jobTitle", "Job Title", x => (string)x.Posting.Title),
    ("department", "Department", x => (string)x.Department.Name),
    ("stage", "Stage", x => x.Application.Status.ToString()),
    ("source", "Source", x => x.Application.Source.ToString()),
    ("appliedAt", "Applied Date", x => ((DateTimeOffset)x.Application.AppliedAt).ToString("yyyy-MM-dd HH:mm:ss")),
    ("resumeFileName", "Resume File", x => (string)(x.Application.ResumeFileName ?? "")),
    ("applicationId", "Application ID", x => x.Application.Id.ToString())
};

private static readonly List<string> DefaultColumnKeys = new()
{
    "candidateName", "candidateEmail", "jobTitle", "department", "stage", "source", "appliedAt"
};

public async Task<ReportQueryResultDto> QueryReportAsync(ReportQueryRequestDto query, CancellationToken ct = default)
{
    var selectedCols = ResolveColumns(query.Columns);
    var items = await FetchReportDataAsync(query, ct);

    var headers = selectedCols.Select(c => c.Header).ToList();
    var rows = new List<Dictionary<string, object?>>();

    foreach (var item in items)
    {
        var row = new Dictionary<string, object?>();
        foreach (var col in selectedCols)
        {
            row[col.Key] = col.Evaluator(item);
        }
        rows.Add(row);
    }

    return new ReportQueryResultDto(headers, rows);
}

public async Task<byte[]> ExportReportCsvAsync(ReportQueryRequestDto query, CancellationToken ct = default)
{
    var selectedCols = ResolveColumns(query.Columns);
    var items = await FetchReportDataAsync(query, ct);

    var builder = new System.Text.StringBuilder();

    // Header row
    builder.AppendLine(string.Join(",", selectedCols.Select(c => EscapeCsvField(c.Header))));

    // Data rows
    foreach (var item in items)
    {
        var rowValues = selectedCols.Select(c => EscapeCsvField(c.Evaluator(item)));
        builder.AppendLine(string.Join(",", rowValues));
    }

    var encoding = new System.Text.UTF8Encoding(true);
    return encoding.GetPreamble().Concat(encoding.GetBytes(builder.ToString())).ToArray();
}

private List<(string Key, string Header, Func<dynamic, string> Evaluator)> ResolveColumns(List<string>? requestedColumns)
{
    if (requestedColumns == null || requestedColumns.Count == 0)
    {
        return AvailableColumns.Where(c => DefaultColumnKeys.Contains(c.Key)).ToList();
    }

    var set = requestedColumns.Select(k => k.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var matched = AvailableColumns.Where(c => set.Contains(c.Key)).ToList();
    return matched.Count > 0 ? matched : AvailableColumns.Where(c => DefaultColumnKeys.Contains(c.Key)).ToList();
}

private async Task<List<dynamic>> FetchReportDataAsync(ReportQueryRequestDto query, CancellationToken ct)
{
    var (denied, allowedDeptIds) = await GetAllowedDepartmentIdsAsync(ct);
    if (denied) return new List<dynamic>();

    var baseQuery = from app in _db.JobApplications.AsNoTracking()
                    join posting in _db.JobPostings.AsNoTracking() on app.JobPostingId equals posting.Id
                    join dept in _db.Departments.AsNoTracking() on posting.DepartmentId equals dept.Id
                    join candidate in _db.Candidates.AsNoTracking() on app.CandidateId equals candidate.Id
                    select new { Application = app, Posting = posting, Department = dept, Candidate = candidate };

    if (allowedDeptIds is not null)
    {
        if (query.DepartmentId.HasValue)
        {
            if (!allowedDeptIds.Contains(query.DepartmentId.Value))
                return new List<dynamic>();
            baseQuery = baseQuery.Where(x => x.Posting.DepartmentId == query.DepartmentId.Value);
        }
        else
        {
            baseQuery = baseQuery.Where(x => allowedDeptIds.Contains(x.Posting.DepartmentId));
        }
    }
    else if (query.DepartmentId.HasValue)
    {
        baseQuery = baseQuery.Where(x => x.Posting.DepartmentId == query.DepartmentId.Value);
    }

    if (query.JobPostingId.HasValue)
    {
        baseQuery = baseQuery.Where(x => x.Posting.Id == query.JobPostingId.Value);
    }

    if (query.DateFrom.HasValue)
    {
        baseQuery = baseQuery.Where(x => x.Application.AppliedAt >= query.DateFrom.Value);
    }

    if (query.DateTo.HasValue)
    {
        baseQuery = baseQuery.Where(x => x.Application.AppliedAt <= query.DateTo.Value);
    }

    if (query.Stages != null && query.Stages.Count > 0)
    {
        baseQuery = baseQuery.Where(x => query.Stages.Contains(x.Application.Status));
    }

    var list = await baseQuery.OrderByDescending(x => x.Application.AppliedAt).ToListAsync(ct);
    return list.Cast<dynamic>().ToList();
}

private static string EscapeCsvField(string? field)
{
    if (string.IsNullOrEmpty(field))
        return string.Empty;

    if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
    {
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }

    return field;
}
```

#### D. `backend/src/Api/Controllers/AnalyticsController.cs`
Add route methods for report query and export:

```csharp
[HttpPost("reports/query")]
public async Task<ActionResult<ReportQueryResultDto>> QueryReport([FromBody] ReportQueryRequestDto request, CancellationToken ct)
{
    var result = await _analyticsService.QueryReportAsync(request, ct);
    return Ok(result);
}

[HttpGet("reports/export")]
public async Task<IActionResult> ExportReport([FromQuery] ReportQueryRequestDto request, CancellationToken ct)
{
    var csvBytes = await _analyticsService.ExportReportCsvAsync(request, ct);
    return File(csvBytes, "text/csv", "report.csv");
}
```

## 5. Verification Method

1. **Build Verification**:
   Execute build command to ensure zero compilation errors:
   `dotnet build backend/RecruitOps.sln`

2. **Test Suite Verification**:
   Run full backend test suite to ensure all 369 existing tests + new unit tests pass:
   `dotnet test backend/RecruitOps.sln`

3. **Specific Test Scenarios**:
   - `POST /api/analytics/reports/query` returns HTTP 200 with JSON payload containing `headers` and `rows`.
   - `GET /api/analytics/reports/export` returns HTTP 200 with `Content-Type: text/csv` and header `Content-Disposition: attachment; filename=report.csv`.
   - Scoping test: Hiring Manager requesting report for unassigned department receives empty report.
   - Exclusion test: Approver role calling report query/export receives empty report (ADR-0018).
   - CSV escaping test: Candidate name containing quotes or commas is correctly quoted in CSV stream.
