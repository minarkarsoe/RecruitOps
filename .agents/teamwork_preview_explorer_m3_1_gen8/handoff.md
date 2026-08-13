# Technical Design & Implementation Blueprint — Milestone 3 (R3 Analytics Dashboard Page & Report Builder UI)

## 1. Observation

### 1.1 Backend API Contracts
The backend reporting and analytics endpoints are defined in `backend/src/Api/Controllers/AnalyticsController.cs` and `backend/src/Application/DTOs/AnalyticsDtos.cs`:
- Controller attribute: `[Authorize(Policy = Policies.InternalUser)]` at route `[Route("api/analytics")]`.
- **Endpoints**:
  1. `GET /api/analytics/kpis`
     - Returns `KpiMetricsDto`:
       - `avgTimeToHireDays`: `double` (Average days from application/sourcing to hire)
       - `activeRequisitions`: `int` (Count of non-closed/approved requisitions)
       - `totalApplications`: `int` (Total candidate applications across tenant scope)
       - `overallHireRate`: `double` (Ratio or percentage of hired applications)
  2. `GET /api/analytics/time-to-hire`
     - Returns `TimeToHireAnalyticsDto`:
       - `stageDurations`: `List<StageDurationDto>` (`stage: string`, `avgDays: double`)
       - `departmentBreakdown`: `List<DepartmentTimeDto>` (`departmentId: Guid`, `departmentName: string`, `avgDays: double`, `hiredCount: int`)
       - `postingBreakdown`: `List<PostingTimeDto>` (`jobPostingId: Guid`, `postingTitle: string`, `avgDays: double`, `hiredCount: int`)
  3. `GET /api/analytics/conversion`
     - Returns `ConversionFunnelAnalyticsDto`:
       - `funnel`: `List<StageFunnelItemDto>` (`stage: string`, `count: int`, `dropOffRate: double`)
  4. `GET /api/analytics/source-of-hire`
     - Returns `SourceOfHireAnalyticsDto`:
       - `sources`: `List<SourceDistributionItemDto>` (`source: string`, `count: int`, `percentage: double`)
  5. `POST /api/analytics/reports/query`
     - Accepts `ReportQueryRequestDto` body:
       - `dateFrom`: `DateTimeOffset?`
       - `dateTo`: `DateTimeOffset?`
       - `departmentId`: `Guid?`
       - `jobPostingId`: `Guid?`
       - `stages`: `List<PipelineStatus>?`
       - `columns`: `List<string>?`
     - Returns `ReportQueryResultDto`:
       - `headers`: `List<string>`
       - `rows`: `List<Dictionary<string, object?>>`
  6. `GET /api/analytics/reports/export`
     - Accepts `[FromQuery] ReportQueryRequestDto request`.
     - Returns `File(csvBytes, "text/csv", "report.csv")`.

