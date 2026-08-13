# Handoff Report — Candidate 360 AI UI Exploration & Design

- **From:** Explorer 2 (Frontend Candidate 360 UI Specialist)
- **To:** Parent / Implementer Agent
- **Date:** 2026-08-11
- **Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_frontend_candidate`

---

## 1. Observation

1. **`packages/types/src/index.ts` (lines 756–798)**:
   Contains AI DTO interfaces:
   - `MatchCandidateRequest` (`candidateId`, `jobPostingId`)
   - `CandidateMatchAnalysis` (`overallScore`, `recommendation`, `strengths`, `gaps`, `criteria`, `suggestedInterviewQuestions`, `summary`)
   - `GenerateExecutiveSummaryRequest` (`candidateId`, `jobPostingId`, `audience`, `language`)
   - `ExecutiveSummaryResult` (`headline`, `summary`, `keyStrengths`, `suggestedInterviewQuestions`, `isBilingual`)
2. **`frontend/internal/src/lib/api.ts` (lines 208–243)**:
   Defines `aiApi` client namespace with methods:
   - `aiApi.matchCandidate` calling `POST /ai/claude/match-candidate`
   - `aiApi.generateExecutiveSummary` calling `POST /ai/gemini/executive-summary`
3. **`frontend/internal/src/features/pipeline/CandidateSlideOver.tsx` (lines 403–617)**:
   Renders full-height candidate drawer using `Sheet` from `@recruitops/ui`. Currently features 5 tabs: `overview`, `cv`, `history`, `scorecards`, `notes`. Smart Match Badge and Executive Summary Panel are not yet integrated into the layout.
4. **`frontend/internal/src/lib/ai.test.ts` (lines 60–107)**:
   Vitest tests exist for `aiApi.matchCandidate` and `aiApi.generateExecutiveSummary` API fetch methods.
5. **`docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md` (lines 34–40)**:
   Specifies that AI features are optional and API-key gated. Unconfigured keys must not throw 500 crashes and must gracefully present feature-disabled fallback states.

---

## 2. Logic Chain

1. **Data Availability**: `packages/types` and `lib/api.ts` already expose full TypeScript definitions and fetch functions for `matchCandidate` and `generateExecutiveSummary`.
2. **UI Integration**: `CandidateSlideOver.tsx` provides the primary Candidate 360 container. Integrating a **Smart Match Header Badge** in `SheetHeader` and an **Executive Summary Card / AI Insights Tab** inside `SheetBody` directly addresses requirement R2 without breaking existing tabs (`cv`, `history`, `scorecards`, `notes`).
3. **Language Toggle Requirement**: `GenerateExecutiveSummaryRequest` accepts `language: 'en' | 'my' | 'bilingual'`. An EN / MY / Bilingual toggle button group in `ExecutiveSummaryPanel` seamlessly passes the selected option to `aiApi.generateExecutiveSummary`.
4. **Error & 402 Gating**: Per ADR-0008, when API keys are absent, backend returns `402 Payment Required`. Checking `err instanceof ApiError && err.status === 402` allows the UI to render an informative yellow banner informing recruiters that Phase 1 manual profiling remains functional while AI features require API key setup.
5. **Testing Approach**: 6 distinct Vitest tests in `CandidateSlideOverAi.test.tsx` will verify badge color-coding, breakdown table rendering, EN/MY language switching, 402 disabled banner display, 500 error retry handling, and clipboard export actions.

---

## 3. Caveats

- **Job Posting Context**: `aiApi.matchCandidate` requires both `candidateId` and `jobPostingId`. In `CandidateSlideOver`, if `jobPostingId` is omitted (e.g., viewing candidate outside job context), Smart Match UI must prompt the user to select a target job posting.
- **Burmese Font Rendering**: System depends on standard Myanmar Unicode fonts for rendering Burmese summary output. Zawgyi normalization is performed backend-side per ADR-0009.

---

## 4. Conclusion

The design for Candidate 360 AI UI (Smart Match Badge & Breakdown drawer + Executive Summary Panel with EN/MY/Bilingual toggle) is fully specified and aligned with `packages/types`, `lib/api.ts`, and ADR-0008 guardrails. All findings and detailed specifications are documented in `analysis.md`.

---

## 5. Verification Method

1. **Inspect Analysis Report**:
   Read `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_frontend_candidate\analysis.md` to review component specs, state hooks, 402 error gating, and Vitest test definitions.
2. **Verify Frontend Typecheck**:
   Run `npm run typecheck` at repository root — must pass with 0 errors.
3. **Verify Frontend Vitest Tests**:
   Run `npm run test` inside `frontend/internal` — existing 295 tests must remain 100% green.
