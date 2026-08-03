# Codebase Survey & Blueprint: Feature-Based Architecture & Test Suite (R3 Guardrails)

## Executive Summary
This document presents the comprehensive codebase survey and architectural refactoring blueprint for **Requirement R3 (Feature Modules & Testing/Typecheck Guardrails)** of the RecruitOps platform.

The objective is to reorganize `frontend/internal/src` from a monolithic page-and-flat-component structure into a **Domain-Driven Feature-Based Frontend Architecture** (`src/features/*`), while ensuring that `npm run typecheck` and the existing 65+ Vitest tests pass cleanly.

---

## 1. Existing Codebase Survey (`frontend/internal/src`)

### 1.1 Current Directory Structure
The internal SPA (`frontend/internal`) is currently structured as follows:
```
frontend/internal/src/
├── App.tsx                     # Main router and shell layout integration
├── main.tsx                    # React DOM root entry point
├── index.css                   # Tailwind CSS import and global styles
├── vite-env.d.ts               # Vite client types
├── lib/
│   ├── api.ts                  # Fetch API client wrapper with JWT & X-Tenant-Id headers
│   ├── auth.ts                 # Session storage manager & RBAC permission predicates
│   ├── scorecard.ts            # Scorecard draft/validation logic (isSendable, toAnswers, missingRequired)
│   └── scorecard.test.ts       # 7 unit tests for scorecard draft validation
├── services/
│   ├── permissionService.ts    # RBAC permission API client
│   ├── roleService.ts          # Role management API client
│   └── userService.ts          # User directory API client
├── types/
│   └── rbac.ts                 # Local RBAC helper interfaces
├── components/
│   ├── AppLayout.tsx           # Main application sidebar & shell layout
│   ├── AppLayout.test.tsx      # 6 tests for navigation permission filtering
│   ├── ApplicationDebrief.tsx  # Interview rounds & schedule form component (582 lines)
│   ├── ApplicationNotes.tsx    # Debrief thread with @Mentions & round pinning (192 lines)
│   ├── ApplicationNotes.test.tsx # 9 tests for notes thread & mentions
│   ├── FormFieldBuilder.tsx    # Custom application form builder
│   ├── PermissionMatrixGrid.tsx# Role permission grid matrix
│   ├── PermissionMatrixGrid.test.tsx # 9 tests for permission matrix
│   ├── RequireAuth.tsx         # Route auth guard
│   ├── RequirePermission.tsx   # Route permission guard
│   ├── RequirePermission.test.tsx # 5 tests for route permission guard
│   ├── TenantSwitcherBar.tsx   # Multi-tenant context switcher
│   └── TenantSwitcherBar.test.tsx # 5 tests for tenant switcher
├── pages/
│   ├── ApprovalChainsPage.tsx
│   ├── DepartmentsPage.tsx
│   ├── InboxPage.tsx
│   ├── InterviewDetailPage.tsx# Split view scorecard & panel evaluations (446 lines)
│   ├── InterviewDetailPage.test.tsx # 6 tests for blind panel rule & scorecard drafts
│   ├── JdTemplatesPage.tsx
│   ├── JobPostingDetailPage.tsx # Advert editor, publishing link & inline pipeline list (398 lines)
│   ├── JobPostingsPage.tsx
│   ├── LoginPage.tsx
│   ├── RequisitionDetailPage.tsx# Requisition details, approval steps & actions (241 lines)
│   ├── RequisitionFormPage.tsx
│   ├── RequisitionsPage.tsx    # Requisitions list table (85 lines)
│   ├── RolesPage.tsx & RolesPage.test.tsx # 4 tests
│   ├── ScorecardTemplatesPage.tsx
│   └── UsersPage.tsx & UsersPage.test.tsx # 6 tests
└── test/
    ├── fixtures.ts             # Shared test data factories (interview, scorecard, myScorecard, panel, note)
    ├── rbacFixtures.ts         # Test roles & permissions fixtures
    ├── setup.ts                # Vitest global setup (jsdom cleanup)
    └── milestone4EmpiricalChallenge.test.tsx # 8 integration tests
```

