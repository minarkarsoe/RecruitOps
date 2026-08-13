# Technical Analysis: Global Command Palette UI Integration, Debounced Search, Keyboard Navigation & Vitest Suite (Milestone 2)

## Executive Summary
This document provides the complete architectural blueprint for Milestone 2 of the RecruitOps search feature. It details the frontend design of the global `Ctrl+K` / `Cmd+K` Command Palette UI integration, the live 300ms debounced search execution against the backend `GET /api/search` endpoint, categorized search result rendering (Quick Actions, Candidates, Requisitions, Job Postings), full keyboard navigation (Up/Down arrow, Enter, Escape), and the Vitest test suite design in `frontend/internal/src/features/search/__tests__/CommandPalette.test.tsx`.

Current Baseline Verification:
- Backend: **387 tests passing** (`dotnet test backend/RecruitOps.sln`)
- Frontend: **274 tests passing** (`npm run test` in `frontend/internal`)
- Typecheck: **0 errors** across all workspaces (`npm run typecheck`)

---

## 1. Component Architecture & Integration Blueprint

### 1.1 Shared Search DTOs (`packages/types/src/index.ts`)
To align with backend DTOs (`SearchDtos.cs` in `RecruitOps.Application`), the shared types package must export the full search contract:

```ts
export type SearchCategory = 'All' | 'Candidates' | 'Postings' | 'Requisitions';

export interface SearchQueryParameters {
  q: string;
  category?: SearchCategory | string;
  page?: number;
  pageSize?: number;
}

export interface SearchResultItem {
  id: string;
  category: 'Candidates' | 'Postings' | 'Requisitions' | string;
  title: string;
  subtitle: string;
  descriptionSnippet?: string | null;
  targetUrl: string;
  departmentId?: string | null;
  departmentName?: string | null;
  relevanceScore: number;
  createdAt: string;
}

export interface CategoryCounts {
  all: number;
  candidates: number;
  postings: number;
  requisitions: number;
}

export interface SearchResponse {
  query: string;
  normalizedQuery: string;
  category: string;
  totalMatches: number;
  categoryCounts: CategoryCounts;
  items: SearchResultItem[];
  page: number;
  pageSize: number;
  totalPages: number;
}
```

### 1.2 Frontend Search Feature Structure (`frontend/internal/src/features/search/`)
Create the new feature directory structure:
```
frontend/internal/src/features/search/
├── searchApi.ts                        # HTTP API service for /api/search
├── useSearch.ts                        # Custom React hook for debounced search state
├── HighlightText.tsx                   # Component to safely render matched search snippets
└── __tests__/
    └── CommandPalette.test.tsx         # Comprehensive Vitest test suite
```

#### `searchApi.ts`
```ts
import { auth } from '../../lib/auth';
import type { SearchQueryParameters, SearchResponse } from '@recruitops/types';

export const searchApi = {
  async search(params: SearchQueryParameters): Promise<SearchResponse> {
    const session = auth.get();
    const queryParams = new URLSearchParams();
    queryParams.set('q', params.q);
    if (params.category) queryParams.set('category', params.category);
    if (params.page) queryParams.set('page', params.page.toString());
    if (params.pageSize) queryParams.set('pageSize', params.pageSize.toString());

    const res = await fetch(`/api/search?${queryParams.toString()}`, {
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${session?.accessToken || ''}`,
      },
    });

    if (!res.ok) {
      throw new Error(`Search request failed with status ${res.status}`);
    }

    return res.json();
  },
};
```

#### `useSearch.ts`
```ts
import { useState, useEffect, useRef } from 'react';
import { searchApi } from './searchApi';
import type { SearchResponse, SearchCategory } from '@recruitops/types';

