# Original User Request

## Initial Request — 2026-08-03T10:43:38Z

Project Goal: Refactor the RecruitOps frontend into a modern, high-density Recruit CRM (Ashby / Linear-style) experience with sleek UI components, high-density scannable layouts, slide-over detail drawers, and a clean Feature-Based (Domain-Driven) Frontend Architecture.

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Integrity mode: development

## Requirements

### R1. Design System & UI Primitive Library (packages/ui & frontend/internal/src/components/ui)
Upgrade Tailwind configuration and typography in packages/ui/tailwind-preset.js and frontend/internal/src/index.css (Bricolage Grotesque & Inter fonts, Zinc neutrals, Cyan/Teal primary brand tokens, semantic status badges). Build reusable primitive components in packages/ui or src/components/ui: Sheet/Drawer (slide-over panel), Badge, Table, CommandPalette (Ctrl+K), Dialog, Tabs, Skeleton, Input, Select.

### R2. Application Layout & Global Navigation
Redesign AppLayout.tsx with a sleek collateral sidebar, header breadcrumbs, global Ctrl+K search command palette, department/user switcher, and permission-aware action buttons.

### R3. Feature-Based Architecture Refactor (frontend/internal/src/features)
Reorganize frontend code into feature modules:
- src/features/requisitions: RequisitionTable, RequisitionDrawer, useRequisitions hook.
- src/features/pipeline: PipelineKanbanBoard, CandidateSlideOver (360 profile drawer with CV viewer, stage history, scorecard summaries, notes), usePipeline hook.
- src/features/interviews: BlindScorecardDrawer (split view 1-5 rating, @Mentions note thread), useInterviews hook.

## Acceptance Criteria

### Verification & Quality Guardrails
- [ ] `npm run typecheck` passes clean across all workspaces with 0 TypeScript errors.
- [ ] `npm run test` in `frontend/internal` passes clean (all 60+ Vitest tests passing).
- [ ] Candidate 360 profile opens instantly via Slide-Over Drawer without full page refresh.
- [ ] Global Ctrl+K Command Palette opens and allows searching & navigation.

## Follow-up — 2026-08-06T13:12:10Z

# RecruitOps Project Refactor & Hybrid AI Integration Plan

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Integrity mode: development

## Requirements

### R1. Complete Frontend CRM Features & UI Primitives
Complete feature modules in `frontend/internal/src/features/`:
- `requisitions`: RequisitionTable, RequisitionDrawer, useRequisitions hook.
- `pipeline`: PipelineKanbanBoard, CandidateSlideOver (360 profile drawer with CV viewer, stage history, scorecard summaries, notes), usePipeline hook.
- `interviews`: BlindScorecardDrawer (split view 1-5 rating, @Mentions note thread), useInterviews hook.

### R2. Dual Surface & Design System Compliance
Ensure strict compliance with `RecruitOps_Design_System.md` ("Clear Pipeline"):
- Bricolage Grotesque & Inter fonts with Noto Sans Myanmar fallback (line-height >= 1.7).
- Status pills, Pipeline stage rails, Client portal cards, and Expiry attention cards.

### R3. Hybrid AI API Integration
Set up API routes:
- Claude API endpoint for Resume Parsing, Structuring, and Candidate Matching data analysis.
- Gemini API endpoint for Document Preparation, Executive Summaries, and Burmese Localization.

## Acceptance Criteria

### Quality & Verification Guardrails
- [ ] `npm run typecheck` passes cleanly across all workspaces with 0 TypeScript errors.
- [ ] `npm run test` in `frontend/internal` passes cleanly (all Vitest tests passing).
- [ ] Candidate 360 profile opens instantly via Slide-Over Drawer without full page refresh.
- [ ] Global Ctrl+K Command Palette allows searching and route navigation.

## Follow-up — 2026-08-07T13:17:00Z

Sprint 0 (Person A): Build three infrastructure foundation pieces for the RecruitOps in-house recruitment SaaS — an object storage abstraction, Myanmar script normalization, and a refresh token mechanism. These are prerequisites for the CV upload, search, and auth hardening features that follow.

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Integrity mode: development

Reference material:
- `docs/decisions/ADR-0013-infrastructure-and-storage.md` — Cloudflare R2 hosted, MinIO on-prem, behind S3-compatible abstraction
- `docs/decisions/ADR-0009-myanmar-script-handling.md` — Zawgyi↔Unicode normalization required for all text ingestion
- `docs/decisions/ADR-0016-login-brute-force-protection.md` — current auth flow (JWT 8h, no refresh token)
- `CLAUDE.md` — conventions, stack (.NET 10 LTS, Clean Architecture), build/test commands

## Verification Resources

The project has an existing test suite that must remain green after changes:
- Backend: `dotnet test backend/RecruitOps.sln` — **228 tests passing** (51 Domain + 177 Api)
- Frontend: `npm run test` in `frontend/internal` — **189 tests passing**
- Typecheck: `npm run typecheck` — **0 errors** across all workspaces
- Docker: `docker compose up --build` runs cleanly