### 1.2 Identified Coupling & Architectural Opportunities
1. **Requisitions (`RequisitionsPage.tsx`, `RequisitionDetailPage.tsx`)**:
   - Requisition table layout is embedded inline inside `RequisitionsPage.tsx`.
   - Requisition detail views, timeline, and decision actions are mixed together inside `RequisitionDetailPage.tsx`.
   - Lacks a reusable `RequisitionTable` component, `RequisitionDrawer` slide-over panel, and `useRequisitions` hook.

2. **Pipeline & Candidate 360 (`JobPostingDetailPage.tsx`, `ApplicationDebrief.tsx`)**:
   - `JobPostingDetailPage.tsx` mixes vacancy advert editing, public link management, and candidate list rendering.
   - Candidates are rendered as a basic vertical `<ul>` list instead of a high-density Ashby/Linear Kanban Board (`PipelineKanbanBoard`).
   - Candidate details and interview debriefs are expanded inline rather than using a slide-over panel (`CandidateSlideOver`) containing candidate 360 profile, CV viewer, stage history, scorecard summaries, and notes.

3. **Interviews & Blind Scorecards (`InterviewDetailPage.tsx`, `ApplicationDebrief.tsx`, `ApplicationNotes.tsx`)**:
   - Interview scorecard submission and panel evaluation rendering live strictly on a standalone page (`InterviewDetailPage.tsx`).
   - Requiring navigation back and forth between pipeline view and interview page breaks the continuous recruiter flow.
   - Refactoring into `BlindScorecardDrawer` with split view (1-5 ratings on left, panel scores & @mentions notes on right) and `useInterviews` hook will dramatically enhance UX.

---

## 2. Feature-Based Architecture Blueprint (`src/features`)

The target modular directory structure under `frontend/internal/src/features` is detailed below:

```
frontend/internal/src/features/
├── requisitions/
│   ├── components/
│   │   ├── RequisitionTable.tsx
│   │   ├── RequisitionDrawer.tsx
│   │   ├── RequisitionStatusBadge.tsx
│   │   └── RequisitionApprovalTimeline.tsx
│   ├── hooks/
│   │   └── useRequisitions.ts
│   ├── api/
│   │   └── requisitionsApi.ts
│   ├── types/
│   │   └── index.ts (re-export/extend @recruitops/types)
│   ├── index.ts
│   └── __tests__/
│       ├── RequisitionTable.test.tsx
│       ├── RequisitionDrawer.test.tsx
│       └── useRequisitions.test.ts
│
├── pipeline/
│   ├── components/
│   │   ├── PipelineKanbanBoard.tsx
│   │   ├── PipelineColumn.tsx
│   │   ├── CandidateCard.tsx
│   │   ├── CandidateSlideOver.tsx
│   │   ├── CandidateCvViewer.tsx
│   │   ├── CandidateStageHistory.tsx
│   │   ├── CandidateScorecardsSummary.tsx
│   │   └── CandidateNotesTab.tsx
│   ├── hooks/
│   │   └── usePipeline.ts
│   ├── api/
│   │   └── pipelineApi.ts
│   ├── types/
│   │   └── index.ts
│   ├── index.ts
│   └── __tests__/
│       ├── PipelineKanbanBoard.test.tsx
│       ├── CandidateSlideOver.test.tsx
│       └── usePipeline.test.ts
│
└── interviews/
    ├── components/
    │   ├── BlindScorecardDrawer.tsx
    │   ├── ScorecardForm.tsx
    │   ├── RatingInput.tsx
    │   ├── CriterionField.tsx
    │   ├── PanelEvaluationsView.tsx
    │   ├── NotesThread.tsx (re-exports / wraps ApplicationNotes)
    │   └── ScheduleInterviewModal.tsx
    ├── hooks/
    │   └── useInterviews.ts
    ├── api/
    │   └── interviewsApi.ts
    ├── types/
    │   └── index.ts
    ├── index.ts
    └── __tests__/
        ├── BlindScorecardDrawer.test.tsx
        ├── ScorecardForm.test.tsx
        └── useInterviews.test.ts
```

### 2.1 Feature 1: Requisitions (`src/features/requisitions`)

