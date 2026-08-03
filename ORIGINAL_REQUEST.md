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