## Requirements

### R1. Object Storage Abstraction
An abstraction over S3-compatible object storage that lets the application store and retrieve files (CVs, documents, profile photos) without knowing whether the backing store is Cloudflare R2 or a local MinIO instance. The abstraction must sit in the Application layer (interface) with the implementation in Infrastructure. Configuration is via environment variables — the same image runs against either backend. Refer to ADR-0013 for the rationale. The application must never call R2 or MinIO APIs directly outside this abstraction.

### R2. Myanmar Script Normalization (Zawgyi→Unicode)
A normalization service that detects Zawgyi-encoded Myanmar text and converts it to Unicode (NFC). This is required for all text ingestion paths (CV text extraction, candidate form submissions, search indexing) per ADR-0009. The detection + conversion must work without network access (local, in-process). Expose it as an injectable service in the Application layer.

### R3. Refresh Token Mechanism
Extend the existing JWT auth flow to support refresh tokens. Currently the system issues an 8-hour access token with no refresh path — when it expires, the user must re-login. Add a refresh token that allows the frontend to silently obtain a new access token. The refresh token should be stored server-side (database) and be revocable. Follow the existing auth patterns in `AuthService` and `JwtTokenService`.

## Acceptance Criteria

### Object Storage (R1)
- [ ] An `IFileStorage` interface (or equivalent) exists in the Application layer with methods for upload, download, delete, and presigned-URL generation
- [ ] At least one implementation exists in Infrastructure that works against an S3-compatible API
- [ ] The MinIO container in `docker-compose.yml` is usable as the storage backend for local development
- [ ] Configuration (endpoint, bucket, credentials) is via environment variables — no hard-coded values
- [ ] `dotnet build backend/src/Api` compiles cleanly after changes
- [ ] All **228 existing backend tests** still pass (`dotnet test backend/RecruitOps.sln`)
- [ ] At least 3 new integration or unit tests covering upload, download, and delete operations

### Myanmar Script Normalization (R2)
- [ ] A service exists that accepts a string, detects whether it is Zawgyi-encoded, and returns a Unicode-normalized (NFC) string
- [ ] The service works in-process with no network dependency
- [ ] At least 5 unit tests covering: pure Unicode input (no-op), Zawgyi input (converts), mixed content, empty/null input, and a real-world Burmese sentence
- [ ] All **228 existing backend tests** still pass

### Refresh Token (R3)
- [ ] A `POST /api/auth/refresh` endpoint exists that accepts a refresh token and returns a new access + refresh token pair
- [ ] Refresh tokens are persisted server-side (database entity + EF migration)
- [ ] A refresh token can be revoked (e.g., on logout or password change)
- [ ] Expired or revoked refresh tokens return 401
- [ ] The frontend `auth.ts` module is updated to use the refresh mechanism (attempt silent refresh before redirecting to login)
- [ ] The `@recruitops/types` shared package includes the updated auth response type
- [ ] All **228 existing backend tests** still pass
- [ ] At least 5 new tests covering: valid refresh, expired refresh, revoked refresh, reuse detection, and login returns a refresh token
- [ ] `npm run typecheck` passes with 0 errors after frontend changes

### Cross-cutting
- [ ] `docker compose up --build` still runs cleanly
- [ ] No new TypeScript errors (`npm run typecheck`)
- [ ] Changes follow existing Clean Architecture conventions (interface in Application, implementation in Infrastructure)
- [ ] New packages (if any) are permissive-licensed (MIT/Apache-2.0/BSD) — no copyleft/AGPL

## Follow-up — 2026-08-07T21:24:00Z

Person A - Flow 1: Build the complete CV Upload & Local Text Extraction Flow for RecruitOps. This includes CV file upload API, local document text extraction (PDF, DOCX, image OCR fallback with Zawgyi→Unicode normalization via `IMyanmarScriptNormalizer`), bulk CV background processing job, drag-and-drop upload UI inside Candidate 360 SlideOver, and parsed data human-review/confirmation panel.

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Integrity mode: development

Reference material:
- `docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md` — Local extraction mandatory MVP, AI optional
- `docs/decisions/ADR-0009-myanmar-script-handling.md` — Zawgyi→Unicode normalization required on extracted text
- `docs/decisions/ADR-0013-infrastructure-and-storage.md` — Storage via `IFileStorage` (S3/MinIO)

## Verification Resources

Existing test suite baseline that MUST remain green:
- Backend: `dotnet test backend/RecruitOps.sln` — **333 tests passing** (51 Domain + 282 Api)
- Frontend: `npm run test` in `frontend/internal` — **233 tests passing**
- Typecheck: `npm run typecheck` — **0 errors** across all workspaces

## Requirements

### R1. CV Resume Storage & Extraction Backend API
Build backend APIs and domain/infrastructure services for CV file management:
- Endpoint `POST /api/applications/{id}/resume` to accept single CV upload (PDF/DOCX/PNG/JPG up to 10MB), store via `IFileStorage`, and extract text.
- Endpoint `GET /api/applications/{id}/resume` to download/view the stored CV file.
- Document extraction service supporting:
  - PDF text extraction (text streams)
  - DOCX text extraction (OpenXML document body)
  - Image OCR fallback (for scanned PDFs or image CVs)
  - Automatic Zawgyi normalization via `IMyanmarScriptNormalizer` on all extracted text
