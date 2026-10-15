# Project: RecruitOps Person B - Flow 3 Execution Plan

## Architecture
- Backend: .NET 10 Web API (`RecruitOps.Api`, `RecruitOps.Infrastructure`, `RecruitOps.Application`, `RecruitOps.Domain`)
- Database & Storage: PostgreSQL 16 (`pg_trgm`), MinIO / Cloudflare R2 (`IFileStorage`)
- Middleware: ASP.NET Core Rate Limiting (`POST /api/auth/login`, `POST /api/public/applications`), Security Headers Middleware
- Operations: Automated EF Core startup migrations, idempotent `RbacSeedData.cs`, `/healthz` health check endpoint
- Multi-Container Orchestration: `docker-compose.yml` for db, storage, backend, frontend-internal, frontend-public

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | GET /healthz endpoint | Returns 200 OK with PostgreSQL, IFileStorage, memory usage & uptime metrics | M1 | Follow-up R1 |
| 2 | Rate Limiting Middleware | 10 reqs/min per IP limit on login & application submission endpoints | M1 | Follow-up R1 |
| 3 | Security Headers Middleware | Applies X-Content-Type-Options, X-Frame-Options, Referrer-Policy, CSP | M1 | Follow-up R1 |
| 4 | Automated DB Startup Migration | Applies pending EF Core migrations cleanly on startup in Program.cs/DependencyInjection.cs | M2 | Follow-up R2 |
| 5 | Idempotent RBAC Seeding | Ensures RbacSeedData runs cleanly without duplicates or data corruption | M2 | Follow-up R2 |
| 6 | Multi-Container Docker Compose | Updates docker-compose.yml with db (pg_trgm), storage (MinIO bucket auto-create), backend, frontend-internal, frontend-public | M3 | Follow-up R3 |
| 7 | Verification & Forensic Audit | Verification of all backend (454 + 8+ new), frontend (318), typecheck (0 errors), build tests, and forensic audit | M4 | Follow-up Verification |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Codebase Survey & Planning | Survey existing codebase state for health checks, middleware, EF migrations, docker-compose | None | DONE |
| 2 | M1: Backend Health Check & Security Middleware & Startup Seeding | /healthz, Rate Limiting (10 reqs/min), Security Headers, unconditional RBAC startup seeding, and 8+ new backend tests | Milestone 1 | DONE |
| 3 | M2: Multi-Container Docker Compose Setup | Updated docker-compose.yml for db (postgres:16 + pg_trgm), storage (recruitops-cvs bucket), backend, frontend-internal, frontend-public, and init-db.sql | Milestone 1 | DONE |
| 4 | M3: Verification, Test Execution & Forensic Audit Gate | Run 454+8+ backend tests, 318 frontend tests, typecheck, production builds, docker-compose syntax, and forensic audit | Milestones 2, 3 | DONE |

## Interface Contracts & Configuration
- `/healthz` response payload schema:
  - `status`: "Healthy" | "Degraded" | "Unhealthy"
  - `checks`: DB connectivity, Storage bucket check
  - `metrics`: Memory usage (MB), Uptime (seconds/duration)
- Rate Limiting rules:
  - Partition key: Remote IP address
  - Limit: 10 requests per 1 minute window
  - Targeted endpoints: `POST /api/auth/login`, `POST /api/public/applications`
  - Rejection code: `429 Too Many Requests`
- Security Headers:
  - `X-Content-Type-Options`: `nosniff`
  - `X-Frame-Options`: `DENY`
  - `Referrer-Policy`: `strict-origin-when-cross-origin`
  - `Content-Security-Policy`: configured policy header
