# Frontend Architecture Survey & Analysis — Person B Flow 1
**Target Area**: Global Ctrl+K Command Palette & Search Results Page  
**Investigator**: explorer_survey_2  
**Date**: 2026-08-11  

---

## 1. Executive Summary
This report provides a comprehensive survey of the frontend codebase for **Person B - Flow 1** (Global Ctrl+K Command Palette & Search Results Page). The RecruitOps frontend is structured as a feature-based monorepo workspace (`@recruitops/internal`), relying on a shared design system (`@recruitops/ui` in `packages/ui`) and shared DTO/contract definitions (`@recruitops/types` in `packages/types`).

Currently:
- `packages/ui/src/CommandPalette.tsx` exists as a primitive UI component with synchronous local filtering and keyboard navigation (`ArrowUp`, `ArrowDown`, `Enter`, `Escape`).
- `frontend/internal/src/components/AppLayout.tsx` and `Header.tsx` wire up `Ctrl+K` keyboard shortcuts and trigger `CommandPalette` modal with static navigation routes.
- The `/search` route and `SearchResultsPage` component do not exist yet in `App.tsx`.
- Candidate SlideOver (`CandidateSlideOver.tsx`), Requisition Detail (`RequisitionDetailPage.tsx` / `RequisitionDrawer.tsx`), and Job Posting Detail (`JobPostingDetailPage.tsx`) routes/drawers exist and provide clear integration targets for search results navigation.

---

## 2. Topic 1: Frontend Architecture

### Workspace & Monorepo Structure
- **Root Workspace**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps`
- **Internal Web Application**: `frontend/internal/` (`@recruitops/internal`)
  - Tech Stack: React 18.3, React Router DOM 6.26, Vite 5.4, Tailwind CSS 3.4, Vitest 2.1, jsdom 25.0.
  - Entry Point: `frontend/internal/src/main.tsx` → `App.tsx`.
  - Directory Layout:
    - `src/components/`: Layout & shared components (`AppLayout.tsx`, `Header.tsx`, `Sidebar.tsx`, `Breadcrumbs.tsx`, `TenantSwitcherBar.tsx`, `RequireAuth.tsx`, `RequirePermission.tsx`).
    - `src/features/`: Domain-driven feature modules (`analytics/`, `interviews/`, `pipeline/`, `requisitions/`).
    - `src/pages/`: Top-level page views (`AnalyticsPage.tsx`, `JobPostingsPage.tsx`, `JobPostingDetailPage.tsx`, `RequisitionsPage.tsx`, `RequisitionDetailPage.tsx`, `UsersPage.tsx`, `RolesPage.tsx`, etc.).
    - `src/lib/`: Utilities (`api.ts` for fetch wrapper, `auth.ts` for session & permission checks).
- **Shared Design System UI Package**: `packages/ui/` (`@recruitops/ui`)
  - Shared primitive components (`CommandPalette.tsx`, `Input.tsx`, `Tabs.tsx`, `Sheet.tsx`, `Card.tsx`, `Badge.tsx`, `Table.tsx`, `StatusPill.tsx`, `Skeleton.tsx`, `Select.tsx`, `Button.tsx`).
- **Shared Types Package**: `packages/types/` (`@recruitops/types`)
  - Shared TypeScript interfaces & DTOs (`index.ts`, `analytics.ts`).

### API Wrapper & Auth Handling
- `frontend/internal/src/lib/api.ts` provides `api<T>(path, options)` for communicating with the backend (proxied to `http://localhost:5080/api` during dev).
- `frontend/internal/src/lib/auth.ts` provides `auth.get()`, `auth.set()`, `auth.clear()`, and `hasPermission(session, permissionCode)`.

### Search DTO Requirement
Currently, `@recruitops/types` has no search DTOs defined. To support `GET /api/search?q={query}&category={category}`, the following types should be added to `packages/types/src/search.ts` (or `index.ts`):
```typescript
export type SearchCategory = 'All' | 'Candidates' | 'Postings' | 'Requisitions' | 'QuickActions';

export interface SearchResultItem {
  id: string;
  category: 'Candidates' | 'Postings' | 'Requisitions' | 'QuickActions';
  title: string;
  subtitle?: string | null;
  snippet?: string | null;
  path: string;
  candidateId?: string | null;
  jobPostingId?: string | null;
  requisitionId?: string | null;
  score?: number;
}

export interface SearchResponse {
  query: string;
  category: SearchCategory;
  totalCount: number;
  results: SearchResultItem[];
}
```

---

## 3. Topic 2: Global Navigation Setup & Routing

