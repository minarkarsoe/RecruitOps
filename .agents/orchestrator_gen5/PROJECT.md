# Project: RecruitOps CRM Features, Design System Compliance & Hybrid AI Integration

## Architecture
- **Backend Architecture**: ASP.NET Core .NET 10 Clean Architecture API (`backend/src/Api`, `Application`, `Infrastructure`, `Domain`). Fine-grained RBAC via `[HasPermission(...)]`.
- **Frontend Architecture**: Vite + React SPA (`frontend/internal`), Next.js SSR (`frontend/public`), `@recruitops/ui` shared UI package (`packages/ui`), shared TypeScript types (`packages/types`).
- **Hybrid AI API**: Dual model architecture — Anthropic Claude API (`claude-3-5-sonnet`) for Resume Parsing & Candidate Matching; Google Gemini API (`gemini-1.5-pro`/`flash`) for Executive Summaries, Document Preparation, and Burmese Localization.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Requisitions Module | RequisitionTable, RequisitionDrawer, useRequisitions hook | M1 | R1 |
| 2 | Pipeline Module | PipelineKanbanBoard, CandidateSlideOver (360 profile drawer with CV viewer, stage history, scorecard summaries, notes), usePipeline hook | M1 | R1 |
| 3 | Interviews Module | BlindScorecardDrawer (split view 1-5 rating, @Mentions note thread), useInterviews hook | M1 | R1 |
| 4 | Typography & Font Stack | Bricolage Grotesque & Inter with Noto Sans Myanmar fallback (line-height >= 1.7) across internal & public apps | M1 | R2 |
| 5 | Design System Signature Primitives | StatusPill extended vocabulary, PipelineStageRail, ExpiryAttentionCard, ClientPortalCard & ClientFeedbackBar | M1 | R2 |
| 6 | Global Command Palette | Ctrl+K / Cmd+K search & route navigation modal | M1 | R2 |
| 7 | Claude API Backend Integration | Resume Parsing & Structuring (`/api/ai/claude/parse-resume`), Candidate Matching Analysis (`/api/ai/claude/match-candidate`) | M2 | R3 |
| 8 | Gemini API Backend Integration | Executive Summaries (`/api/ai/gemini/executive-summary`), Document Preparation (`/api/ai/gemini/document-prep`), Burmese Localization (`/api/ai/gemini/burmese-localization`) | M2 | R3 |
| 9 | Shared Types & Frontend AI Client | AI DTO interfaces in `packages/types`, `api.ai` methods in `frontend/internal/src/lib/api.ts`, Vitest test suite | M3 | R3 |
| 10 | Candidate 360 AI Integration | Connect Claude & Gemini AI actions into `CandidateSlideOver` profile drawer | M3 | R1, R3 |
| 11 | Final E2E Verification & Forensic Audit | Full typecheck (0 errors), frontend Vitest suite (100% pass), backend .NET test suite (100% pass), empirical challenge, forensic audit CLEAN | M4 | Acceptance Criteria |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Design System Polish & Signature Components | R2: Line-height 1.7, public app font load, StatusPill vocabulary extension, PipelineStageRail, ExpiryAttentionCard, ClientPortalCard & ClientFeedbackBar | None | DONE |
| 2 | Hybrid AI API Backend Architecture & Endpoints | R3: C# DTOs, interfaces, ClaudeApiClient, GeminiApiClient, AiIntegrationService, AiController.cs endpoints, backend integration tests | None | PLANNED |
| 3 | Frontend AI Client Integration & Candidate 360 Wireup | R3 & R1: Shared TS types in `packages/types`, `api.ai` in `frontend/internal/src/lib/api.ts`, Vitest AI tests, Candidate 360 AI tab & actions | M1, M2 | PLANNED |
| 4 | E2E Integration, Empirical Challenge & Forensic Audit | All R1-R3: Typecheck (0 errors), Vitest suite, .NET test suite, Challenger verification, Forensic Auditor CLEAN verdict | M3 | PLANNED |

## Interface Contracts
### Hybrid AI API Endpoints & RBAC Permissions
- `POST /api/ai/claude/parse-resume` (`permission:ai:resume:parse`) -> `ParseResumeRequest` -> `ParsedResumeResultDto`
- `POST /api/ai/claude/match-candidate` (`permission:ai:matching:analyze`) -> `MatchCandidateRequest` -> `CandidateMatchAnalysisDto`
- `POST /api/ai/gemini/executive-summary` (`permission:ai:summary:generate`) -> `GenerateExecutiveSummaryRequest` -> `ExecutiveSummaryDto`
- `POST /api/ai/gemini/document-prep` (`permission:ai:document:prepare`) -> `PrepareDocumentRequest` -> `DocumentPrepResultDto`
- `POST /api/ai/gemini/burmese-localization` (`permission:ai:localization:translate`) -> `BurmeseLocalizationRequest` -> `BurmeseLocalizationResultDto`

### Shared Frontend AI API Service (`frontend/internal/src/lib/api.ts`)
- `api.ai.parseResume(req: ParseResumeRequest): Promise<ParsedResumeResult>`
- `api.ai.matchCandidate(req: MatchCandidateRequest): Promise<CandidateMatchAnalysis>`
- `api.ai.generateExecutiveSummary(req: GenerateExecutiveSummaryRequest): Promise<ExecutiveSummaryResult>`
- `api.ai.prepareDocument(req: PrepareDocumentRequest): Promise<DocumentPrepResult>`
- `api.ai.translateBurmese(req: BurmeseLocalizationRequest): Promise<BurmeseLocalizationResult>`

## Code Layout
- Backend: `backend/src/Domain`, `backend/src/Application`, `backend/src/Infrastructure`, `backend/src/Api`
- Backend Tests: `backend/tests/RecruitOps.Api.Tests`
- Shared Packages: `packages/ui`, `packages/types`
- Frontend Internal: `frontend/internal` (`src/components`, `src/features`, `src/lib`)
- Frontend Public: `frontend/public`
