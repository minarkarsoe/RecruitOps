# Blueprint Report: Milestone 2 Frontend Search DTOs, API Client, and `useSearch` Hook

## Executive Summary
This document provides a complete technical analysis and design blueprint for Milestone 2 of the RecruitOps Full-text Search & Command Palette flow. It details:
1. Exact TypeScript contract definitions for `@recruitops/types`.
2. The `searchApi` client module in `@recruitops/internal`.
3. The custom `useSearch` React hook in `@recruitops/internal` with 300ms debouncing, request cancellation (`AbortController`), loading/error state management, and category tab switching.

---

## 1. TypeScript Types for `packages/types/src/index.ts`

### 1.1 Backend Contract Alignment
The backend contract is defined in `backend/src/Application/DTOs/Search/SearchDtos.cs` and served via `GET /api/search` in `backend/src/Api/Controllers/SearchController.cs`. ASP.NET Core serializes response JSON using camelCase property naming by default.

| Backend C# DTO / Property | Frontend TypeScript Target | Type | Notes |
|---|---|---|---|
| `SearchCategory` | `SearchCategory` | `'All' \| 'Candidates' \| 'Postings' \| 'Requisitions'` | Category filter union |
| `SearchResultItemDto.Id` | `SearchResultItem.id` | `string` | GUID string |
| `SearchResultItemDto.Category` | `SearchResultItem.category` | `SearchCategory \| string` | Matches item type |
| `SearchResultItemDto.Title` | `SearchResultItem.title` | `string` | Primary headline |
| `SearchResultItemDto.Subtitle` | `SearchResultItem.subtitle` | `string` | Email, phone, location |
| `SearchResultItemDto.DescriptionSnippet` | `SearchResultItem.descriptionSnippet` | `string \| null` | Includes `<mark>` tags |
| `SearchResultItemDto.TargetUrl` | `SearchResultItem.targetUrl` | `string` | SPA detail route |
| `SearchResultItemDto.DepartmentId` | `SearchResultItem.departmentId` | `string \| null` | Scoped department |
| `SearchResultItemDto.DepartmentName` | `SearchResultItem.departmentName` | `string \| null` | Department name |
| `SearchResultItemDto.RelevanceScore` | `SearchResultItem.relevanceScore` | `number` | 0.0 to 100.0 |
| `SearchResultItemDto.CreatedAt` | `SearchResultItem.createdAt` | `string` | ISO 8601 string |
| `CategoryCountsDto.All` | `CategoryCounts.all` | `number` | Category match count |
| `CategoryCountsDto.Candidates` | `CategoryCounts.candidates` | `number` | Candidate match count |
| `CategoryCountsDto.Postings` | `CategoryCounts.postings` | `number` | Posting match count |
| `CategoryCountsDto.Requisitions` | `CategoryCounts.requisitions` | `number` | Requisition match count |

### 1.2 Exact TypeScript Code to Append to `packages/types/src/index.ts`

```typescript
// ── Module 4: Full-text Search & Command Palette DTOs (Milestones 1, 2, 3) ──

export type SearchCategory = 'All' | 'Candidates' | 'Postings' | 'Requisitions';

export interface SearchQueryParameters {
  q: string;
  category?: SearchCategory;
  page?: number;
  pageSize?: number;
}

export interface CategoryCounts {
  all: number;
  candidates: number;
  postings: number;
  requisitions: number;
}

export interface SearchResultItem {
  id: string;
  category: SearchCategory | string;
  title: string;
  subtitle: string;
  descriptionSnippet: string | null;
  targetUrl: string;
  departmentId: string | null;
  departmentName: string | null;
  relevanceScore: number;
  createdAt: string;
}

export interface SearchResponse {
  query: string;
  normalizedQuery: string;
  category: SearchCategory;
  totalMatches: number;
  categoryCounts: CategoryCounts;
  items: SearchResultItem[];
  page: number;
  pageSize: number;
  totalPages: number;
}
```

---

## 2. API Client (`frontend/internal/src/features/search/searchApi.ts`)

### 2.1 Design & Capabilities
- Uses `apiFetch` from `../../lib/api.ts`, which automatically injects:
  - `Authorization: Bearer <accessToken>` header from `sessionStorage` session via `auth.get()`.
  - `X-Tenant-Id: <tenantId>` header if active tenant is selected.
  - Silent token refresh retry flow on 401 Unauthorized responses.
  - JSON error parsing into `ApiError(status, message)`.
