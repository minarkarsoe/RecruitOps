# Project: RecruitOps Dynamic RBAC & Audit Remediation

## Architecture
- **Backend Architecture**: Clean Architecture (.NET 10) across `Domain`, `Application`, `Infrastructure`, and `Api`.
- **Frontend Architecture**: Vite + React SPA (`frontend/internal`), Next.js SSR (`frontend/public`).
- **Data Layer**: PostgreSQL with Entity Framework Core 10, global query filters for `ITenantScoped`.

## Milestones

| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Audit Findings Remediation & Security Upgrades | R1: `UsersController.cs` in-memory projection fix, `AuthLoginTests.cs` bearer token assertion fix, `System.Security.Cryptography.Xml` upgrade, loose HTTP status assertion cleanup | None | DONE |
| 2 | Granular Dynamic RBAC Data Model & Migration | R2: Domain entities (Role, Permission, RolePermission), Super-Admin cross-tenant concept, seeds/migrations, backwards-compatibility mapping | M1 | DONE |
| 3 | Dynamic Permission Evaluator Engine & Backend APIs | R3: Dynamic permission authorization handler/policy, Roles & Permissions CRUD endpoints, User Management endpoints (GET, POST, PUT, deactivate, reactivate, role assignment) | M2 | DONE |
| 4 | Frontend User Management & Role Builder UI | R4: User Management UI, Role Builder permission matrix grid, Super-Admin dashboard in `frontend/internal` | M3 | DONE |
| 5 | Permission-Aware UX, Documentation & E2E Verification | R5 & R6: Dynamic permission UI adaptivity, doc updates (`CLAUDE.md`, `FEATURE-STATUS.md`, `NEXT-SESSION.md`, `CHANGELOG.md`), API integration test expansion, frontend test suite execution, forensic audit | M4 | DONE |

## Interface Contracts
### Dynamic RBAC & Permission Matrix
- Permission Claims: `permission:<module>:<feature>:<action>`
- Special Actions: `Approve`, `Publish`, `Cancel`, `BlindEvaluation`
- Super-Admin: Bypass tenant scoping or cross-tenant scope (`IsSuperAdmin` / TenantId null or `*`).

## Code Layout
- Backend: `backend/src/Domain`, `backend/src/Application`, `backend/src/Infrastructure`, `backend/src/Api`
- Backend Tests: `backend/tests/RecruitOps.Api.Tests`, `backend/tests/RecruitOps.Domain.Tests`
- Frontend Internal: `frontend/internal`
- Frontend Public: `frontend/public`
- Docs: `docs/status/` and root `CLAUDE.md`
