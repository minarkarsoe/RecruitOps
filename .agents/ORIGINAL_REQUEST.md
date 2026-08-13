# Original User Request

## Initial Request — 2026-08-11T22:03:46Z

Person B - Flow 2: Build the complete AI Integration Flow (5 Endpoints End-to-End) for RecruitOps per ADR-0008. This includes provider-agnostic Claude (Data Analysis / Smart Match) and Gemini (Doc Gen / Localization) API clients with API-key gating, backend AI endpoints, and rich frontend UI in CandidateSlideOver, Document Prep Modal, and Burmese translation.

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Integrity mode: development

Reference material:
- `docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md` — AI optional, API-key gated, human confirmation mandatory
- `docs/decisions/ADR-0009-myanmar-script-handling.md` — Burmese ↔ English AI translation and script handling
- `CLAUDE.md` — Clean Architecture, TypeScript types, build/test commands

## Verification Resources

Existing test suite baseline that MUST remain green:
- Backend: `dotnet test backend/RecruitOps.sln` — **411 tests passing** (51 Domain + 360 Api)
- Frontend: `npm run test` in `frontend/internal` — **295 tests passing**
- Typecheck: `npm run typecheck` — **0 errors** across all workspaces

## Requirements

### R1. AI Provider Abstraction & API Key Gating Backend
Build backend services in `RecruitOps.Infrastructure` & `RecruitOps.Application`:
- Provider-agnostic interfaces `IAiIntegrationService`, `IClaudeService`, `IGeminiService`.
- Implement Claude API client for Data Analysis:
  - `POST /api/ai/parse-resume`: Extracted text → structured candidate JSON
  - `POST /api/ai/match-candidate`: Candidate vs. Job Posting match scoring (0-100), criteria compatibility breakdown, suggested interview questions
- Implement Gemini API client for Document Generation & Localization:
  - `POST /api/ai/executive-summary`: Candidate profile executive summary
  - `POST /api/ai/document-prep`: Interview Kit / Client Dossier document generation
  - `POST /api/ai/translate`: Burmese ↔ English text localization
- API Key Gating: If no API key is configured in environment/secrets, endpoints return explicit `402 Payment Required` or feature-disabled response without throwing 500 errors.

### R2. Smart Match & Executive Summary UI in Candidate 360
Build frontend components inside `@recruitops/internal`:
- Enhance `CandidateSlideOver.tsx`:
  - **Smart Match Badge & Breakdown:** Match score badge (e.g. "85% Match"), detailed criteria breakdown drawer, suggested interview questions list.
  - **Executive Summary Panel:** "Generate AI Summary" button, EN / MY / Bilingual language toggle, copy text / export buttons.

### R3. AI Document Prep Modal & Burmese Localization UI
Build frontend components inside `@recruitops/internal`:
- Add `AiDocumentPrepModal.tsx` on Candidate 360 / Job Posting pages allowing recruiters to generate and preview Interview Kits / Dossiers.
- Add inline "Translate (EN ↔ MY)" button on long text fields (Job Descriptions, Candidate Notes).

## Acceptance Criteria

### Backend Criteria
- [ ] 5 AI endpoints (`parse-resume`, `match-candidate`, `executive-summary`, `document-prep`, `translate`) execute cleanly with mock/real provider response
- [ ] If API keys are unconfigured, endpoints gracefully return feature-disabled status without 500 server crashes
- [ ] Extracted AI structured data requires explicit human review/confirmation before mutating database records (ADR-0008)
- [ ] All **411 existing backend tests** pass cleanly (`dotnet test backend/RecruitOps.sln`)
- [ ] At least 10 new backend tests covering AI provider client mocking, API key gating fallback, match scoring calculation, and translation endpoints

### Frontend Criteria
- [ ] `CandidateSlideOver.tsx` renders AI Match Score badge, criteria breakdown, and suggested interview questions
- [ ] Executive Summary panel generates summary with language switcher
- [ ] Document Prep Modal generates Interview Kit preview
- [ ] `npm run typecheck` passes with **0 errors** across all workspaces
- [ ] All **295 existing frontend tests** pass cleanly (`npm run test` in `frontend/internal`)
- [ ] At least 6 new frontend Vitest tests covering AI component interactions, loading states, and error handling

