# Project Plan: RecruitOps Dynamic RBAC & Audit Remediation

## Executive Overview
The objective of this project is to fix security audit findings, implement a dynamic RBAC domain model with custom roles and fine-grained permissions, build backend authorization handlers and management APIs, create frontend SPA User Management & Role Builder UIs in `frontend/internal`, enforce permission-aware UX adaptations, maintain documentation (`CLAUDE.md`, `FEATURE-STATUS.md`, `NEXT-SESSION.md`, `CHANGELOG.md`), expand tests, and verify overall product integrity.

## Milestone Decomposition

### Milestone 1: Audit Findings Remediation & Security Upgrades (COMPLETED)
- R1.1: GET `/api/users` PostgreSQL `enum.ToString` LINQ translation fix (in-memory projection).
- R1.2: `AuthLoginTests.cs` bearer token test assertion fix.
- R1.3: `System.Security.Cryptography.Xml` package upgrade & loose HTTP status assertion cleanup.
- Status: COMPLETED & VERIFIED CLEAN.

### Milestone 2: Granular Dynamic RBAC Data Model & Migration (COMPLETED)
- R2.1: Domain Entities (`Role`, `Permission`, `RolePermission`, `User` updating to CustomRole).
- R2.2: Infrastructure configuration & migrations for PostgreSQL (`AppDbContext`, `RbacSeedData`).
- R2.3: Super-Admin cross-tenant capabilities & canonical permission hierarchy.
- Status: COMPLETED & VERIFIED CLEAN.

### Milestone 3: Dynamic Permission Evaluator Engine & Backend APIs (IN_PROGRESS)
- R3.1: Dynamic Permission Evaluation Engine (`HasPermission` policy handler / middleware / requirement).
- R3.2: Roles & Permissions Management REST APIs (`GET /api/permissions`, `GET /api/roles`, `GET /api/roles/{id}`, `POST /api/roles`, `PUT /api/roles/{id}`, `DELETE /api/roles/{id}`).
- R3.3: User Account Management CRUD APIs (`GET /api/users` with pagination/search/role filters, `POST /api/users`, `PUT /api/users/{id}`, deactivate, reactivate, assign role).
- Steps:
  1. Explorers 1 & 3 explore authorization engine details and User Management API design.
  2. Worker implements Authorization Policy Handler, RolesService, UsersService, RolesController, UsersController endpoints.
  3. Reviewers verify code quality, DTO contracts, system role immutability safeguards, and EF Core query translation.
  4. Challenger tests API authorization boundaries, edge cases, system role protection, and user management CRUD operations.
  5. Forensic Auditor checks for authentic implementation and zero cheating.

### Milestone 4: Frontend User Management, Role Builder & Super-Admin UI (PLANNED)
- R4.1: User Management Screen in `frontend/internal` (user table, search/filter, create user modal, edit user modal, role assignment, active/inactive toggle).
- R4.2: Role Builder Matrix UI in `frontend/internal` (permission matrix grid grouped by module/feature, custom role creation/editing modal, system role view-only protection).
- R4.3: Super-Admin Tenant Switching / Management Views in `frontend/internal`.
- Steps:
  1. Explorer analyzes UI component hierarchy, TypeScript types, API integration points, and state management.
  2. Worker implements React components, pages, routes, and services in `frontend/internal`.
  3. Reviewers verify UI code, TypeScript types (`npm run typecheck`), and Vitest specs.
  4. Challenger tests frontend workflows and interactions.
  5. Forensic Auditor verifies UI integration authenticity.

### Milestone 5: Permission-Aware UX, Documentation & E2E Verification (PLANNED)
- R5.1: Dynamic UX Adaptivity (sidebar menu item visibility based on permissions, action button enabling/disabling).
- R5.2: Documentation Maintenance (`CLAUDE.md`, `FEATURE-STATUS.md`, `NEXT-SESSION.md`, `CHANGELOG.md`).
- R5.3: Backend Integration Test Expansion (`RecruitOps.Api.Tests` - RolesController, UsersController, Authorization tests).
- R5.4: Comprehensive Verification (`dotnet test`, `npm run typecheck`, `npm run test` in frontend/internal).
- R5.5: Final Forensic Audit.
