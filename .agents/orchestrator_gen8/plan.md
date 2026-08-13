# Execution Plan - Person A Flow 2: Reporting & Analytics Dashboard Flow (End-to-End)

## Overview
Build complete Reporting & Analytics Dashboard Flow for RecruitOps, spanning backend analytics endpoints, custom report builder and CSV export API, frontend Analytics Dashboard page (`/analytics`), custom report builder UI, and end-to-end quality verification & forensic audit.

## Milestones

### Milestone 1: R1 Analytics & Metrics Backend APIs
- **Goal**: Implement backend services and REST API endpoints in `RecruitOps.Api` & `RecruitOps.Application`.
- **Endpoints**:
  - `GET /api/analytics/kpis`: Average Time-to-Hire, Active Requisitions, Total Applications, Overall Hire Rate derived from `ApplicationStageHistory`.
  - `GET /api/analytics/time-to-hire`: Average time spent per pipeline stage and breakdown by department / job posting.
  - `GET /api/analytics/conversion`: Pipeline stage conversion funnel counts and drop-off percentages.
  - `GET /api/analytics/source-of-hire`: Candidate application source distribution (Public Page, Referral, Sourced, Multi-Channel).
- **Constraints**:
  - Department Reach Scoping (ADR-0003): Hiring Managers only see metrics within their department scope.
  - Backend tests: Maintain 369 existing tests passing + add unit/integration tests covering analytics query calculations and stage history duration metrics.
- **Workflow**:
  - Step 1.1: Survey codebase and existing `ApplicationStageHistory` entity/service using Explorer.
  - Step 1.2: Worker implements analytics DTOs, queries, handlers, and endpoints.
  - Step 1.3: Reviewers & Challengers review and test backend endpoints and ADR-0003 scoping.
  - Step 1.4: Forensic Auditor checks for genuine logic (no hardcoded responses).

### Milestone 2: R2 Custom Report Builder & CSV Export API
- **Goal**: Implement custom report builder query endpoint and CSV export generator API.
- **Endpoints**:
  - `POST /api/analytics/reports/query`: Execute custom parameter queries (date range, department, posting, stage) returning tabular data.
  - `GET /api/analytics/reports/export`: Generate and download formatted CSV reports.
- **Constraints**:
  - Enforce Department Reach Scoping (ADR-0003).
  - Add backend tests for custom query filters and CSV export output generation.
- **Workflow**:
  - Step 2.1: Explorer investigates report query parameters and CSV formatting requirements.
  - Step 2.2: Worker implements report builder query service, CSV export generator, and API endpoints.
  - Step 2.3: Reviewers & Challengers verify custom report query logic, CSV formatting, and scoping.
  - Step 2.4: Forensic Auditor conducts integrity check.

### Milestone 3: R3 Analytics Dashboard Page & Report Builder UI
- **Goal**: Build frontend dashboard page and components in `@recruitops/internal`.
- **UI Components**:
  - Route `/analytics` (`pages/AnalyticsPage.tsx`) in `@recruitops/internal`.
  - Visual KPI Cards (Time-to-Hire, Active Jobs, Total Applicants, Hired Count).
  - Time-to-Hire Line/Bar Chart & Pipeline Stage Funnel visualization.
  - Source Distribution Chart (Source of Hire).
  - Custom Report Builder component (column/filter selection, live table preview, CSV export button).
  - Navigation sidebar updated to include permission-aware "Analytics" link (HR Director, Admin, Recruiter, Hiring Manager).
- **Constraints**:
  - Full TypeScript safety (`npm run typecheck` 0 errors).
  - Frontend Vitest tests (256 existing + >=5 new tests covering AnalyticsPage rendering and report builder CSV export).
- **Workflow**:
  - Step 3.1: Explorer investigates existing UI design primitives and charts.
  - Step 3.2: Worker implements `AnalyticsPage.tsx`, components, API client integrations, and sidebar link.
  - Step 3.3: Reviewers & Challengers verify component rendering, state, permissions, and Vitest tests.
  - Step 3.4: Forensic Auditor conducts integrity check.

### Milestone 4: End-to-End Verification & Quality Audit
- **Goal**: Final quality verification and forensic audit across backend and frontend.
- **Verification Gates**:
  - Backend: 369 existing + 8 new tests = 377+ tests passing (`dotnet test backend/RecruitOps.sln`).
  - Frontend: 256 existing + 5 new tests = 261+ tests passing (`npm run test` in `frontend/internal`).
  - Typecheck: 0 errors across all workspaces (`npm run typecheck`).
  - Forensic Audit: VICTORY CONFIRMED (0 cheating, 0 hardcoding, clean implementation).