export function useSearch(initialQuery = '', category: SearchCategory = 'All', debounceMs = 300) {
  const [query, setQuery] = useState(initialQuery);
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery);
  const [results, setResults] = useState<SearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  // Debounce query state update
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedQuery(query.trim());
    }, debounceMs);
    return () => clearTimeout(handler);
  }, [query, debounceMs]);

  // Execute search when debouncedQuery changes
  useEffect(() => {
    if (!debouncedQuery) {
      setResults(null);
      setIsLoading(false);
      setError(null);
      return;
    }

    setIsLoading(true);
    setError(null);

    searchApi.search({ q: debouncedQuery, category, page: 1, pageSize: 20 })
      .then((data) => {
        setResults(data);
        setIsLoading(false);
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Search failed');
        setIsLoading(false);
      });
  }, [debouncedQuery, category]);

  return { query, setQuery, debouncedQuery, results, isLoading, error };
}
```

---

## 2. Command Palette UI Primitive (`packages/ui/src/CommandPalette.tsx`)

### 2.1 Interface & Component Enhancements
To accommodate both static commands and dynamic search API results, update `CommandPaletteProps`:

```ts
export interface CommandItem {
  id: string;
  title: string;
  description?: string;
  category?: 'Navigation' | 'Quick Actions' | 'Candidates' | 'Requisitions' | 'Job Postings' | string;
  icon?: React.ReactNode;
  shortcut?: string;
  path?: string;
  onSelect?: () => void;
}

export interface CommandPaletteProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectRoute?: (path: string) => void;
  items?: CommandItem[];
  searchResults?: CommandItem[];
  query?: string;
  onQueryChange?: (q: string) => void;
  isLoading?: boolean;
  placeholder?: string;
}
```

### 2.2 Integration in `AppLayout.tsx` & `Header.tsx`
- In `Header.tsx`, the search trigger button calls `onOpenCommandPalette()`.
- In `AppLayout.tsx`:
  1. `useSearch` hook supplies `query`, `setQuery`, `results`, `isLoading`.
  2. Map `results?.items` to dynamic `CommandItem`s:
     - `Candidates` -> category: `'Candidates'`, path: `/candidates/${item.id}`
     - `Requisitions` -> category: `'Requisitions'`, path: `/requisitions/${item.id}`
     - `Postings` -> category: `'Job Postings'`, path: `/jobpostings/${item.id}`
  3. Combine static permission-filtered `commandItems` (Quick Actions & Navigation) with dynamic `searchResults`.
  4. Passing `onSelectRoute={(path) => { navigate(path); setIsCommandPaletteOpen(false); }}`.

---

## 3. Keyboard Navigation Specification

### 3.1 Keydown Handlers & Modular Focus Index
Keyboard navigation operates across the aggregated flat array of filtered/combined `allItems`:

1. **`ArrowDown`**: `setSelectedIndex((prev) => (allItems.length > 0 ? (prev + 1) % allItems.length : 0))`
2. **`ArrowUp`**: `setSelectedIndex((prev) => (allItems.length > 0 ? (prev - 1 + allItems.length) % allItems.length : 0))`
3. **`Enter`**: Executes `handleExecuteItem(allItems[selectedIndex])`, triggering `onSelectRoute(item.path)` and closing the palette.
4. **`Escape`**: Triggers `onClose()` and resets state.
5. **Mouse Hover**: Triggers `onMouseEnter={() => setSelectedIndex(currentIndex)}` so mouse and keyboard selection remain perfectly synchronized.
6. **Input Focus**: Focuses search input `inputRef.current?.focus()` on palette open.

---

## 4. Vitest Test Suite Blueprint (`CommandPalette.test.tsx`)

Location: `frontend/internal/src/features/search/__tests__/CommandPalette.test.tsx`

### 4.1 Test Scenarios & Expectations

```tsx
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { AppLayout } from '../../../components/AppLayout';
import { auth } from '../../../lib/auth';
import { searchApi } from '../searchApi';

vi.mock('../searchApi', () => ({
  searchApi: {
    search: vi.fn(),
  },
}));