- Accepts an optional `AbortSignal` for cancelling pending HTTP requests when user types a new character.
- Short-circuits empty/whitespace queries (`!params.q.trim()`) to return a default empty `SearchResponse` without triggering an HTTP call (preventing unnecessary 400 Bad Request responses from backend).

### 2.2 Exact Blueprint for `searchApi.ts`

```typescript
import { apiFetch } from '../../lib/api';
import type { SearchResponse, SearchQueryParameters } from '@recruitops/types';

export const searchApi = {
  /**
   * Executes a full-text search query against GET /api/search.
   *
   * @param params Query string, category filter, and pagination options.
   * @param signal Optional AbortSignal to cancel pending fetch requests.
   */
  search: async (
    params: SearchQueryParameters,
    signal?: AbortSignal
  ): Promise<SearchResponse> => {
    const trimmed = params.q?.trim() ?? '';
    
    // Short-circuit empty queries to prevent empty 400 requests to backend
    if (!trimmed) {
      return {
        query: '',
        normalizedQuery: '',
        category: params.category ?? 'All',
        totalMatches: 0,
        categoryCounts: { all: 0, candidates: 0, postings: 0, requisitions: 0 },
        items: [],
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
        totalPages: 0,
      };
    }

    const queryParams = new URLSearchParams();
    queryParams.append('q', trimmed);
    if (params.category && params.category !== 'All') {
      queryParams.append('category', params.category);
    }
    if (params.page !== undefined) {
      queryParams.append('page', params.page.toString());
    }
    if (params.pageSize !== undefined) {
      queryParams.append('pageSize', params.pageSize.toString());
    }

    return apiFetch<SearchResponse>(`/search?${queryParams.toString()}`, { signal });
  },
};
```

---

## 3. Custom React Hook (`frontend/internal/src/features/search/useSearch.ts`)

### 3.1 Requirements & Behavior
1. **300ms Debouncing**:
   - User typing in input updates local `query` instantly for responsive UI rendering.
   - A `debouncedQuery` state is updated after a 300ms timer.
   - If `query` is emptied (`query.trim() === ''`), `debouncedQuery` and search results reset **immediately** without waiting 300ms.
2. **Loading & Debouncing Flags**:
   - `isDebouncing`: true while user is actively typing within the 300ms window.
   - `isLoading`: true while the API fetch promise is pending.
3. **Category & Pagination Switching**:
   - Changing `category` immediately resets `page` to `1` and re-runs search for the new category tab.
   - Changing `page` fetches the requested page.
4. **Race Condition & Cancellation Protection**:
   - Uses `AbortController` inside `useEffect`.
   - Aborts previous request when a new query or category change triggers before the previous fetch completes.
5. **Error Handling**:
   - Captures `ApiError` or network errors and sets `error` message string.
   - Clears `error` state when user types a new query or calls `clear()`.
   - Exposes `refetch()` for manual retry.

### 3.2 Exact Blueprint for `useSearch.ts`

