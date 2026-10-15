# Handoff Report — Milestone 1 (Design System Polish & Signature Components) Implementation Plan

**Agent**: Explorer (`teamwork_preview_explorer_m1_1_gen5`)  
**Working Directory**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1_gen5`  
**Target Milestone**: Milestone 1 (Design System Polish & Signature Components)  

---

## 1. Observation

### 1.1 Baseline System Verification
- **Command**: `npm run typecheck` in `frontend/internal` -> Exit Code 0 (0 TypeScript errors).
- **Command**: `npm run test -- --run` in `frontend/internal` -> Exit Code 0 (22 test files passed, 189 tests passed).
- Existing test suites cover `primitives.test.tsx`, `challenger_m1_2.test.tsx`, `AppLayout.test.tsx`, `pipeline.test.tsx`, `requisitions.test.tsx`, `interviews.test.tsx`.

### 1.2 Code Base Audit & File Locations
1. **Typography & Line-Height**:
   - `frontend/internal/src/index.css` (line 14): currently has `line-height: 1.6;`.
   - `frontend/public/app/globals.css` (line 11): currently has `line-height: 1.6;`.
   - `frontend/public/app/layout.tsx` (lines 1-18): root layout lacks Google Fonts stylesheet links (`Bricolage Grotesque`, `Inter`, `Noto Sans Myanmar`, `IBM Plex Mono`).

2. **StatusPill Primitive**:
   - `packages/ui/src/StatusPill.tsx`: status styles mapping (`STYLES`) currently covers candidate pipeline (`Sourced`, `Applied`, `Screening`, `Shortlisted`, `Interview`, `Offer`, `Hired`, `Rejected`), requisition (`Draft`, `PendingApproval`, `Approved`, `Cancelled`), job posting (`Live`, `Closed`), and interview (`Scheduled`, `Completed`, `NoShow`).
   - Missing required extended vocabulary: `Sent to Client` (info), `Placed` (success), `Accepted` (success), `Need More Info` (warning), `Active` (success), `Expiring Soon` (warning), `Expired` (danger).

3. **Signature Components**:
   - `PipelineStageRail`: No standalone component in `packages/ui/src/`. Design system §6.1 requires horizontal stage count rail (`Sourced 24 -> Shortlisted 8 -> Sent 5 -> Interview 2 -> Placed 1`) with mono counts and status pill colors.
   - `ExpiryAttentionCard`: No standalone component in `packages/ui/src/`. Design system §6.4 requires dashboard card listing expiring contracts with urgency color-coded mono countdowns (>30d ink, 8-30d warning, <=7d danger) and "Renew" action.
   - `ClientPortalCard` & `ClientFeedbackBar`: No standalone component in `packages/ui/src/`. Design system §6.2 & §6.3 require candidate card (avatar, name, role, quiet chips, CV button) + full-width 44px feedback bar (`Accept for Interview`, `Need More Info`, `Reject`).

4. **Package Exports**:
   - `packages/ui/src/index.ts`: Exports 12 primitives (Button, Card, Sheet, Badge, Table, CommandPalette, Dialog, Tabs, Skeleton, Input, Select, StatusPill). Must be updated to export `PipelineStageRail`, `ExpiryAttentionCard`, `ClientPortalCard`, and `ClientFeedbackBar`.

---

## 2. Logic Chain

1. **Font Consistency & Line-Height Alignment**:
   - Standardizing `line-height: 1.7;` across both `frontend/internal/src/index.css` and `frontend/public/app/globals.css` satisfies the Burmese-safe typography specification (line-height ≥ 1.7) in prompt Requirement R2.
   - Adding the Google Fonts `<link>` stylesheet to `frontend/public/app/layout.tsx` guarantees that Next.js client portal pages load `Bricolage Grotesque`, `Inter`, `Noto Sans Myanmar`, and `IBM Plex Mono` fonts cleanly without font FOUT or fallback layout shifts.

2. **StatusPill Vocabulary Completeness**:
   - Extending `StatusPill.tsx` with `Sent to Client` (info), `Placed` (success), `Accepted` (success), `Need More Info` (warning), `Active` (success), `Expiring Soon` (warning), and `Expired` (danger) completes the design system status badge language (§5.2).
   - Supporting both string literals with spaces (e.g. `'Sent to Client'`) and PascalCase variants (e.g. `'SentToClient'`) prevents runtime type mismatch or missing style keys.

3. **Signature Primitives Design Integrity**:
   - `PipelineStageRail`: Renders horizontal pipeline stage counts with mono numbers (`font-mono`), status pill colors for active stages, and arrow separators.
   - `ExpiryAttentionCard`: Renders contract/job countdowns with urgency color coding (>30d ink, 8–30d warning, ≤7d danger), client tier badges, and "Renew" action buttons.
   - `ClientPortalCard` & `ClientFeedbackBar`: Premium candidate layout (avatar 56, name, role, quiet chips, CV button) coupled with a 44px high feedback bar (`Accept for Interview`, `Need More Info`, `Reject`) with instant confirmation states.

4. **Package Export & Unit Testing**:
   - Re-exporting all new signature components in `packages/ui/src/index.ts` allows both internal app and public/portal apps to import from `@recruitops/ui`.
   - Comprehensive unit tests in `frontend/internal/src/components/ui/signatureComponents.test.tsx` guarantee 100% test coverage and non-regression across all signature components.

---

## 3. Caveats

- **Dual Surface Context**: `frontend/public` is a Next.js App Router application whereas `frontend/internal` is a Vite SPA. Font links in `layout.tsx` must use React JSX `<link>` tags inside `<html><head>...`.
- **Burmese Line Height**: Setting `line-height: 1.7;` globally on `body` affects all text blocks; component-level line-height utility overrides (e.g., `leading-none`, `leading-tight`) on buttons and pills remain untouched as designed.

---

## 4. Conclusion & Step-by-Step Implementation Plan for Worker

The Worker agent should execute the following 5 tasks sequentially:

### Task 1: Update Fonts & Line-height (`frontend/internal` & `frontend/public`)
1. **File**: `frontend/internal/src/index.css`
   - Modify line 14: Change `line-height: 1.6;` to `line-height: 1.7;`
2. **File**: `frontend/public/app/globals.css`
   - Add `@import url('https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,600;12..96,700&family=IBM+Plex+Mono:wght@400;600&family=Inter:wght@400;500;600;700&family=Noto+Sans+Myanmar:wght@400;600;700&display=swap');` at top.
   - Modify line 11: Change `line-height: 1.6;` to `line-height: 1.7;`
3. **File**: `frontend/public/app/layout.tsx`
   - Update `RootLayout` to render Google Fonts links inside `<head>`:
     ```tsx
     export default function RootLayout({ children }: { children: React.ReactNode }) {
       return (
         <html lang="en">
           <head>
             <link rel="preconnect" href="https://fonts.googleapis.com" />
             <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
             <link
               href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,600;12..96,700&family=IBM+Plex+Mono:wght@400;600&family=Inter:wght@400;500;600;700&family=Noto+Sans+Myanmar:wght@400;600;700&display=swap"
               rel="stylesheet"
             />
           </head>
           <body>{children}</body>
         </html>
       );
     }
     ```

### Task 2: Extend `StatusPill` Vocabulary (`packages/ui/src/StatusPill.tsx`)
1. **File**: `packages/ui/src/StatusPill.tsx`
   - Update `Status` type definition:
     ```tsx
     export type StatusPillVocabulary =
       | PipelineStatus
       | RequisitionStatus
       | JobStatus
       | InterviewStatus
       | 'Sent to Client' | 'SentToClient'
       | 'Placed'
       | 'Accepted'
       | 'Need More Info' | 'NeedMoreInfo'
       | 'Active'
       | 'Expiring Soon' | 'ExpiringSoon'
       | 'Expired';
     ```
   - Update `STYLES` dictionary mapping:
     ```tsx
     // Extended vocabulary (Design System §5.2)
     'Sent to Client': 'bg-info-100 text-info-600',
     SentToClient: 'bg-info-100 text-info-600',
     Placed: 'bg-success-100 text-success-600',
     Accepted: 'bg-success-100 text-success-600',
     'Need More Info': 'bg-warning-100 text-warning-600',
     NeedMoreInfo: 'bg-warning-100 text-warning-600',
     Active: 'bg-success-100 text-success-600',
     'Expiring Soon': 'bg-warning-100 text-warning-600',
     ExpiringSoon: 'bg-warning-100 text-warning-600',
     Expired: 'bg-danger-100 text-danger-600',
     ```
   - Update `StatusPill` prop type: `{ status: StatusPillVocabulary }`.

### Task 3: Build Signature Components in `packages/ui/src/`

1. **File**: `packages/ui/src/PipelineStageRail.tsx`
   - Create component for horizontal stage count rail (`Sourced 24 → Shortlisted 8 → Sent 5 → Interview 2 → Placed 1`).
   - Support click interactions (`onStageClick`), mono counts (`font-mono`), active stage highlighting matching status pill colors, and separator arrows (`→`).
   - Default stages: Sourced (24), Shortlisted (8), Sent to Client (5), Interview (2), Placed (1).

2. **File**: `packages/ui/src/ExpiryAttentionCard.tsx`
   - Create dashboard card component listing contract expirations.
   - Urgency color coding:
     - >30 days: ink (`bg-surface-50 text-ink-900 border-line-200`)
     - 8–30 days: warning (`bg-accent-100 text-warning-600 border-warning-200`)
     - ≤7 days: danger (`bg-danger-100 text-danger-600 border-danger-200`)
   - IBM Plex Mono (`font-mono`) countdown labels ("21 days"), client tier badges (Gold/Silver/Bronze), and "Renew" action button per row.

3. **File**: `packages/ui/src/ClientPortalCard.tsx`
   - Create `ClientFeedbackBar` component:
     - 44px height full-width row with 3 buttons: `Accept for Interview` (solid success-600), `Need More Info` (secondary with warning-600 text), `Reject` (ghost with danger-600 text).
     - Confirmed status pill state upon selection.
   - Create `ClientPortalCard` component:
     - Radius 16 (`rounded-2xl`), padding 32 (`p-8`), avatar 56 (`w-14 h-14`), candidate name (h2), role, quiet chips (experience, salary, notice period, location), skills tags, CV button (`secondary`), and integrated `ClientFeedbackBar`.

### Task 4: Re-export Components in `packages/ui/src/index.ts`
1. **File**: `packages/ui/src/index.ts`
   - Add exports for `PipelineStageRail`, `ExpiryAttentionCard`, `ClientPortalCard`, `ClientFeedbackBar`, and their prop types.

### Task 5: Define Component Unit Test Suite
1. **File**: `frontend/internal/src/components/ui/signatureComponents.test.tsx`
   - Create comprehensive Vitest suite checking:
     - `StatusPill` extended vocabulary rendering and color classes.
     - `PipelineStageRail` rendering, mono numbers, active stage styles, and click callbacks.
     - `ExpiryAttentionCard` countdown urgency styling (>30d ink, 8-30d warning, <=7d danger) and Renew callbacks.
     - `ClientPortalCard` layout, quiet chips, CV button, and `ClientFeedbackBar` click callbacks.

---

## 5. Verification Method

To independently verify the implementation:

1. **TypeScript Typecheck**:
   ```bash
   cd frontend/internal
   npm run typecheck
   ```
   *Expected outcome*: Exit Code 0 with 0 errors across all workspaces.

2. **Vitest Unit Test Suite**:
   ```bash
   cd frontend/internal
   npm run test -- --run
   ```
   *Expected outcome*: 100% pass across all test files including `signatureComponents.test.tsx`.

3. **File Inspection**:
   - `frontend/internal/src/index.css` & `frontend/public/app/globals.css` (verify `line-height: 1.7;`)
   - `frontend/public/app/layout.tsx` (verify Google Fonts `<link>` tags)
   - `packages/ui/src/StatusPill.tsx` (verify extended STYLES dictionary)
   - `packages/ui/src/PipelineStageRail.tsx` (verify mono counts and stage chips)
   - `packages/ui/src/ExpiryAttentionCard.tsx` (verify countdown urgency logic & Renew button)
   - `packages/ui/src/ClientPortalCard.tsx` (verify avatar 56, quiet chips, 44px feedback bar)
   - `packages/ui/src/index.ts` (verify re-exports)