### 1.2 Frontend Codebase & UI Conventions
- **Shared Types**: Defined in `packages/types/src/index.ts`. Currently `analytics.ts` is not present in `packages/types/src/`.
- **API Client & Auth**:
  - `frontend/internal/src/lib/api.ts` exports `apiFetch<T>` and helper namespaces (`resumeApi`, `aiApi`).
  - Auth sessions manage tokens and permissions (`frontend/internal/src/lib/auth.ts`). Roles include `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `SuperAdmin`.
  - Roles authorized for Analytics: `Admin`, `HrDirector`, `Recruiter`, `HiringManager`.
- **Layout & Routing**:
  - `frontend/internal/src/App.tsx`: React Router v6 setup with `AppLayout`.
  - `frontend/internal/src/components/Sidebar.tsx`: Navigation sidebar with permissions checks and role filter.
  - `frontend/internal/src/components/AppLayout.tsx`: Global Ctrl+K `CommandPalette` item registry.
- **UI Primitives**:
  - `@recruitops/ui` provides `Card`, `Button`, `Table`, `TableHeader`, `TableBody`, `TableRow`, `TableHead`, `TableCell`, `Badge`, `Select`, `Input`, `Skeleton`, `StatusPill`, `PipelineStageRail`.
- **Testing**:
  - Vitest + `@testing-library/react` configured in `frontend/internal`. Page tests mock services/API using `vi.mock`.

---

## 2. Logic Chain

From the observations above, to achieve a clean, feature-based architecture for Milestone 3, we must construct the following artifacts in sequence:

### Step 2.1: Shared TypeScript Definitions (`@recruitops/types`)
- Create `packages/types/src/analytics.ts` defining:
  - `KpiMetricsDto`
  - `StageDurationDto`
  - `DepartmentTimeDto`
  - `PostingTimeDto`
  - `TimeToHireAnalyticsDto`
  - `StageFunnelItemDto`
  - `ConversionFunnelAnalyticsDto`
  - `SourceDistributionItemDto`
  - `SourceOfHireAnalyticsDto`
  - `ReportQueryRequestDto`
  - `ReportQueryResultDto`
- Re-export in `packages/types/src/index.ts`:
  ```typescript
  export * from './analytics';
  ```

### Step 2.2: Analytics API Client (`analyticsApi.ts`) & Hook (`useAnalytics.ts`)
- File: `frontend/internal/src/features/analytics/analyticsApi.ts`
  - Encapsulates `getKpis()`, `getTimeToHire()`, `getConversionFunnel()`, `getSourceOfHire()`, `queryReport(req)`, and `exportReportCsv(req)`.
  - Handles CSV GET request with URL search params and returns `Blob` for browser download.
- File: `frontend/internal/src/features/analytics/useAnalytics.ts`
  - React custom hook managing state for KPI metrics, time-to-hire, stage funnel, source distribution, custom query results, loading spinners, and error state.
  - Exposes `refresh()`, `runReportQuery(query)`, and `downloadReportCsv(query)`.

### Step 2.3: Visual Feature Components (`frontend/internal/src/features/analytics/`)
1. **`KpiCardSection.tsx`**:
   - High-density card grid rendering 4 core metrics:
     - Average Time-to-Hire (in days)
     - Active Requisitions count
     - Total Applications volume
     - Overall Hire Rate (%)
   - Skeletons displayed during initial loading.
2. **`TimeToHireChart.tsx`**:
   - Visual breakdown of average days spent per pipeline stage (Sourced, Applied, Screening, Shortlisted, Interview, Offer, Hired).
   - Department breakdown listing average time-to-hire by department with hired applicant count badges.
3. **`FunnelChart.tsx`**:
   - Stage conversion funnel visualization.
   - Shows candidate counts through each stage transition alongside drop-off percentage badges.
4. **`SourceDistributionChart.tsx`**:
   - Candidate acquisition source distribution (Public Page, Referral, Sourced, Multi-Channel).
   - Shows channel breakdown bars, candidate counts, and relative percentages.
5. **`CustomReportBuilder.tsx`**:
   - Interactive report generator panel:
     - Filter bar: Date Range (`dateFrom`, `dateTo`), Department filter dropdown, Job Posting selector, Stage checkboxes.
     - Column toggles: Select visible output columns.
     - Actions: "Run Query" button and "Export to CSV" download button.
     - Data Table Preview: Uses `@recruitops/ui` `Table` component to render query result headers and rows dynamically.
6. **`index.ts`**:
   - Re-exports feature components and hooks.

### Step 2.4: Analytics Page & Navigation Integration
- **`frontend/internal/src/pages/AnalyticsPage.tsx`**:
  - Assembles `KpiCardSection`, `TimeToHireChart`, `FunnelChart`, `SourceDistributionChart`, and `CustomReportBuilder`.
  - Includes page title, refresh action button, and error alerts.
- **`frontend/internal/src/App.tsx`**:
  - Add route `<Route path="/analytics" element={<AnalyticsPage />} />` inside `<AppLayout />`.
- **`frontend/internal/src/components/Sidebar.tsx`**:
  - Add "Analytics" link to sidebar navigation under Recruitment/Insights group, visible to authorized internal roles (`Admin`, `HrDirector`, `Recruiter`, `HiringManager`).
- **`frontend/internal/src/components/AppLayout.tsx`**:
  - Register `/analytics` in global Command Palette (`Ctrl+K` shortcut: `G A`).

### Step 2.5: Vitest Test Suite (`AnalyticsPage.test.tsx`)
- File: `frontend/internal/src/features/analytics/__tests__/AnalyticsPage.test.tsx`
- Minimum 5 comprehensive test cases:
  1. `renders Analytics dashboard page with header and skeleton loader`: verifies initial load state.
  2. `renders KPI metrics cards correctly with backend data`: verifies KPI figures display accurately.
  3. `renders Time-to-Hire, Conversion Funnel, and Source Distribution charts`: verifies chart component rendering.
  4. `executes custom report query and updates preview table`: verifies running custom report query updates tabular preview.
  5. `handles Export to CSV button click and triggers download`: verifies CSV download trigger fires with active filters.

---

## 3. Caveats

1. **Department Scoping (ADR-0003)**:
   - Backend enforces row-level department scoping for `HiringManager` roles. The UI must cleanly handle department-filtered responses without assuming all tenant departments are visible.
2. **CSV Export Blob Download**:
   - Browser environment requires converting CSV response array buffers into `Blob` and triggering a temporary `<a>` element download with `window.URL.createObjectURL(blob)`. In test environments (jsdom), `URL.createObjectURL` and `URL.revokeObjectURL` must be mocked if not available.
3. **Authorized Internal Roles**:
   - Approver role is excluded from candidate data (ADR-0018). Sidebar visibility for `/analytics` must check that the user is an authorized internal role (Admin, HR Director, Recruiter, Hiring Manager) and not an Approver without candidate/analytics permissions.
4. **No Direct Source Editing**:
   - As Explorer 1, code modifications to source folders are prohibited during this read-only phase. All file definitions and instructions are fully documented in this handoff for the Implementer.

---

## 4. Conclusion

The design specification for Milestone 3 provides a clean, robust, and feature-driven implementation plan for the RecruitOps Analytics Dashboard and Custom Report Builder UI. 

### Target File Inventory & Responsibilities
| File Path | Purpose |
|---|---|
| `packages/types/src/analytics.ts` | TypeScript interfaces for Analytics DTOs & queries |
| `packages/types/src/index.ts` | Re-export analytics types |
| `frontend/internal/src/features/analytics/analyticsApi.ts` | Analytics REST API client calls & CSV downloader |
| `frontend/internal/src/features/analytics/useAnalytics.ts` | Custom hook for dashboard state & report query actions |
| `frontend/internal/src/features/analytics/KpiCardSection.tsx` | Visual KPI metrics cards component |
| `frontend/internal/src/features/analytics/TimeToHireChart.tsx` | Time-to-hire stage durations & department breakdown |
| `frontend/internal/src/features/analytics/FunnelChart.tsx` | Pipeline stage conversion funnel & drop-off % |
| `frontend/internal/src/features/analytics/SourceDistributionChart.tsx` | Source of hire distribution visual breakdown |
| `frontend/internal/src/features/analytics/CustomReportBuilder.tsx` | Report filter, column toggle, table preview & CSV export |
| `frontend/internal/src/features/analytics/index.ts` | Module index export |
| `frontend/internal/src/pages/AnalyticsPage.tsx` | Main `/analytics` dashboard page component |
| `frontend/internal/src/App.tsx` | Route mapping for `/analytics` |
| `frontend/internal/src/components/Sidebar.tsx` | Sidebar menu link for Analytics |
| `frontend/internal/src/components/AppLayout.tsx` | Ctrl+K Command Palette item for Analytics |
| `frontend/internal/src/features/analytics/__tests__/AnalyticsPage.test.tsx` | Vitest test suite covering page, cards, charts, query, export |

---

## 5. Verification Method

To verify the implementation independently once built:

1. **TypeScript Type Check**:
   ```bash
   npm run typecheck
   ```
   *Expected outcome*: 0 errors across all workspaces (`@recruitops/types`, `@recruitops/internal`, `@recruitops/public`).

2. **Frontend Vitest Test Suite**:
   ```bash
   npx vitest run frontend/internal/src/features/analytics/__tests__/AnalyticsPage.test.tsx
   ```
   *Expected outcome*: All 5+ test cases pass cleanly.

3. **Full Project Test Suite**:
   ```bash
   npm run test --prefix frontend/internal
   ```
   *Expected outcome*: All existing tests (256+) plus 5+ new analytics tests pass cleanly (261+ total passing tests).
