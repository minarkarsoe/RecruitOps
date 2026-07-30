# Execution Plan — RecruitOps Comprehensive Audit & Verification

## Overview
This plan details the multi-milestone orchestration strategy for auditing and end-to-end verifying RecruitOps Modules 1-3.

## Milestones & Strategy

### Milestone 1: Existing Test Suite & Typecheck Validation (R3)
- **Objective**: Execute existing backend test suite (169 tests), frontend Vitest suite (27 tests), and TypeScript typecheck (`npm run typecheck`). Analyze test code for assertion quality and potential false positives (e.g. testing mocks instead of implementation, always-true assertions).
- **Execution Plan**:
  1. Dispatch `teamwork_preview_worker` to execute backend tests, frontend tests, and typecheck commands.
  2. Dispatch `teamwork_preview_explorer` to inspect test source files (`backend/tests`, `frontend/internal/src/**/*.test.*`, etc.) for assertion strength, mock reliance, and coverage gaps.
  3. Dispatch `teamwork_preview_reviewer` and `teamwork_preview_auditor` to audit test execution results and assertion quality.

### Milestone 2: Backend API Audit & Data Integrity Verification (R1)
- **Objective**: Conduct systematic backend audit across Modules 1-3.
- **Scope**:
  - Authorization & RBAC (Admin, HrDirector, Recruiter, HiringManager, Approver; HTTP 401, 403, 404 behavior for out-of-scope/tenant data).
  - Business logic flows (requisition approval chain sequentiality, posting dependency on approved requisition, blind scoring enforcement, stage history logging).
  - Data integrity & multi-tenancy (tenant isolation query filters, department scoping, candidate dedup logic, custom form JSON schema validation).
  - Evaluation of known gaps from `FEATURE-STATUS.md` (e.g., `GET /api/users` enum.ToString() query translation on Postgres).
- **Execution Plan**:
  1. Dispatch `teamwork_preview_explorer` to analyze API endpoints, controllers, authorization attributes, query filters, and business handlers.
  2. Dispatch `teamwork_preview_worker` to execute targeted verification commands / API requests.
  3. Dispatch `teamwork_preview_reviewer` and `teamwork_preview_auditor` to verify findings.

### Milestone 3: Frontend UI Workflow & Behavior Verification (R2)
- **Objective**: Verify internal SPA (`frontend/internal`) and public SSR (`frontend/public`) workflows and the 3 un-eyeballed UI gaps.
- **Scope**:
  - Internal SPA flows (Login -> Requisition -> Submit Approval -> Inbox -> Approve/Reject -> Posting Creation -> Pipeline -> Interview Scheduling -> Scorecard -> Notes).
  - Public app flows (Public job detail page rendering, custom application form submission, Open Graph meta tags).
  - 3 Specific UI Gaps:
    a) Panel picker populated as Recruiter
    b) Blind state on interview detail view
    c) `.mention` CSS styling surviving Tailwind production build
- **Execution Plan**:
  1. Dispatch `teamwork_preview_explorer` to inspect frontend component code, route handlers, state management, and CSS builds.
  2. Dispatch `teamwork_preview_worker` to run frontend tests, linting, build checks, and component behavior verification.
  3. Dispatch `teamwork_preview_reviewer` and `teamwork_preview_auditor` to verify UI compliance.

### Milestone 4: End-to-End Integration Testing (R4)
- **Objective**: Implement and execute full multi-module API-level integration tests covering the complete candidate life cycle.
- **Scope**:
  1. Admin setup: department & assignment.
  2. HiringManager: requisition creation & approval submission.
  3. Approver: requisition approval.
  4. Recruiter: job posting creation & publishing.
  5. Anonymous candidate: view public job & submit application with custom fields.
  6. Recruiter: view pipeline, advance application stage, schedule interview, assign panel.
  7. Panel member: submit blind scorecard, add notes with @mentions.
  8. Full audit of stage history timeline completeness and candidate deduplication (differing phone formats).
- **Execution Plan**:
  1. Dispatch `teamwork_preview_explorer` to design integration test scenarios and assert points.
  2. Dispatch `teamwork_preview_worker` to write and run the integration test suite.
  3. Dispatch `teamwork_preview_reviewer`, `teamwork_preview_challenger`, and `teamwork_preview_auditor` to verify correctness and integrity.

### Milestone 5: Consolidated Gap Analysis & Production-Readiness Findings Report (R5)
- **Objective**: Synthesize all verification data, security/business logic audits, and test results into a production-grade findings report.
- **Outputs**:
  - Categorized findings with severity badges: 🔴 Critical, 🟡 Important, 🟢 Minor.
  - Status updates for all Known Gaps listed in `FEATURE-STATUS.md`.
  - Concrete pre-production remediation steps and recommendations.