#### Component Specifications
1. **`RequisitionTable.tsx`**:
   - **Purpose**: High-density scannable data table for requisitions.
   - **Props**:
     ```ts
     export interface RequisitionTableProps {
       items: RequisitionListItem[];
       isLoading?: boolean;
       onSelectRequisition?: (id: string) => void;
       onEditDraft?: (id: string) => void;
     }
     ```
   - **Features**:
     - Columns: Position Title, Department, Headcount, Salary Budget (formatted currency), Status (`StatusPill`), Awaiting Approver, Actions.
     - Row click opens `RequisitionDrawer`.
     - Hover highlights, high-density padding (`py-2.5 px-3.5`), monospace headcount/budget text.
     - Empty state card with quick action button.

2. **`RequisitionDrawer.tsx`**:
   - **Purpose**: High-density slide-over panel displaying 360-degree requisition detail, approval step timeline, and decision actions.
   - **Props**:
     ```ts
     export interface RequisitionDrawerProps {
       requisitionId: string | null;
       isOpen: boolean;
       onClose: () => void;
     }
     ```
   - **Features**:
     - Header: Requisition Title, Department, Status Pill, Edit button (for drafts owned by user).
     - Core Metrics Grid: Headcount, Salary budget, Submitted date, Decided date.
     - Job Description section (with formatted text and whitespace preservation).
     - Approval Timeline: Step sequence badges (`1`, `2`), approver name, decision pills (`Waiting`, `Approved`, `Rejected`), decided timestamp, approver comments.
     - Action Bar:
       * Draft: "Submit for approval" button (`POST /api/requisitions/:id/submit`).
       * PendingApproval: "Approve" (success) and "Reject" (danger) buttons with optional comment input (rendered when `activeStep.approverUserId === session.userId`).
       * Cancel / Withdraw button with confirmation prompt.

3. **`useRequisitions.ts`**:
   - **Custom Hook API**:
     ```ts
     export function useRequisitions(initialDepartmentId?: string) {
       const [items, setItems] = useState<RequisitionListItem[] | null>(null);
       const [selectedId, setSelectedId] = useState<string | null>(null);
       const [detail, setDetail] = useState<RequisitionDetail | null>(null);
       const [loading, setLoading] = useState<boolean>(false);
       const [actionBusy, setActionBusy] = useState<boolean>(false);
       const [error, setError] = useState<string | null>(null);

       const loadRequisitions = useCallback(async () => { ... });
       const loadDetail = useCallback(async (id: string) => { ... });
       const submitRequisition = async (id: string) => { ... };
       const decideRequisition = async (id: string, approve: boolean, comment?: string) => { ... };
       const cancelRequisition = async (id: string) => { ... };

       return { items, selectedId, detail, loading, actionBusy, error, setSelectedId, loadRequisitions, loadDetail, submitRequisition, decideRequisition, cancelRequisition };
     }
     ```

---

### 2.2 Feature 2: Pipeline (`src/features/pipeline`)

#### Component Specifications
1. **`PipelineKanbanBoard.tsx`**:
   - **Purpose**: High-density Ashby/Linear style Kanban board for tracking job application stages.
   - **Stages**: `Sourced` | `Applied` | `Screening` | `Shortlisted` | `Interview` | `Offer` | `Hired` | `Rejected`.
   - **Props**:
     ```ts
     export interface PipelineKanbanBoardProps {
       postingId: string;
       items: PipelineItem[];
       applicationFormFieldsJson?: string | null;
       onMoveStage: (applicationId: string, toStatus: PipelineStatus) => Promise<void>;
       onSelectCandidate: (candidateId: string) => void;
     }
     ```
   - **Features**:
     - Columns grouped by pipeline stage with count badges (`Sourced (3)`, `Interview (5)`, etc.).
     - Candidate Cards with candidate name, email/phone, applied date, source channel badge, custom field answers, cover note excerpt, and quick stage move dropdown.
     - Click card triggers `CandidateSlideOver` drawer.

2. **`CandidateSlideOver.tsx` (Candidate 360 Profile Drawer)**:
   - **Purpose**: Comprehensive 360-degree candidate detail drawer with instant slide-over panel.
   - **Props**:
     ```ts
     export interface CandidateSlideOverProps {
       candidateId: string | null;
       isOpen: boolean;
       onClose: () => void;
       onStageChanged?: () => void;
     }
     ```
   - **Tabs**:
     - `Overview`: Applied job title, contact details, source channel, cover note, custom answers.
     - `CV Viewer`: Resume document preview / viewer frame.
     - `Stage History`: Timeline of `StageHistoryItem` rows showing stage transitions (`Sourced` -> `Screening` -> `Interview`), who moved the candidate, timestamp, and notes.
     - `Scorecard Summaries`: Summary of completed rounds, hire recommendations (`StrongYes`, `Yes`, `No`, `StrongNo`), score breakdown per criterion, and button to launch `BlindScorecardDrawer`.
     - `Notes & Debrief`: Embedded `ApplicationNotes` with `@Mentions` thread and round pinning.