describe('CommandPalette Feature & Keyboard Navigation Test Suite', () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.clearAllMocks();
  });

  it('1. opens command palette when pressing Ctrl+K and Cmd+K', () => {
    auth.set({
      accessToken: 'token-super',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super Admin',
      userId: 'usr-1',
      isSuperAdmin: true,
      permissions: [],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // Trigger Ctrl+K
    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    expect(screen.getByRole('dialog', { name: /command palette/i })).toBeInTheDocument();

    // Close with Escape
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('2. executes debounced search query and renders categorized sections', async () => {
    auth.set({
      accessToken: 'token-super',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super Admin',
      userId: 'usr-1',
      isSuperAdmin: true,
      permissions: [],
    });

    (searchApi.search as any).mockResolvedValue({
      query: 'developer',
      normalizedQuery: 'developer',
      category: 'All',
      totalMatches: 3,
      categoryCounts: { all: 3, candidates: 1, postings: 1, requisitions: 1 },
      items: [
        {
          id: 'cand-1',
          category: 'Candidates',
          title: 'Kyaw Kyaw',
          subtitle: 'kyaw@example.com',
          descriptionSnippet: 'Senior Developer',
          targetUrl: '/candidates/cand-1',
          relevanceScore: 90,
          createdAt: '2026-08-01T00:00:00Z',
        },
        {
          id: 'req-1',
          category: 'Requisitions',
          title: 'Lead Software Engineer',
          subtitle: 'Engineering',
          descriptionSnippet: 'Full-time position',
          targetUrl: '/requisitions/req-1',
          relevanceScore: 85,
          createdAt: '2026-08-01T00:00:00Z',
        },
        {
          id: 'jp-1',
          category: 'Postings',
          title: 'React Developer',
          subtitle: 'Yangon',
          descriptionSnippet: 'Frontend role',
          targetUrl: '/jobpostings/jp-1',
          relevanceScore: 80,
          createdAt: '2026-08-01T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    const input = screen.getByPlaceholderText(/type a command or search/i);
    fireEvent.change(input, { target: { value: 'developer' } });

    await waitFor(() => {
      expect(searchApi.search).toHaveBeenCalledWith(
        expect.objectContaining({ q: 'developer' })
      );
    });

    expect(screen.getByText('Kyaw Kyaw')).toBeInTheDocument();
    expect(screen.getByText('Lead Software Engineer')).toBeInTheDocument();
    expect(screen.getByText('React Developer')).toBeInTheDocument();
  });

  it('3. handles full keyboard navigation with ArrowDown, ArrowUp, and Enter', async () => {
    auth.set({
      accessToken: 'token-super',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super Admin',
      userId: 'usr-1',
      isSuperAdmin: true,
      permissions: [],
    });

    render(
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<AppLayout />}>
            <Route path="requisitions" element={<div data-testid="req-page">Requisitions Page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    const dialog = screen.getByRole('dialog', { name: /command palette/i });

    // Move selection using ArrowDown and press Enter
    fireEvent.keyDown(window, { key: 'ArrowDown' });
    fireEvent.keyDown(window, { key: 'Enter' });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('4. filters static command items based on user permissions', () => {
    auth.set({
      accessToken: 'token-limited',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'HiringManager',
      displayName: 'HM User',
      userId: 'usr-2',
      isSuperAdmin: false,
      permissions: ['permission:requisitions:requisitions:read'],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    const dialog = screen.getByRole('dialog', { name: /command palette/i });

    expect(within(dialog).getByText('Requisitions')).toBeInTheDocument();
    expect(within(dialog).queryByText('Users')).not.toBeInTheDocument();
  });

  it('5. resets search input and closes on Escape key', () => {
    auth.set({
      accessToken: 'token-super',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super Admin',
      userId: 'usr-1',
      isSuperAdmin: true,
      permissions: [],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
```

---

## 5. Verification & Quality Assurance Strategy

1. **TypeScript Type Safety**:
   - `npm run typecheck` must pass with 0 errors across `@recruitops/types`, `@recruitops/ui`, `@recruitops/internal`, and `@recruitops/public`.

2. **Vitest Test Suite Coverage**:
   - All 274 existing Vitest tests must pass without regressions.
   - The new tests in `CommandPalette.test.tsx` will add at least 5 new tests, bringing the total frontend test suite count to 279+ passing tests.

3. **Backend Alignment**:
   - `searchApi.ts` endpoints map cleanly to `SearchController.cs` (`GET /api/search?q={query}&category={category}`).