```typescript
import { useState, useEffect, useCallback, useRef } from 'react';
import type { SearchCategory, SearchResponse, SearchResultItem } from '@recruitops/types';
import { searchApi } from './searchApi';

export interface UseSearchOptions {
  initialQuery?: string;
  initialCategory?: SearchCategory;
  initialPage?: number;
  initialPageSize?: number;
  debounceMs?: number;
  enabled?: boolean;
}

export interface UseSearchResult {
  query: string;
  setQuery: (q: string) => void;
  category: SearchCategory;
  setCategory: (cat: SearchCategory) => void;
  page: number;
  setPage: (p: number) => void;
  pageSize: number;
  setPageSize: (s: number) => void;
  data: SearchResponse | null;
  items: SearchResultItem[];
  totalMatches: number;
  categoryCounts: SearchResponse['categoryCounts'];
  isLoading: boolean;
  isDebouncing: boolean;
  error: string | null;
  refetch: () => Promise<void>;
  clear: () => void;
}

const DEFAULT_CATEGORY_COUNTS = {
  all: 0,
  candidates: 0,
  postings: 0,
  requisitions: 0,
};

export function useSearch(options: UseSearchOptions = {}): UseSearchResult {
  const {
    initialQuery = '',
    initialCategory = 'All',
    initialPage = 1,
    initialPageSize = 20,
    debounceMs = 300,
    enabled = true,
  } = options;

  const [query, setQueryState] = useState(initialQuery);
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery);
  const [category, setCategoryState] = useState<SearchCategory>(initialCategory);
  const [page, setPageState] = useState(initialPage);
  const [pageSize, setPageSizeState] = useState(initialPageSize);

  const [data, setData] = useState<SearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isDebouncing, setIsDebouncing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);

  // Handle setting query with immediate debounce bypass for empty string
  const setQuery = useCallback((newQuery: string) => {
    setQueryState(newQuery);
    if (!newQuery.trim()) {
      setDebouncedQuery('');
      setIsDebouncing(false);
    } else {
      setIsDebouncing(true);
    }
  }, []);

  // Handle setting category (resets page to 1)
  const setCategory = useCallback((newCategory: SearchCategory) => {
    setCategoryState(newCategory);
    setPageState(1);
  }, []);

  // Handle setting page
  const setPage = useCallback((newPage: number) => {
    setPageState(newPage);
  }, []);

  // Handle setting page size (resets page to 1)
  const setPageSize = useCallback((newPageSize: number) => {
    setPageSizeState(newPageSize);
    setPageState(1);
  }, []);

  // Debounce query input timer effect
  useEffect(() => {
    if (!query.trim()) return;

    const timer = setTimeout(() => {
      setDebouncedQuery(query);
      setIsDebouncing(false);
    }, debounceMs);

    return () => clearTimeout(timer);
  }, [query, debounceMs]);

  // Main search API execution effect
  const executeSearch = useCallback(async () => {
    if (!enabled || !debouncedQuery.trim()) {
      setData(null);
      setIsLoading(false);
      setError(null);
      return;
    }

    // Cancel any ongoing fetch request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    const controller = new AbortController();
    abortControllerRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const response = await searchApi.search(
        {
          q: debouncedQuery,
          category,
          page,
          pageSize,
        },
        controller.signal
      );

      if (!controller.signal.aborted) {
        setData(response);
        setIsLoading(false);
      }
    } catch (err: unknown) {
      if (err instanceof Error && err.name === 'AbortError') {
        return; // Ignore abort exceptions
      }
      if (!controller.signal.aborted) {
        setError(err instanceof Error ? err.message : 'An error occurred during search.');
        setIsLoading(false);
      }
    }
  }, [debouncedQuery, category, page, pageSize, enabled]);

  useEffect(() => {
    executeSearch();

    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, [executeSearch]);

  const clear = useCallback(() => {
    setQueryState('');
    setDebouncedQuery('');
    setData(null);
    setError(null);
    setIsLoading(false);
    setIsDebouncing(false);
    setPageState(1);
  }, []);

  return {
    query,
    setQuery,
    category,
    setCategory,
    page,
    setPage,
    pageSize,
    setPageSize,
    data,
    items: data?.items ?? [],
    totalMatches: data?.totalMatches ?? 0,
    categoryCounts: data?.categoryCounts ?? DEFAULT_CATEGORY_COUNTS,
    isLoading,
    isDebouncing,
    error,
    refetch: executeSearch,
    clear,
  };
}
```

---

## 4. Proposed Unit & Integration Test Plan

To maintain the project's quality standard (32 Vitest suites, 274 tests passing), the implementer should add test cases in `frontend/internal/src/features/search/__tests__/useSearch.test.ts`:
1. **Debounce verification**: Typing "dev" does not call `searchApi.search` until 300ms pass.
2. **Immediate empty clearing**: Clearing input to "" instantly resets state without 300ms wait.
3. **Category tab switch**: Switching category from 'All' to 'Candidates' resets page to 1 and triggers search with category parameter.
4. **Error handling**: Simulated API failure populates `error` state and stops `isLoading`.
5. **AbortController cancellation**: Rapid consecutive query changes abort prior requests.

---

## 5. Blueprint File Locations Summary

1. Types: `packages/types/src/index.ts`
2. API Client: `frontend/internal/src/features/search/searchApi.ts`
3. Custom Hook: `frontend/internal/src/features/search/useSearch.ts`
4. Hook Tests: `frontend/internal/src/features/search/__tests__/useSearch.test.ts`
