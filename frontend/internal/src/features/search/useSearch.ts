import { useState, useEffect, useCallback, useRef } from 'react';
import type { SearchCategory, SearchResponse, SearchResultItem, CategoryCounts } from '@recruitops/types';
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
  debouncedQuery: string;
  category: SearchCategory;
  setCategory: (cat: SearchCategory) => void;
  page: number;
  setPage: (p: number) => void;
  pageSize: number;
  setPageSize: (s: number) => void;
  data: SearchResponse | null;
  results: SearchResponse | null;
  items: SearchResultItem[];
  totalMatches: number;
  categoryCounts: CategoryCounts;
  isLoading: boolean;
  isDebouncing: boolean;
  error: string | null;
  refetch: () => Promise<void>;
  clear: () => void;
}

const DEFAULT_CATEGORY_COUNTS: CategoryCounts = {
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
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery.trim());
  const [category, setCategoryState] = useState<SearchCategory>(initialCategory);
  const [page, setPageState] = useState(initialPage);
  const [pageSize, setPageSizeState] = useState(initialPageSize);

  const [data, setData] = useState<SearchResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isDebouncing, setIsDebouncing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);

  // Handle setting query with instant clearing on empty input
  const setQuery = useCallback((newQuery: string) => {
    setQueryState(newQuery);
    if (!newQuery.trim()) {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      setDebouncedQuery('');
      setIsDebouncing(false);
      setData(null);
      setError(null);
      setIsLoading(false);
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
      setDebouncedQuery(query.trim());
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
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
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
    debouncedQuery,
    category,
    setCategory,
    page,
    setPage,
    pageSize,
    setPageSize,
    data,
    results: data,
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