### Current Route Setup (`frontend/internal/src/App.tsx`)
`App.tsx` configures routes inside a `<RequireAuth><AppLayout /></RequireAuth>` wrapper:
- `/requisitions` → `RequisitionsPage`
- `/requisitions/new` → `RequisitionFormPage` (create)
- `/requisitions/:id` → `RequisitionDetailPage`
- `/requisitions/:id/edit` → `RequisitionFormPage` (edit)
- `/jobpostings` → `JobPostingsPage`
- `/jobpostings/:id` → `JobPostingDetailPage`
- `/interviews/:id` → `InterviewDetailPage`
- `/analytics` → `AnalyticsPage`
- `/users` → `UsersPage` (gated by `permission:users:users:read`)
- `/roles` → `RolesPage` (gated by `permission:roles:roles:read`)

**Missing Route**:
- `/search` route needs to be added to `App.tsx`:
```tsx
<Route path="/search" element={<SearchResultsPage />} />
```

### Global Header & AppLayout Integration
- `AppLayout.tsx` (lines 20-30): Registers global window listener for `Ctrl+K` or `Cmd+K` key combination to toggle `isCommandPaletteOpen`.
- `Header.tsx` (lines 24-47): Renders header search trigger button with `Search or jump to... Ctrl+K`. Clicking it calls `onOpenCommandPalette()`.
- `CommandPalette` in `AppLayout.tsx` (lines 153-161): Receives `isOpen`, `onClose`, `onSelectRoute`, and `commandItems`.

---

## 4. Topic 3: Available UI Primitives in `@recruitops/ui`

| UI Primitive | Source File | Capability & Usage for Flow 1 |
|---|---|---|
| `CommandPalette` | `packages/ui/src/CommandPalette.tsx` | Dialog box, query input, categorized results list, keyboard navigation (`ArrowUp`, `ArrowDown`, `Enter`, `Escape`), ESC badge, footer hints. Needs extension/wiring for live backend search API integration. |
| `Input` | `packages/ui/src/Input.tsx` | Styled search input with icon support, clear button, and focus ring. Used in `CommandPalette` and `SearchResultsPage`. |
| `Tabs` | `packages/ui/src/Tabs.tsx` | `TabsList`, `TabsTrigger`, `TabsContent` for category tab filtering (`All`, `Candidates`, `Job Postings`, `Requisitions`). |
| `Sheet` (Drawer) | `packages/ui/src/Sheet.tsx` | Slide-over drawer primitive used by `CandidateSlideOver` and `RequisitionDrawer`. |
| `Card` | `packages/ui/src/Card.tsx` | High-density container card for search result items. |
| `Badge` | `packages/ui/src/Badge.tsx` | Categorical and status badges (`cyan`, `warning`, `success`, `danger`, `neutral`). |
| `StatusPill` | `packages/ui/src/StatusPill.tsx` | Status indicators for Pipeline stages (`Applied`, `Interview`, `Hired`, etc.) and Requisitions (`Approved`, `Draft`, etc.). |
| `Skeleton` | `packages/ui/src/Skeleton.tsx` | Loading placeholders during debounced search query fetching. |

---

## 5. Topic 4: Navigation to Candidate SlideOver, Requisition Detail, & Job Posting Detail

### Navigation Target Matrix

| Result Type | Target Navigation / Action | Detail Handling |
|---|---|---|
| **Requisition** | Navigate to `/requisitions/:id` or open `RequisitionDrawer` | `RequisitionDetailPage.tsx` handles `/requisitions/:id`. `RequisitionDrawer` takes `requisition: RequisitionDetail \| null`. |
| **Job Posting** | Navigate to `/jobpostings/:id` | `JobPostingDetailPage.tsx` handles `/jobpostings/:id`. |
| **Candidate** | Navigate to candidate's posting page or open `CandidateSlideOver` | Navigate to `/jobpostings/:postingId?candidateId=:candidateId` or open `CandidateSlideOver` directly on `/search` page using candidate state. |
| **Quick Action / Nav** | Navigate to route path | e.g. `/requisitions/new`, `/analytics`, `/users`. |

### Candidate SlideOver Details (`features/pipeline/CandidateSlideOver.tsx`)
- Props: `candidate: PipelineItem | null`, `isOpen: boolean`, `onClose: () => void`, `stageHistory`, `interviews`.
- To render `CandidateSlideOver` on `SearchResultsPage` when clicking a candidate result:
  - Keep state `[selectedCandidate, setSelectedCandidate]` on `SearchResultsPage`.
  - Fetch application detail/history via `api<PipelineItem>(`/applications/${id}`)` or construct `PipelineItem` from search result metadata.

---

