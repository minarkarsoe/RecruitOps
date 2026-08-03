# Project: RecruitOps Frontend CRM Refactor

## Architecture
- Feature-Based (Domain-Driven) Frontend Architecture in `frontend/internal/src/features/`
- Shared Primitive Component Library in `packages/ui/src` (and `frontend/internal/src/components/ui/`)
- Unified Application Shell in `frontend/internal/src/components/AppLayout.tsx` with Header, Navigation Sidebar, Breadcrumbs, Tenant Switcher, and Ctrl+K Command Palette.
- Feature Modules:
  - `src/features/requisitions` (RequisitionTable, RequisitionDrawer, useRequisitions)
  - `src/features/pipeline` (PipelineKanbanBoard, CandidateSlideOver, Candidate 360 profile, CV viewer, stage history, scorecard summaries, notes, usePipeline)
  - `src/features/interviews` (BlindScorecardDrawer, split view 1-5 rating, @Mentions note thread, useInterviews)

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Typography & Tailwind Config | Add Bricolage Grotesque & Inter fonts, Zinc neutrals, Cyan/Teal brand tokens | M1 | ORIGINAL_REQUEST R1 |
| 2 | UI Primitive Library | Build Sheet/Drawer, Badge, Table, CommandPalette, Dialog, Tabs, Skeleton, Input, Select primitives | M1 | ORIGINAL_REQUEST R1 |
| 3 | Application Layout Shell | Redesign AppLayout.tsx with collateral sidebar, header breadcrumbs, user/department switcher, permission-aware actions | M2 | ORIGINAL_REQUEST R2 |
| 4 | Global Ctrl+K Command Palette | Implement Ctrl+K event handler, global search drawer/dialog, route jumping | M2 | ORIGINAL_REQUEST R2 |
| 5 | Requisitions Feature Module | Reconstruct src/features/requisitions with RequisitionTable, RequisitionDrawer, useRequisitions | M3 | ORIGINAL_REQUEST R3 |
| 6 | Candidate Pipeline & 360 Drawer | Reconstruct src/features/pipeline with PipelineKanbanBoard, CandidateSlideOver (360 profile drawer, CV viewer, stage history, scorecard summaries, notes), usePipeline | M3 | ORIGINAL_REQUEST R3 |
| 7 | Blind Scorecard & Interview Module | Reconstruct src/features/interviews with BlindScorecardDrawer (split view 1-5 rating, @Mentions note thread), useInterviews | M3 | ORIGINAL_REQUEST R3 |
| 8 | Workspace Integration & Pages | Update pages (RequisitionsPage, RequisitionDetailPage, InterviewDetailPage, App.tsx) to connect features, passing all Vitest tests and TypeScript typechecks | M4 | ORIGINAL_REQUEST Verification |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Design System & UI Primitives | Upgrade Tailwind config, fonts, index.css; build 9 primitive components in packages/ui & src/components/ui | None | DONE |
| M2 | App Layout & Command Palette | Redesign AppLayout with Sidebar, Header, Breadcrumbs, TenantSwitcher, Ctrl+K CommandPalette | M1 | DONE |
| M3 | Feature Modules Reconstruct | Build features/requisitions, features/pipeline (CandidateSlideOver), features/interviews (BlindScorecardDrawer) | M1, M2 | IN_PROGRESS |
| M4 | Page Integration & Quality Verification | Connect feature components to pages/App.tsx, run npm run typecheck & npm run test verification | M1, M2, M3 | PLANNED |

## Interface Contracts
### UI Primitives ↔ App Shell & Features
- `Sheet` / `Drawer`: `{ isOpen: boolean; onClose: () => void; title?: string; children: React.ReactNode }`
- `CommandPalette`: `{ isOpen: boolean; onClose: () => void; onSelectRoute: (path: string) => void }`
- `Badge`: `{ variant: 'default' | 'cyan' | 'teal' | 'zinc' | 'success' | 'warning' | 'danger'; children: React.ReactNode }`
- `Table`: `{ headers: string[]; data: any[]; renderRow: (item: any) => React.ReactNode }`
- `Tabs`: `{ tabs: { id: string; label: string }[]; activeTab: string; onChange: (id: string) => void }`

### Candidate Pipeline ↔ Candidate 360 SlideOver
- `CandidateSlideOver`: `{ candidateId: string | null; isOpen: boolean; onClose: () => void }`
  - Internal tabs: Overview, CV Viewer, Stage History, Scorecard Summaries, Notes (@Mentions)

### Interviews ↔ BlindScorecardDrawer
- `BlindScorecardDrawer`: `{ interviewId: string | null; isOpen: boolean; onClose: () => void }`
  - Split view: Left = Candidate & Job info, Right = 1-5 Rating inputs & @Mentions note thread.

## Code Layout
- `packages/ui/tailwind-preset.js`: Extended Tailwind tokens (Zinc neutrals, Cyan/Teal brand colors).
- `packages/ui/src/`: UI Primitive components (`Button.tsx`, `Card.tsx`, `StatusPill.tsx`, `Sheet.tsx`, `Badge.tsx`, `Table.tsx`, `CommandPalette.tsx`, `Dialog.tsx`, `Tabs.tsx`, `Skeleton.tsx`, `Input.tsx`, `Select.tsx`, `index.ts`).
- `frontend/internal/src/index.css`: Google Fonts imports (Bricolage Grotesque, Inter, IBM Plex Mono, Noto Sans Myanmar).
- `frontend/internal/src/components/ui/`: Re-exports of primitives.
- `frontend/internal/src/components/AppLayout.tsx`, `Header.tsx`, `Sidebar.tsx`, `Breadcrumbs.tsx`, `TenantSwitcherBar.tsx`, `CommandPalette.tsx`.
- `frontend/internal/src/features/requisitions/`: `RequisitionTable.tsx`, `RequisitionDrawer.tsx`, `useRequisitions.ts`, `index.ts`.
- `frontend/internal/src/features/pipeline/`: `PipelineKanbanBoard.tsx`, `CandidateSlideOver.tsx`, `usePipeline.ts`, `index.ts`.
- `frontend/internal/src/features/interviews/`: `BlindScorecardDrawer.tsx`, `useInterviews.ts`, `index.ts`.
