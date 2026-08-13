# Strategic Execution Plan: AI Integration Flow (5 Endpoints End-to-End)

## Objective
Implement end-to-end AI capabilities for RecruitOps according to ADR-0008 & ADR-0009:
- Provider-agnostic abstractions and API clients (Claude for Data Analysis/Smart Match, Gemini for Doc Gen/Localization).
- API Key Gating returning graceful 402 Payment Required / feature-disabled responses without 500 errors when API keys are unconfigured.
- 5 Backend Endpoints (`parse-resume`, `match-candidate`, `executive-summary`, `document-prep`, `translate`).
- Human confirmation workflow for AI structured data per ADR-0008.
- Candidate 360 UI enhancements in `CandidateSlideOver.tsx` (Smart Match Badge & Breakdown, Executive Summary Panel with language switcher).
- AI Document Prep Modal (`AiDocumentPrepModal.tsx`) & inline Burmese/English translation toggles.
- Pass 100% of existing tests (411 backend, 295 frontend, 0 typecheck errors) + add >=10 new backend tests and >=6 new frontend tests.

## Phase 0: Survey & Technical Exploration (Parallel Explorers)
- Dispatch Explorer 1: Backend AI architecture, existing services, controllers, DTOs, options/secrets pattern, and API key gating strategy.
- Dispatch Explorer 2: Frontend Candidate 360 components (`CandidateSlideOver.tsx`, Candidate detail tabs/panels, types, API client patterns).
- Dispatch Explorer 3: AI Document Prep Modal requirements, Burmese translation script handling (ADR-0009), inline translate components, and Vitest setup.

## Phase 1: Milestone Decomposition & Interface Contracts
- Synthesize survey findings into `PROJECT.md`.
- Establish 4 Milestones:
  - M1: Backend AI Provider Abstraction & 5 Gated Endpoints (+10 backend tests)
  - M2: Smart Match & Executive Summary UI in Candidate 360
  - M3: AI Document Prep Modal & Burmese Localization UI (+6 frontend Vitest tests)
  - M4: E2E Integration, Verification & Forensic Audit Pass

## Phase 2: Execution via Iteration Loops (Worker → Reviewer → Challenger → Forensic Auditor)
- For each milestone, execute:
  1. Dispatch Explorer for targeted design and test plan.
  2. Dispatch Worker for implementation, unit testing, and test execution verification.
  3. Dispatch 2 parallel Reviewers for correctness, Clean Architecture, and completeness.
  4. Dispatch 2 parallel Challengers for empirical/edge case verification.
  5. Dispatch Forensic Auditor for integrity check.
  6. Evaluate gate criteria (All pass + CLEAN audit required).

## Phase 3: Final Verification & Hand-off
- Verify backend test suite (`dotnet test backend/RecruitOps.sln` -> 411 + >=10 = >=421 tests).
- Verify frontend test suite (`npm run test` in `frontend/internal` -> 295 + >=6 = >=301 tests).
- Verify typecheck (`npm run typecheck` -> 0 errors).
- Notify Sentinel / User of completion.
