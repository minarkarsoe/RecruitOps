# Project: RecruitOps Comprehensive Audit & End-to-End Verification

## Architecture
- Backend: .NET 10 (Clean Architecture: Domain, Application, Infrastructure, Api), PostgreSQL, Docker Compose
- Frontends:
  - Internal SPA (`frontend/internal`): Vite + React + TypeScript
  - Public SSR app (`frontend/public`): Next.js + React + TypeScript
- Auth: JWT + RBAC (Admin, HrDirector, Recruiter, HiringManager, Approver)
- Multi-tenancy: Query filters for tenant isolation
- Scope: Modules 1-3 (Module 1: Requisition & Approval, Module 2: ATS & Sourcing, Module 3: Interview & Assessment)

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Existing Test Suite & Typecheck Validation | Run 169 backend tests, 27 frontend Vitest tests, `npm run typecheck`, inspect assertion quality | none | DONE |
| 2 | Backend API Audit & Data Integrity | Audit authorization matrix, business logic, tenant isolation, department scoping, known gaps (R1) | M1 | DONE |
| 3 | Frontend UI Workflow & Behavior Verification | Verify internal SPA flows, public app flows, and 3 specific UI gaps (panel picker, blind state, .mention) (R2) | M1 | DONE |
| 4 | End-to-End Integration Testing | Write and execute API integration tests for full user journey from requisition to scorecard & stage history (R4) | M2, M3 | DONE |
| 5 | Gap Analysis & Findings Report | Synthesize all findings into structured report (🔴/🟡/🟢), update known gaps status, production recommendations (R5) | M1, M2, M3, M4 | DONE |

## Interface Contracts
- Backend API endpoints: REST API `/api/...` returning JSON payloads
- Auth Header: `Authorization: Bearer <jwt_token>`
- Tenant Isolation: TenantId claims in JWT claims & EF Core global query filters

## Code Layout
- Backend: `backend/src/` (Domain, Application, Infrastructure, Api), `backend/tests/`
- Frontend Internal: `frontend/internal/src/`
- Frontend Public: `frontend/public/src/`
- Documentation: `docs/status/FEATURE-STATUS.md`, `docs/status/NEXT-SESSION.md`, `CLAUDE.md`
- Agent Workspace: `.agents/`