## 6. Topic 5: Debouncing, Keyboard Navigation, and Text Highlighting Requirements

### 1. Debouncing Requirement (300ms)
- User typing in Command Palette search input triggers live backend search queries.
- Must use a 300ms debounce interval to avoid sending HTTP requests on every keystroke.
- Implementation: Custom hook `useDebounce(value, delay = 300)` or `setTimeout` inside `useEffect`.
- When search query is empty (`""`), return static Quick Actions & Navigation items immediately without calling API.

### 2. Keyboard Navigation Requirements
- `Ctrl+K` / `Cmd+K`: Global toggle to open/close Command Palette modal.
- `ArrowDown`: Move selected highlight down through categorized items list (wrapping around at end).
- `ArrowUp`: Move selected highlight up (wrapping around at start).
- `Enter`: Trigger selection of currently highlighted item.
  - If item is a navigation/quick action: Navigate to route path.
  - If item is a search result (Candidate/Posting/Requisition): Navigate to detail page/drawer.
  - If user presses `Enter` on input with query or clicks "View all results for '{query}'": Navigate to `/search?q={query}` and close modal.
- `Escape`: Close Command Palette.
- Focus Management: Focus input immediately upon opening modal.

### 3. Text Highlighting Requirement
- On `SearchResultsPage` (and optionally inside `CommandPalette` search items), match occurrences of the search query in candidate names, skills, titles, and extracted CV text snippets must be visually highlighted.
- Highlight Component Specification:
```tsx
export function HighlightText({ text, query, className = '' }: { text: string; query: string; className?: string }) {
  if (!query.trim() || !text) return <span className={className}>{text}</span>;
  const parts = text.split(new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi'));
  return (
    <span className={className}>
      {parts.map((part, i) =>
        part.toLowerCase() === query.toLowerCase() ? (
          <mark key={i} className="bg-amber-200 text-ink-900 font-semibold px-0.5 rounded">
            {part}
          </mark>
        ) : (
          part
        )
      )}
    </span>
  );
}
```

---

## 7. Topic 6: Test Suite (Vitest Setup) & Test Structure Strategy

### Current Test Suite Status
- Framework: Vitest 2.1 with `@testing-library/react` and `jsdom`.
- Running Command: `npm run test` inside `frontend/internal`.
- Test Files Location: Co-located next to components (`*.test.tsx`) or inside `__tests__/` subdirectories.

### Recommended Test File Structure for Person B - Flow 1

1. **`frontend/internal/src/features/search/__tests__/CommandPalette.test.tsx`**:
   - `it('opens palette when Ctrl+K is pressed')`
   - `it('shows static navigation items when query is empty')`
   - `it('debounces input by 300ms before calling search API')`
   - `it('renders categorized results (Quick Actions, Candidates, Job Postings, Requisitions)')`
   - `it('supports ArrowUp / ArrowDown keyboard navigation and Enter key selection')`
   - `it('navigates to /search?q={query} when pressing Enter or clicking view all results')`
   - `it('closes palette on Escape key')`

2. **`frontend/internal/src/features/search/__tests__/SearchResultsPage.test.tsx`**:
   - `it('fetches and renders search results for URL search param q')`
   - `it('switches category tabs (All, Candidates, Postings, Requisitions) and filters displayed results')`
   - `it('highlights matched search query terms in candidate names and skills')`
   - `it('opens CandidateSlideOver when candidate result card is clicked')`
   - `it('renders empty state when no results are returned')`
   - `it('displays error state gracefully when search API fails')`

---

## 8. Concrete Implementation Deliverables for Person B Flow 1

1. **Shared Types (`packages/types/src/search.ts` / `index.ts`)**:
   - Add `SearchCategory`, `SearchResultItem`, `SearchResponse`, `SearchQueryParameters`.
2. **Search Feature Module (`frontend/internal/src/features/search/`)**:
   - `searchApi.ts`: `searchContent(query, category)` wrapper calling `GET /api/search`.
   - `useSearch.ts`: Hook for managing debounced query state, API fetching, category tabs, and loading/error states.
   - `HighlightText.tsx`: Text highlighting component.
   - `SearchResultsPage.tsx` (in `pages/`): Search results page with search bar, category tabs, high-density result cards, highlighting, and candidate slideover drawer.
3. **App Integration**:
   - Route `/search` in `App.tsx`.
   - Update `AppLayout.tsx` & `CommandPalette.tsx` to handle live debounced API search queries and navigate to `/search?q={query}`.
4. **Vitest Unit & Integration Tests**:
   - Add `CommandPalette.test.tsx` and `SearchResultsPage.test.tsx` to achieve full test coverage.
