# Project: RecruitOps AI Integration Flow (5 Endpoints End-to-End)

## Architecture
- **Backend Architecture**: Clean Architecture (.NET 10)
  - `RecruitOps.Domain`: Domain Entities (`Candidate`, `JobApplication`, `JobPosting`, `Requisition`).
  - `RecruitOps.Application`: Provider-agnostic interfaces (`IAiIntegrationService`, `IClaudeService`, `IGeminiService`), AI DTOs (`ParseResumeRequestDto`, `CandidateMatchRequestDto`, `ExecutiveSummaryRequestDto`, `DocumentPrepRequestDto`, `TranslateTextRequestDto`, etc.), `IMyanmarScriptNormalizer`.
  - `RecruitOps.Infrastructure`: `ClaudeApiClient` (Data Analysis / Smart Match), `GeminiApiClient` (Doc Gen / Localization), `AiIntegrationService` facade, API key gating logic (`ClaudeOptions`, `GeminiOptions`).
  - `RecruitOps.Api`: `AiController` exposing 5 dual-routed endpoints (`parse-resume`, `match-candidate`, `executive-summary`, `document-prep`, `translate`), returning `402 Payment Required` on missing API keys without 500 errors.
- **Frontend Architecture**: React 18 / Vite / TypeScript
  - `@recruitops/types`: TypeScript AI DTOs (`MatchCandidateRequest`, `CandidateMatchAnalysis`, `GenerateExecutiveSummaryRequest`, `ExecutiveSummaryResult`, `PrepareDocumentRequest`, `GeneratedDocument`, `TranslateTextRequest`, `TranslateTextResult`).
  - `@recruitops/ui`: `Dialog.tsx`, `Sheet.tsx`, `Button.tsx`, `Badge.tsx`, `Tabs.tsx`.
  - `@recruitops/internal`:
    - `features/pipeline/CandidateSlideOver.tsx`: Smart Match badge, criteria breakdown drawer, suggested interview questions, Executive Summary panel (EN/MY/Bilingual toggle).
    - `features/ai/AiDocumentPrepModal.tsx`: Interview Kit / Client Dossier document generation & preview modal.
    - `features/ai/InlineTranslator.tsx` & `TranslatedTextField.tsx`: Burmese ↔ English inline translation toggles per ADR-0009.
    - `lib/api.ts`: `aiApi` endpoints fetch client with `ApiError` 402 handling.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Claude API Client: Resume Parsing | `POST /api/ai/parse-resume`: Extracted text -> structured candidate JSON | M1 | survey |
| 2 | Claude API Client: Smart Match | `POST /api/ai/match-candidate`: Candidate vs. Job Match Scoring (0-100), criteria breakdown, interview questions | M1 | survey |
| 3 | Gemini API Client: Executive Summary | `POST /api/ai/executive-summary`: Candidate profile executive summary | M1 | survey |
| 4 | Gemini API Client: Document Prep | `POST /api/ai/document-prep`: Interview Kit / Client Dossier generation | M1 | survey |
| 5 | Gemini API Client: Burmese Translation | `POST /api/ai/translate`: Burmese ↔ English text localization per ADR-0009 | M1 | survey |
| 6 | Backend API Key Gating | Missing API keys return explicit HTTP 402 Payment Required without 500 crashes | M1 | survey |
| 7 | Backend Unit & Integration Tests | 411 existing tests pass + 43 new tests = 454 backend tests passing | M1 | survey |
| 8 | Candidate 360 Smart Match Badge & Breakdown UI | `CandidateSlideOver.tsx`: Match score badge, criteria breakdown drawer, suggested questions | M2 | survey |
| 9 | Candidate 360 Executive Summary Panel UI | `CandidateSlideOver.tsx`: Executive summary panel with EN/MY/Bilingual toggle, copy/export | M2 | survey |
| 10 | AI Document Prep Modal UI | `AiDocumentPrepModal.tsx`: Interview Kit / Client Dossier generation and preview modal | M3 | survey |
| 11 | Inline Burmese Translation UI | Inline EN ↔ MY translation buttons on Job Descriptions and Candidate Notes | M3 | survey |
| 12 | Frontend Vitest Tests & Typecheck | 295 existing frontend tests pass + 0 TypeScript errors + >=6 new Vitest tests | M3 | survey |
| 13 | E2E Integration & Integrity Verification | Full end-to-end alignment, zero regressions, Clean audit verdict | M4 | survey |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Backend AI Provider & 5 Gated Endpoints | `IAiIntegrationService`, `IClaudeService`, `IGeminiService`, 5 API endpoints, API key gating (402), +43 backend tests | none | DONE |
| 2 | Candidate 360 Smart Match & Executive Summary UI | `CandidateSlideOver.tsx` Smart Match badge, criteria breakdown drawer, Executive summary panel with language switcher | M1 | IN_PROGRESS |
| 3 | AI Document Prep Modal & Burmese Localization UI | `AiDocumentPrepModal.tsx`, `InlineTranslator.tsx`, `TranslatedTextField.tsx`, +6 frontend Vitest tests | M1, M2 | PLANNED |
| 4 | E2E Integration & Verification Hardening | Full regression suite execution, adversarial testing, Forensic Audit pass | M1, M2, M3 | PLANNED |