- Return structured extraction results (`extractedText`, `detectedLanguage`, `isZawgyiNormalized`, `parsedContactInfo`).

### R2. Bulk CV Upload Background Job
Build a background processing job for bulk CV ingest:
- Endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk` to accept up to 50 CV files in a single batch.
- Process files asynchronously using background job runner without blocking HTTP requests.
- Track per-file processing status (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`) with progress summary endpoint `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`.

### R3. Candidate 360 SlideOver CV Viewer & Parsed Profile UI
Build frontend components inside `@recruitops/internal`:
- Add a "CV & Documents" tab/section in `CandidateSlideOver.tsx` with drag-and-drop upload zone, upload progress bar, and embedded CV text viewer.
- Add a "Parsed Profile Human Review" panel that shows extracted text side-by-side with editable candidate profile fields (Name, Email, Phone, Experience, Skills), requiring explicit recruiter confirmation before applying changes to candidate profile.
- Add a Bulk CV Upload modal on `JobPostingDetailPage` allowing recruiters to drag-and-drop multiple CVs with live progress indicators.

## Acceptance Criteria

### Backend Criteria
- [ ] `POST /api/applications/{id}/resume` stores file using `IFileStorage` and extracts text cleanly
- [ ] PDF and DOCX text extraction returns readable plain text
- [ ] Any extracted text containing Zawgyi Myanmar script is automatically converted to Unicode NFC via `IMyanmarScriptNormalizer`
- [ ] Bulk upload endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk` accepts up to 50 files and returns batch tracking ID
- [ ] All **333 existing backend tests** pass cleanly (`dotnet test backend/RecruitOps.sln`)
- [ ] At least 8 new backend tests covering CV upload, PDF/DOCX extraction, Zawgyi normalization on extracted text, and bulk job batch status

### Frontend Criteria
- [ ] `CandidateSlideOver.tsx` displays CV upload zone, file preview link, and extracted text viewer
- [ ] Recruiter can edit and confirm parsed profile data before updating candidate records
- [ ] Bulk upload modal on `JobPostingDetailPage` displays progress bar per file
- [ ] `npm run typecheck` passes with **0 errors** across `@recruitops/internal`, `@recruitops/public`, and `@recruitops/types`
- [ ] All **233 existing frontend tests** pass cleanly (`npm run test` in `frontend/internal`)
- [ ] At least 5 new frontend Vitest tests covering CV upload component interactions and parsed profile review panel

---
*Cross-cutting: Maintain Clean Architecture principles, full TypeScript & C# types alignment.*

## Follow-up — 2026-08-08T14:57:03Z

Person A - Flow 1 (Milestone 2 & 3): Resume work to complete the remaining parts of Person A Flow 1 (Bulk CV Upload Background Job & Candidate 360 CV Viewer / Parsed Data UI).

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Integrity mode: development

Current Status:
- Milestone 1 (R1: CV Upload & Extraction API, `IDocumentTextExtractor`, `POST/GET /api/applications/{id}/resume`, Zawgyi normalization integration) is COMPLETE and PASSING all 349 backend tests.

Remaining Work:
### R2. Bulk CV Upload Background Job (Milestone 2)
- Endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk` to accept up to 50 CV files in a single batch.
- Process files asynchronously using background job runner without blocking HTTP requests.
- Track per-file processing status (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`) with progress summary endpoint `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`.

### R3. Candidate 360 SlideOver CV Viewer & Parsed Profile UI (Milestone 3)
- Update `CandidateSlideOver.tsx` in `@recruitops/internal`:
  - Add "CV & Documents" tab/section with drag-and-drop upload zone, upload progress bar, and embedded CV text viewer / download button.
  - Add "Parsed Profile Human Review" panel that shows extracted text side-by-side with editable candidate profile fields (Name, Email, Phone, Experience, Skills), requiring explicit recruiter confirmation before applying changes to candidate profile.
- Add Bulk CV Upload modal on `JobPostingDetailPage` allowing recruiters to drag-and-drop multiple CVs with live progress indicators per file.

## Acceptance Criteria
- [ ] Bulk upload endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk` accepts up to 50 files and returns batch tracking ID
- [ ] Batch progress endpoint `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` returns per-file status summary
- [ ] `CandidateSlideOver.tsx` displays CV upload zone, file preview link, and extracted text viewer
- [ ] Recruiter can edit and confirm parsed profile data before updating candidate records
- [ ] Bulk upload modal on `JobPostingDetailPage` displays progress bar per file
- [ ] All 349+ existing backend tests pass cleanly (`dotnet test backend/RecruitOps.sln`)
- [ ] All 233+ existing frontend tests pass cleanly (`npm run test` in `frontend/internal`)
- [ ] `npm run typecheck` passes with 0 errors across all workspaces