3. **`usePipeline.ts`**:
   - **Custom Hook API**:
     ```ts
     export function usePipeline(postingId?: string) {
       const [pipeline, setPipeline] = useState<PipelineItem[]>([]);
       const [selectedCandidateId, setSelectedCandidateId] = useState<string | null>(null);
       const [stageHistory, setStageHistory] = useState<StageHistoryItem[]>([]);
       const [loading, setLoading] = useState<boolean>(false);
       const [movingStage, setMovingStage] = useState<boolean>(false);
       const [error, setError] = useState<string | null>(null);

       const loadPipeline = useCallback(async () => { ... });
       const moveStage = async (applicationId: string, toStatus: PipelineStatus, note?: string) => { ... };
       const loadStageHistory = async (applicationId: string) => { ... };

       return { pipeline, selectedCandidateId, stageHistory, loading, movingStage, error, setSelectedCandidateId, loadPipeline, moveStage, loadStageHistory };
     }
     ```

---

### 2.3 Feature 3: Interviews (`src/features/interviews`)

#### Component Specifications
1. **`BlindScorecardDrawer.tsx`**:
   - **Purpose**: Split-view high-density slide-over drawer for interviewing, filling scorecards, reading blind panel scores, and debriefing.
   - **Props**:
     ```ts
     export interface BlindScorecardDrawerProps {
       interviewId: string | null;
       isOpen: boolean;
       onClose: () => void;
       onScorecardSubmitted?: () => void;
     }
     ```
   - **Layout**:
     - **Left Column / Panel (Evaluation Form)**:
       * Round header: Round number, date/time, duration, mode, location, agenda, panel roster (`InterviewParticipant[]`).
       * Criterion Fields: 1-5 rating buttons (`RatingInput`), Yes/No toggle buttons, comment textareas (`CriterionField`).
       * Overall Recommendation dropdown (`StrongNo`, `No`, `Yes`, `StrongYes`).
       * Summary comment textarea.
       * Unanswered required criteria notice ("Still needed to submit: Technical depth").
       * Action buttons: "Save draft" (PUT) and "Submit evaluation" (POST /submit with confirmation).
     - **Right Column / Panel (Panel Evaluations & @Mentions)**:
       * Blind Panel View: Renders warning banner when blinded (`"{hiddenCount} evaluations are waiting for yours"`), or shows submitted scorecards (`ScorecardView`) once unlocked.
       * Debrief Thread: Embedded `@Mentions` note thread (`ApplicationNotes`) pinned to the round.

2. **`useInterviews.ts`**:
   - **Custom Hook API**:
     ```ts
     export function useInterviews(interviewId?: string) {
       const [interview, setInterview] = useState<Interview | null>(null);
       const [mine, setMine] = useState<MyScorecard | null>(null);
       const [panel, setPanel] = useState<InterviewScorecards | null>(null);
       const [drafts, setDrafts] = useState<Record<string, Draft>>({});
       const [recommendation, setRecommendation] = useState<HireRecommendation | ''>('');
       const [summary, setSummary] = useState<string>('');
       const [loading, setLoading] = useState<boolean>(false);
       const [busy, setBusy] = useState<boolean>(false);
       const [error, setError] = useState<string | null>(null);

       const loadInterviewData = useCallback(async (id: string) => { ... });
       const saveDraft = async () => { ... };
       const submitEvaluation = async () => { ... };
       const scheduleInterview = async (applicationId: string, request: ScheduleInterviewRequest) => { ... };

       return { interview, mine, panel, drafts, recommendation, summary, loading, busy, error, setDrafts, setRecommendation, setSummary, loadInterviewData, saveDraft, submitEvaluation, scheduleInterview };
     }
     ```

---

## 3. Tooling, Workspace & Test Guardrails Inspection

### 3.1 Workspace Package Configuration
- **Root `package.json`**:
  - `workspaces`: `["packages/*", "frontend/internal", "frontend/public"]`
  - `scripts`:
    * `"typecheck": "npm run typecheck --workspaces --if-present"`
    * `"test": "npm run test --workspaces --if-present"`