## Interface Contracts
### 1. Resume Parsing API
`POST /api/ai/parse-resume` (and `/api/ai/claude/parse-resume`)
- Request: `{ "rawText": "string", "fileName": "string" }`
- Response 200: `{ "parsedCandidate": { "fullName": "string", "email": "string", "phone": "string", "skills": ["string"], ... } }`
- Response 402: `{ "status": 402, "title": "Payment Required", "detail": "Claude API key is unconfigured." }`

### 2. Candidate Match API
`POST /api/ai/match-candidate` (and `/api/ai/claude/match-candidate`)
- Request: `{ "candidateId": "guid", "jobPostingId": "guid" }`
- Response 200: `{ "overallScore": 85, "recommendation": "Strong Match", "strengths": ["string"], "gaps": ["string"], "criteria": [ { "name": "string", "score": 90, "matched": true, "reason": "string" } ], "suggestedInterviewQuestions": ["string"], "summary": "string" }`
- Response 402: `{ "status": 402, "title": "Payment Required", "detail": "Claude API key is unconfigured." }`

### 3. Executive Summary API
`POST /api/ai/executive-summary` (and `/api/ai/gemini/executive-summary`)
- Request: `{ "candidateId": "guid", "jobPostingId": "guid", "audience": "InternalRecruiter", "language": "en" | "my" | "bilingual" }`
- Response 200: `{ "headline": "string", "summary": "string", "keyStrengths": ["string"], "suggestedInterviewQuestions": ["string"], "isBilingual": boolean }`
- Response 402: `{ "status": 402, "title": "Payment Required", "detail": "Gemini API key is unconfigured." }`

### 4. Document Preparation API
`POST /api/ai/document-prep` (and `/api/ai/gemini/document-prep`)
- Request: `{ "candidateId": "guid", "jobPostingId": "guid", "documentType": "InterviewKit" | "ClientDossier", "language": "en" | "my" | "bilingual" }`
- Response 200: `{ "documentType": "string", "title": "string", "markdownContent": "string", "htmlContent": "string", "metadata": {} }`
- Response 402: `{ "status": 402, "title": "Payment Required", "detail": "Gemini API key is unconfigured." }`

### 5. Translation API
`POST /api/ai/translate` (and `/api/ai/gemini/burmese-localization`)
- Request: `{ "text": "string", "sourceLanguage": "auto" | "en" | "my", "targetLanguage": "en" | "my" }`
- Response 200: `{ "originalText": "string", "translatedText": "string", "sourceLanguage": "string", "targetLanguage": "string", "confidenceScore": 0.95 }`
- Response 402: `{ "status": 402, "title": "Payment Required", "detail": "Gemini API key is unconfigured." }`

## Code Layout
- Backend:
  - `backend/src/Application/Interfaces/IAiIntegrationService.cs`
  - `backend/src/Application/Interfaces/IClaudeService.cs`
  - `backend/src/Application/Interfaces/IGeminiService.cs`
  - `backend/src/Application/DTOs/Ai/AiIntegrationDtos.cs`
  - `backend/src/Infrastructure/Services/ClaudeApiClient.cs`
  - `backend/src/Infrastructure/Services/GeminiApiClient.cs`
  - `backend/src/Infrastructure/Services/AiIntegrationService.cs`
  - `backend/src/Infrastructure/Options/ClaudeOptions.cs`
  - `backend/src/Infrastructure/Options/GeminiOptions.cs`
  - `backend/src/Api/Controllers/AiController.cs`
  - `backend/tests/RecruitOps.Api.Tests/AiProviderIntegrationAndGatingTests.cs`
  - `backend/tests/RecruitOps.Api.Tests/AiStressAndResilienceTests.cs`
- Frontend:
  - `packages/types/src/index.ts`
  - `frontend/internal/src/lib/api.ts`
  - `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`
  - `frontend/internal/src/features/ai/AiDocumentPrepModal.tsx`
  - `frontend/internal/src/features/ai/InlineTranslator.tsx`
  - `frontend/internal/src/features/ai/TranslatedTextField.tsx`
  - `frontend/internal/src/features/pipeline/__tests__/CandidateSlideOverAi.test.tsx`
  - `frontend/internal/src/features/ai/__tests__/AiDocumentPrepModal.test.tsx`
  - `frontend/internal/src/features/ai/__tests__/InlineTranslator.test.tsx`
