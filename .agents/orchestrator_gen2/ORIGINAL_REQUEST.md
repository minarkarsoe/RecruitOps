# Original User Request

## 2026-07-29T16:09:47Z

You are the Project Orchestrator for the RecruitOps project.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator_gen2
The project root directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Read the verbatim user request in .agents/ORIGINAL_REQUEST.md (specifically the latest request under timestamp 2026-07-29T16:09:47Z).
Refer to existing audit findings in .agents/orchestrator/FINDINGS_REPORT.md and audit_results.md.

Your objective:
Lead the team to fix all audit findings, implement Dynamic RBAC & Super-Admin capabilities, User Management CRUD, Role Builder UI, permission-aware UX, expand tests, and update all project documentation (CLAUDE.md, docs/status/FEATURE-STATUS.md, docs/status/NEXT-SESSION.md, docs/status/CHANGELOG.md).

Requirements Breakdown:
- R1. Audit Findings Remediation:
  - Critical: GET /api/users in UsersController.cs projected in-memory after SQL fetch to prevent enum.ToString() translation failure under PostgreSQL.
  - Important: Update AuthLoginTests.cs (Issued_Token_Grants_Access_To_Protected_Endpoint) to send authenticated HTTP request using bearer token.
  - Security & Maintenance: Upgrade System.Security.Cryptography.Xml (NU1903), fix loose HTTP status assertions.
- R2. Granular Dynamic RBAC Data Model & Domain:
  - Super-Admin role (cross-tenant system owner).
  - Dynamic Roles & Custom Permissions (Modules, Features, CRUD operations, Special Actions: Approve, Publish, Cancel, Blind Evaluation).
  - Migration & backwards compatibility (map existing default roles to standard pre-configured role-permission definitions).
- R3. Permission Evaluation Engine & Backend APIs:
  - Dynamic claim/DB dynamic permission evaluator middleware/policy handlers.
  - Roles & Permissions CRUD endpoints.
  - User Account Management Endpoints (GET, POST, PUT, deactivate, reactivate, assign roles).
- R4. Frontend User Management & Role Builder UI (frontend/internal):
  - User Management screen (list, create, edit, deactivate, assign roles).
  - Role Builder & Permission Grid UI (dynamic matrix UI).
  - Super-Admin Dashboard (cross-tenant view/settings).
- R5. Permission-Aware Frontend UX & Documentation Maintenance:
  - Dynamic UI adaptivity based on caller permissions.
  - Update CLAUDE.md, docs/status/FEATURE-STATUS.md, docs/status/NEXT-SESSION.md, docs/status/CHANGELOG.md.
- R6. Verification & Test Suite Expansion:
  - Expand RecruitOps.Api.Tests for Dynamic RBAC, User CRUD, Role Builder, Super-Admin.
  - Ensure all backend & frontend tests pass (dotnet test, npm run typecheck, vitest).
