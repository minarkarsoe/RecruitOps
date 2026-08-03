# Comprehensive Survey & Analysis: Requirement R1 (Design System & UI Primitives)

**Author:** Explorer 1 (Design System & UI Primitives)  
**Date:** 2026-08-03  
**Target:** RecruitOps Design System (`packages/ui` & `frontend/internal/src/components/ui`)  
**Reference Specification:** `RecruitOps_Design_System.md` ("Clear Pipeline" Design System) & `ORIGINAL_REQUEST.md`  

---

## 1. Executive Summary

Requirement R1 mandates upgrading the RecruitOps design system and building a comprehensive library of reusable UI primitives in `packages/ui` (and `frontend/internal/src/components/ui`). Currently, `packages/ui` contains only 3 components (`Button`, `Card`, `StatusPill`) and a basic Tailwind preset (`tailwind-preset.js`). While the color tokens and status pill styles match the "Clear Pipeline" design specification, critical design elements and primitive UI components required for high-density CRM layouts (Ashby/Linear style) are missing.

Key findings include:
- **Font Imports Missing:** Google Fonts imports for **Bricolage Grotesque**, **Inter**, **IBM Plex Mono**, and **Noto Sans Myanmar** are absent from `frontend/internal/index.html` and `index.css`.
- **Color Tokens & Neutrals:** The Tailwind preset defines custom `ink`, `surface`, `line`, and `primary` (Teal `#0E6E6B`) tokens. Adding aliases/mappings for `zinc` neutrals and `cyan`/`teal` brand tokens will ensure seamless developer experience across components.
- **Missing Primitive Components (9 Core Primitives):**
  1. **Sheet / Drawer** (slide-over panel for Candidate 360 profile, Requisition details, Scorecard debriefs)
  2. **Badge** (tier badges, department tags, role pills, status indicators)
  3. **Table** (high-density, scannable, sortable table subcomponents)
  4. **CommandPalette** (Ctrl+K global search and navigation overlay)
  5. **Dialog** (modal dialogs for confirmation and forms)
  6. **Tabs** (underline-style tabs as per §5.8)
  7. **Skeleton** (animated placeholder loaders for high-density tables/cards/drawers)
  8. **Input** (form text inputs with integrated labels and error states)
  9. **Select** (form select dropdowns with integrated labels and error states)

---

## 2. Detailed Assessment of Current Design System Infrastructure

### 2.1 Tailwind Configuration (`packages/ui/tailwind-preset.js`)

**File Location:** `packages/ui/tailwind-preset.js`  
**Import Status:** Imported by `frontend/internal/tailwind.config.js` and `frontend/public/tailwind.config.js`.

**Current Token Definitions:**
```js
export default {
  theme: {
    extend: {
      colors: {
        ink: { 900: '#16232B', 600: '#4A5B66', 400: '#8A99A3' },
        line: { 200: '#E3E9EC' },
        surface: { 0: '#FFFFFF', 50: '#F6F9F9' },
        primary: { 700: '#0B5654', 600: '#0E6E6B', 100: '#DCEFEE' },
        accent: { 500: '#F2A33C', 100: '#FCF0DC' },
        success: { 600: '#1E8E5A', 100: '#E2F4EA' },
        warning: { 600: '#C97A0A', 100: '#FCF0DC' },
        danger: { 600: '#C94430', 100: '#FBE8E4' },
        info: { 600: '#2E6ECF', 100: '#E6EEFB' },
      },
      fontFamily: {
        sans: ['Inter', '"Noto Sans Myanmar"', 'system-ui', 'sans-serif'],
        display: ['"Bricolage Grotesque"', 'Inter', '"Noto Sans Myanmar"', 'sans-serif'],
        mono: ['"IBM Plex Mono"', 'monospace'],
      },
      borderRadius: { sm: '8px', md: '12px', lg: '16px', full: '999px' },
      boxShadow: {
        card: '0 1px 2px rgba(22,35,43,0.06)',
        pop: '0 8px 24px rgba(22,35,43,0.12)',
      },
    },
  },
};
```

**Gaps & Recommendations:**
1. **Zinc Neutrals & Cyan/Teal Brand Aliases:**
   Requirement R1 specifies "Zinc neutrals, Cyan/Teal primary brand tokens". In `tailwind-preset.js`, `primary-600` is `#0E6E6B` (Cyan/Teal family) and neutrals use custom names (`ink-900`, `ink-600`, `line-200`, `surface-50`).
   *Recommendation:* Extend `colors` in `tailwind-preset.js` to alias `zinc` to the neutral palette (or include standard Tailwind `zinc`) and alias `cyan`/`teal` to `primary` brand tokens so both token names work without breaking existing styles.

2. **Font Imports:**
   Neither `frontend/internal/index.html` nor `frontend/internal/src/index.css` imports Google Fonts.
   *Recommendation:* Add `<link>` tags in `frontend/internal/index.html` or `@import` in `index.css`:
   - `Bricolage Grotesque` (weights: 600, 700)
   - `Inter` (weights: 400, 500, 600, 700)
   - `IBM Plex Mono` (weights: 400, 600)
   - `Noto Sans Myanmar` (weights: 400, 600)

