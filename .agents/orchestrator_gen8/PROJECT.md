# Project: RecruitOps Reporting & Analytics Dashboard Flow (Person A - Flow 2)

## Architecture
- Backend: .NET 10 LTS Clean Architecture (`RecruitOps.Api`, `RecruitOps.Application`, `RecruitOps.Domain`, `RecruitOps.Infrastructure`).
- Frontend: React + Vite + TypeScript (`frontend/internal`), Tailwind CSS, `@recruitops/types`, `@recruitops/internal`.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | KPI Metrics API | `GET /api/analytics/kpis` (Time-to-Hire, Active Jobs, Applicants, Hire Rate) | M1 | ORIGINAL_REQUEST |
| 2 | Time-to-Hire API | `GET /api/analytics/time-to-hire` (Stage duration, department/posting breakdown) | M1 | ORIGINAL_REQUEST |
| 3 | Stage Funnel API | `GET /api/analytics/conversion` (Funnel counts & drop-off %) | M1 | ORIGINAL_REQUEST |
| 4 | Source of Hire API | `GET /api/analytics/source-of-hire` (Source distribution) | M1 | ORIGINAL_REQUEST |
| 5 | Department Reach Scoping | Enforce ADR-0003 scoping on all analytics queries | M1 | ORIGINAL_REQUEST |
| 6 | Custom Report Query API | `POST /api/analytics/reports/query` (Filtered tabular report query) | M2 | ORIGINAL_REQUEST |
| 7 | CSV Export API | `GET /api/analytics/reports/export` (Download CSV report) | M2 | ORIGINAL_REQUEST |
| 8 | Analytics Page Route | `/analytics` route (`pages/AnalyticsPage.tsx`) | M3 | ORIGINAL_REQUEST |
| 9 | KPI Cards UI | Visual KPI summary cards | M3 | ORIGINAL_REQUEST |
| 10 | Visual Charts UI | Time-to-Hire, Stage Funnel, Source Distribution charts | M3 | ORIGINAL_REQUEST |
| 11 | Report Builder UI | Custom report column/filter selector, live preview, CSV export | M3 | ORIGINAL_REQUEST |
| 12 | Navigation Integration | Permission-aware "Analytics" link in AppLayout sidebar | M3 | ORIGINAL_REQUEST |
| 13 | Full Suite Verification | 369+8 backend tests, 256+5 frontend tests, 0 typecheck errors | M4 | ORIGINAL_REQUEST |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Milestone 1: Backend Analytics APIs | R1 endpoints & ADR-0003 scoping | none | DONE |
| 2 | Milestone 2: Custom Report & CSV Export API | R2 report query & CSV export | M1 | DONE |
| 3 | Milestone 3: Analytics Dashboard & Report UI | R3 `/analytics` page & UI components | M1, M2 | DONE |
| 4 | Milestone 4: E2E Verification & Quality Audit | Full test suite & Forensic audit | M1, M2, M3 | PLANNED |

## Interface Contracts
- `GET /api/analytics/kpis`: returns `{ avgTimeToHireDays: number, activeRequisitions: number, totalApplications: number, overallHireRate: number }`
- `GET /api/analytics/time-to-hire`: returns `{ stageDurations: [{ stage: string, avgDays: number }], departmentBreakdown: [...], postingBreakdown: [...] }`
- `GET /api/analytics/conversion`: returns `{ funnel: [{ stage: string, count: number, dropOffRate: number }] }`
- `GET /api/analytics/source-of-hire`: returns `{ sources: [{ source: string, count: number, percentage: number }] }`
- `POST /api/analytics/reports/query`: accepts `{ dateFrom?: string, dateTo?: string, departmentId?: string, jobPostingId?: string, stages?: string[], columns?: string[] }`, returns `{ headers: string[], rows: Record<string, any>[] }`
- `GET /api/analytics/reports/export`: generates text/csv file stream.

## Code Layout
- Backend:
  - `backend/src/Api/Controllers/AnalyticsController.cs`
  - `backend/src/Application/Analytics/` / `DTOs/` (Queries, DTOs, Handlers, Services)
  - `backend/src/Infrastructure/Services/AnalyticsService.cs`
- Frontend:
  - `frontend/internal/src/pages/AnalyticsPage.tsx`
  - `frontend/internal/src/features/analytics/` (components: `KpiCardSection.tsx`, `TimeToHireChart.tsx`, `FunnelChart.tsx`, `SourceDistributionChart.tsx`, `CustomReportBuilder.tsx`)
  - `packages/types/src/analytics.ts`