---
*Cross-cutting: Maintain Clean Architecture principles, full TypeScript & C# types alignment.*

## Follow-up — 2026-08-12T19:48:19Z

Person B - Flow 3: Build the complete Deployment & Operational Readiness Flow (End-to-End) for RecruitOps. This includes multi-container `docker-compose.yml` production setup (PostgreSQL with `pg_trgm`, MinIO S3 storage, .NET 10 API, Internal Frontend, Public Portal), backend `/healthz` health check endpoint, rate-limiting & security headers middleware, automated EF Core startup database migrations & RBAC seed verification, and production build checks.

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Integrity mode: development

Reference material:
- `docker-compose.yml` — Multi-container compose configuration
- `docs/decisions/ADR-0013-infrastructure-and-storage.md` — Cloudflare R2 / MinIO storage configuration
- `docs/decisions/ADR-0016-login-brute-force-protection.md` — Rate limiting and brute-force protection
- `CLAUDE.md` — Clean Architecture, TypeScript types, build/test commands

## Verification Resources

Existing test suite baseline that MUST remain green:
- Backend: `dotnet test backend/RecruitOps.sln` — **454 tests passing** (51 Domain + 403 Api)
- Frontend: `npm run test` in `frontend/internal` — **318 tests passing**
- Typecheck: `npm run typecheck` — **0 errors** across all workspaces

## Requirements

### R1. Health Check Endpoint & Operational Monitoring Backend
Build operational endpoints in `RecruitOps.Api`:
- Endpoint `GET /healthz` returning 200 OK with detailed health status:
  - Database connectivity (PostgreSQL query check)
  - Object storage connectivity (`IFileStorage` bucket check)
  - Memory usage & uptime metrics
- Add ASP.NET Core Rate Limiting middleware to prevent brute-force attacks on `POST /api/auth/login` and `POST /api/public/applications` (10 requests / min limit per IP).
- Add security headers middleware (X-Content-Type-Options: nosniff, X-Frame-Options: DENY, Referrer-Policy: strict-origin-when-cross-origin, Content-Security-Policy).

### R2. Automated DB Migrations & Production Seeding
Enhance application startup flow in `Program.cs` / `DependencyInjection.cs`:
- Automated EF Core database migration check on application startup (applies pending migrations cleanly without data loss).
- Ensure idempotent execution of `RbacSeedData.cs` initializing default tenant, system roles, permissions, and initial SuperAdmin account.

### R3. Multi-Container Docker Compose & Production Build Verification
Verify multi-container deployment setup:
- Update `docker-compose.yml` defining services:
  - `db`: PostgreSQL 16 with `pg_trgm` pre-initialized
  - `storage`: MinIO S3-compatible object storage with auto-created `recruitops-cvs` bucket
  - `backend`: .NET 10 Web API built via multi-stage Dockerfile
  - `frontend-internal`: React CRM frontend
  - `frontend-public`: Public Career Portal
- Verify `docker compose up --build` config without missing env vars or broken network alias links.

## Acceptance Criteria

### Backend Criteria
- [ ] `GET /healthz` returns HTTP 200 with DB and Storage health status
- [ ] Rate limiting middleware blocks excessive requests (>10 reqs/min) on `/api/auth/login` with 429 Too Many Requests
- [ ] Security headers are present on all HTTP API responses
- [ ] EF Core startup migration applies cleanly without throwing exceptions
- [ ] All **454 existing backend tests** pass cleanly (`dotnet test backend/RecruitOps.sln`)
- [ ] At least 8 new backend tests covering `/healthz` endpoint, rate limiting middleware, and security headers

### Frontend & Build Criteria
- [ ] `npm run typecheck` passes with **0 errors** across all 4 workspaces (`@recruitops/internal`, `@recruitops/public`, `@recruitops/types`, `@recruitops/ui`)
- [ ] All **318 existing frontend tests** pass cleanly (`npm run test` in `frontend/internal`)
- [ ] `docker-compose.yml` parses cleanly without syntax errors
- [ ] Production frontend bundle builds cleanly (`npm run build` in `@recruitops/internal` and `@recruitops/public`)

---
*Cross-cutting: Maintain Clean Architecture principles, full TypeScript & C# types alignment.*