---

## 3. Existing UI Components Assessment (`packages/ui/src`)

Currently, `packages/ui/src` contains only 3 component files:

| Component | File Path | Current Status & Capabilities | Gaps / Issues |
|---|---|---|---|
| `Button` | `packages/ui/src/Button.tsx` | Supports `primary`, `secondary`, `ghost`, `danger`. Height 40px (`h-10`), rounded-md. Focus visible ring. | Lacks icon support props (`leftIcon`, `rightIcon`), loading state spinner, or size variants (`sm`, `md`, `lg`). |
| `Card` | `packages/ui/src/Card.tsx` | Supports `title` and `action`. White bg, `line-200` border, `shadow-card`, padding 24. | Standard card panel. Good baseline. |
| `StatusPill` | `packages/ui/src/StatusPill.tsx` | Signature component (§5.2). Supports candidate pipeline, requisition, job posting, and interview status tokens. Uses 6px dot + text label. | Well-designed. Can be complemented by generic `Badge` for non-status tags (tier badges, roles, counts). |

---

## 4. Missing Primitive Components Specification

To support the Ashby / Linear high-density CRM experience (Requirements R1, R2, R3), the following 9 primitive UI components must be created in `packages/ui/src` (and exported via `packages/ui/src/index.ts` and re-exported in `frontend/internal/src/components/ui/index.ts`).

### 4.1 Sheet / Drawer (`Sheet.tsx` / `Drawer.tsx`)
- **Purpose:** Slide-over detail panel from the right edge of the screen for Candidate 360 profiles, Requisition detail previews, and Blind Scorecards.
- **Key Features:**
  - Fixed semi-transparent backdrop (`bg-ink-900/40 backdrop-blur-xs z-50`).
  - Right-aligned panel (`fixed inset-y-0 right-0 z-50 w-full max-w-2xl bg-surface-0 shadow-pop flex flex-col`).
  - Slide-in animation (`transform transition-transform duration-200 ease-in-out`).
  - Accessibility: Close button (`X`), `Escape` key event listener, click-outside overlay to dismiss.
  - Subcomponents: `Sheet`, `SheetHeader`, `SheetTitle`, `SheetDescription`, `SheetBody`, `SheetFooter`.

### 4.2 Badge (`Badge.tsx`)
- **Purpose:** Render client tier badges (Gold/Silver/Bronze as specified in §5.3), department badges, role indicators, counts, and metadata tags.
- **Key Features:**
  - Variants: `default` (ink), `primary` (teal), `secondary` (line/surface), `success`, `warning`, `danger`, `info`, `gold`, `silver`, `bronze`.
  - Client Tier preset styling (§5.3):
    - Gold: `#D9A441` text / `#FBF3E1` bg + crown icon
    - Silver: `#8F9CA8` text / `#EFF2F5` bg
    - Bronze: `#B0784A` text / `#F6ECE3` bg
  - Sizes: `sm` (height 20px), `md` (height 24px). Pill shape (`rounded-full`).

### 4.3 Table (`Table.tsx`)
- **Purpose:** High-density, scannable data grid for Requisitions, Candidates, Job Postings, and Users following Design System §5.6.
- **Key Features:**
  - Row height min 48px, horizontal bottom rule (`border-b border-line-200`), no vertical rules, no zebra striping.
  - Header row: `bg-surface-50 text-[11px] uppercase tracking-wider text-ink-600 font-semibold`.
  - Hover effect: `hover:bg-surface-50`. Selected row: `bg-primary-100/50`.
  - Subcomponents: `Table`, `TableHeader`, `TableBody`, `TableFooter`, `TableRow`, `TableHead`, `TableCell`, `TableCaption`.

### 4.4 CommandPalette (`CommandPalette.tsx`)
- **Purpose:** Global Ctrl+K / Cmd+K search & quick action overlay (Acceptance Criterion: "Global Ctrl+K Command Palette opens and allows searching & navigation").
- **Key Features:**
  - Open state triggered by `Ctrl+K` / `Cmd+K` key combination or header search trigger button.
  - Centered overlay modal (`fixed inset-0 z-50 flex items-start justify-center pt-20 bg-ink-900/50 backdrop-blur-sm`).
  - Search input with auto-focus, magnifying glass icon, and clear button.
  - Action list grouped by category: **Navigation** (Requisitions, Job Postings, Inbox, Departments, Users), **Quick Actions** (+ New Requisition, + New Job Posting), **Candidates** (search results).
  - Full keyboard navigation: Up/Down arrow key highlight, Enter to select/navigate, Escape to dismiss.