- **`frontend/internal/package.json`**:
  - `name`: `@recruitops/internal`
  - `scripts`:
    * `"typecheck": "tsc --noEmit"`
    * `"test": "vitest run"`
    * `"test:watch": "vitest"`

### 3.2 Vitest Configuration (`frontend/internal/vitest.config.ts`)
```ts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
});
```
- **Key Insight**: `include: ['src/**/*.test.{ts,tsx}']` automatically matches test files located anywhere within `src/`, including co-located tests inside `src/features/requisitions/__tests__/`, `src/features/pipeline/__tests__/`, and `src/features/interviews/__tests__/`.

### 3.3 Empirical Test Execution Results
Running `npm run test` in `frontend/internal` yields:
- **Test Files**: 10 passed (10)
- **Tests**: 65 passed (65)
- **Duration**: ~8.9 seconds

The 10 existing test files are:
1. `src/components/TenantSwitcherBar.test.tsx` (5 tests)
2. `src/components/RequirePermission.test.tsx` (5 tests)
3. `src/pages/RolesPage.test.tsx` (4 tests)
4. `src/pages/InterviewDetailPage.test.tsx` (6 tests)
5. `src/pages/UsersPage.test.tsx` (6 tests)
6. `src/test/milestone4EmpiricalChallenge.test.tsx` (8 tests)
7. `src/components/AppLayout.test.tsx` (6 tests)
8. `src/components/ApplicationNotes.test.tsx` (9 tests)
9. `src/components/PermissionMatrixGrid.test.tsx` (9 tests)
10. `src/lib/scorecard.test.ts` (7 tests)

### 3.4 TypeScript Workspace Configuration (`frontend/internal/tsconfig.json`)
```json
{
  "compilerOptions": {
    "target": "ES2020",
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noEmit": true,
    "skipLibCheck": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "allowImportingTsExtensions": false
  },
  "include": ["src"]
}
```
- **Typecheck Result**: Executing `npm run typecheck` runs `tsc --noEmit` and returns 0 errors.

---

## 4. Implementation Step-by-Step Blueprint for Implementers

1. **Step 1: Create Feature Folder Skeleton**
   - Create directories:
     * `src/features/requisitions/components`, `hooks`, `api`, `types`, `__tests__`
     * `src/features/pipeline/components`, `hooks`, `api`, `types`, `__tests__`
     * `src/features/interviews/components`, `hooks`, `api`, `types`, `__tests__`

2. **Step 2: Build `src/features/requisitions`**
   - Implement API helpers in `requisitionsApi.ts`.
   - Implement `RequisitionTable.tsx` and `RequisitionDrawer.tsx`.
   - Implement `useRequisitions.ts` hook.
   - Refactor `RequisitionsPage.tsx` and `RequisitionDetailPage.tsx` to utilize the new feature components.
   - Add co-located unit tests in `src/features/requisitions/__tests__/`.

3. **Step 3: Build `src/features/pipeline`**
   - Implement `PipelineKanbanBoard.tsx` (Ashby/Linear style board).
   - Implement `CandidateSlideOver.tsx` with tabs: Overview, CV Viewer, Stage History, Scorecard Summaries, Notes.
   - Implement `usePipeline.ts` hook.
   - Refactor `JobPostingDetailPage.tsx` to embed the new Kanban board and slide-over drawer.
   - Add co-located unit tests in `src/features/pipeline/__tests__/`.

4. **Step 4: Build `src/features/interviews`**
   - Implement `BlindScorecardDrawer.tsx` with split view (left: scoring form & recommendation, right: blind panel view & @mentions note thread).
   - Implement `useInterviews.ts` hook.
   - Refactor `InterviewDetailPage.tsx` to utilize `BlindScorecardDrawer` or re-export component.
   - Maintain full backwards compatibility for `InterviewDetailPage.test.tsx` (all 6 tests passing).
   - Add co-located unit tests in `src/features/interviews/__tests__/`.

5. **Step 5: Execute Quality & Guardrail Verification**
   - Run `npm run typecheck` in `frontend/internal` -> verify 0 errors.
   - Run `npm run test` in `frontend/internal` -> verify 65+ Vitest tests pass.
