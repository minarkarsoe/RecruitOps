# Task Context & Reference Materials

## Project Scope
Build Person B - Flow 2: Complete AI Integration Flow (5 Endpoints End-to-End) for RecruitOps per ADR-0008 & ADR-0009.

## Reference Architectural Decisions & Directives
1. **ADR-0008 (Document Extraction & AI Profiling)**:
   - AI optional, API-key gated.
   - Human confirmation mandatory before mutating database records.
   - Structured JSON response from resume parsing and candidate matching.
2. **ADR-0009 (Myanmar Script Handling)**:
   - Burmese ↔ English AI translation and script handling (Zawgyi vs Unicode).
3. **CLAUDE.md**:
   - Clean Architecture (.NET 10 / React 18 + Vite + TypeScript).
   - Test commands:
     - Backend: `dotnet test backend/RecruitOps.sln`
     - Frontend: `npm run test` in `frontend/internal`
     - Typecheck: `npm run typecheck`

## Key Targets & Quality Requirements
- Backend:
  - 5 Endpoints: `POST /api/ai/parse-resume`, `POST /api/ai/match-candidate`, `POST /api/ai/executive-summary`, `POST /api/ai/document-prep`, `POST /api/ai/translate`.
  - Abstraction: `IAiIntegrationService`, `IClaudeService`, `IGeminiService`.
  - API Key Gating: 402 Payment Required or feature-disabled response when unconfigured.
  - All 411 existing backend tests passing + >=10 new tests.
- Frontend:
  - `CandidateSlideOver.tsx`: Smart Match badge & breakdown drawer, suggested interview questions, Executive Summary panel with language switcher (EN / MY / Bilingual), copy/export.
  - `AiDocumentPrepModal.tsx`: Interview Kit / Client Dossier generation and preview.
  - Inline translation button (EN ↔ MY) on long text fields.
  - All 295 existing frontend tests passing + >=6 new tests + 0 typecheck errors.