### 4.5 Dialog / Modal (`Dialog.tsx`)
- **Purpose:** Modal dialog overlay for confirmations (e.g. submitting irreversible scorecards, cancelling requisitions) and form modals.
- **Key Features:**
  - Backdrop overlay (`fixed inset-0 z-50 bg-ink-900/50 flex items-center justify-center p-4`).
  - Modal panel (`bg-surface-0 rounded-lg border border-line-200 shadow-pop max-w-lg w-full overflow-hidden`).
  - Subcomponents: `Dialog`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogBody`, `DialogFooter`.
  - Keyboard `Escape` dismiss and click-backdrop dismiss.

### 4.6 Tabs (`Tabs.tsx`)
- **Purpose:** Underline-style tab navigation as specified in §5.8.
- **Key Features:**
  - Active tab: `text-ink-900 font-semibold border-b-2 border-primary-600`.
  - Inactive tab: `text-ink-600 hover:text-ink-900 font-medium`.
  - Tab height 44px, gap 24px.
  - Subcomponents: `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent`.

### 4.7 Skeleton (`Skeleton.tsx`)
- **Purpose:** High-density placeholder loader for tables, candidate cards, and slide-over drawers while async data loads.
- **Key Features:**
  - `animate-pulse bg-line-200/70 rounded-md`.
  - Pre-packaged layouts: `SkeletonText`, `SkeletonAvatar`, `SkeletonRow`, `SkeletonCard`.

### 4.8 Input (`Input.tsx`)
- **Purpose:** Standardized form input field matching §5.5 (Height 40px, 8px radius, focus ring, integrated label and error message).
- **Key Features:**
  - Integrated `label` prop, `error` message prop, `helperText` prop.
  - Styling: `h-10 w-full rounded-sm border border-line-200 bg-surface-0 px-3 text-[15px] focus:outline-none focus:ring-2 focus:ring-primary-600`.
  - Error state styling: `border-danger-600 focus:ring-danger-600`.

### 4.9 Select (`Select.tsx`)
- **Purpose:** Standardized form dropdown select field matching §5.5.
- **Key Features:**
  - Integrated `label` prop, `error` message prop, `options` array or native children support.
  - Styling: `h-10 w-full rounded-sm border border-line-200 bg-surface-0 px-3 text-[15px] focus:outline-none focus:ring-2 focus:ring-primary-600`.

---

## 5. TypeScript & Workspace Export Structure

### Packages & Workspaces Structure:
```
packages/ui/
├── package.json               # Exports '.' -> src/index.ts and './tailwind-preset' -> tailwind-preset.js
├── tailwind-preset.js         # Single source of truth for colors, fonts, radius, shadows
└── src/
    ├── index.ts               # Central export file for all UI primitives
    ├── Button.tsx
    ├── Card.tsx
    ├── StatusPill.tsx
    ├── Sheet.tsx              # NEW: Slide-Over Drawer primitive
    ├── Badge.tsx              # NEW: Tier & status badge primitive
    ├── Table.tsx              # NEW: High-density Table subcomponents
    ├── CommandPalette.tsx     # NEW: Ctrl+K Command Palette
    ├── Dialog.tsx             # NEW: Modal Dialog primitive
    ├── Tabs.tsx               # NEW: Underline Tabs primitive
    ├── Skeleton.tsx           # NEW: Loading Skeletons
    ├── Input.tsx              # NEW: Form Input primitive
    └── Select.tsx             # NEW: Form Select primitive
```

Re-export bridge in `frontend/internal`:
```
frontend/internal/src/components/ui/index.ts -> export * from '@recruitops/ui';
```
This ensures zero breakage for components importing from `@recruitops/ui` or `components/ui`.

---

## 6. Recommended Implementation Steps for Implementer

1. **Step 1: Update Font Imports & Tailwind Preset Mappings**
   - Add Google Fonts stylesheet links in `frontend/internal/index.html` (Bricolage Grotesque, Inter, IBM Plex Mono, Noto Sans Myanmar).
   - Update `packages/ui/tailwind-preset.js` to alias `zinc` and `cyan`/`teal` color scales to match token definitions.

2. **Step 2: Implement Core Form & Loading Primitives**
   - Create `packages/ui/src/Input.tsx`
   - Create `packages/ui/src/Select.tsx`
   - Create `packages/ui/src/Skeleton.tsx`
   - Create `packages/ui/src/Badge.tsx`

3. **Step 3: Implement Layout & Navigation Primitives**
   - Create `packages/ui/src/Table.tsx` (`Table`, `TableHeader`, `TableRow`, `TableCell`, etc.)
   - Create `packages/ui/src/Tabs.tsx` (`Tabs`, `TabsList`, `TabsTrigger`, `TabsContent`)
   - Create `packages/ui/src/Dialog.tsx` (`Dialog`, `DialogHeader`, `DialogBody`, `DialogFooter`)

4. **Step 4: Implement High-Density Overlay Primitives**
   - Create `packages/ui/src/Sheet.tsx` (Slide-over drawer with backdrop, right slide animation, escape listener)
   - Create `packages/ui/src/CommandPalette.tsx` (Ctrl+K modal with search, hotkey listener, keyboard navigation)

5. **Step 5: Export & Bridge Components**
   - Export all primitives in `packages/ui/src/index.ts`.
   - Create `frontend/internal/src/components/ui/index.ts` re-exporting `@recruitops/ui`.

6. **Step 6: Verification**
   - Run `npm run typecheck` across all workspaces (expect 0 errors).
   - Run `npm run test --workspace @recruitops/internal` (expect 60+ Vitest tests passing).
