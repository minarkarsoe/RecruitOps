import { describe, expect, it, beforeEach, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useSearch } from '../useSearch';
import { searchApi } from '../searchApi';

vi.mock('../searchApi', () => ({
  searchApi: {
    search: vi.fn(),
  },
}));

describe('useSearch Hook Unit Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('1. debounces search input for 300ms before executing API search', async () => {
    (searchApi.search as any).mockResolvedValue({
      query: 'developer',
      normalizedQuery: 'developer',
      category: 'All',
      totalMatches: 1,
      categoryCounts: { all: 1, candidates: 1, postings: 0, requisitions: 0 },
      items: [],
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    const { result } = renderHook(() => useSearch({ debounceMs: 300 }));

    act(() => {
      result.current.setQuery('developer');
    });

    expect(result.current.query).toBe('developer');
    expect(result.current.isDebouncing).toBe(true);
    expect(searchApi.search).not.toHaveBeenCalled();

    await waitFor(
      () => {
        expect(searchApi.search).toHaveBeenCalledWith(
          expect.objectContaining({ q: 'developer' }),
          expect.any(Object)
        );
      },
      { timeout: 1000 }
    );

    expect(result.current.isDebouncing).toBe(false);
  });

  it('2. clears query instantly when setting query to empty string without waiting 300ms', async () => {
    const { result } = renderHook(() => useSearch());

    act(() => {
      result.current.setQuery('react');
    });
    expect(result.current.isDebouncing).toBe(true);

    act(() => {
      result.current.setQuery('');
    });

    expect(result.current.query).toBe('');
    expect(result.current.debouncedQuery).toBe('');
    expect(result.current.isDebouncing).toBe(false);
    expect(result.current.items).toEqual([]);
  });

  it('3. resets page to 1 when changing search category', async () => {
    (searchApi.search as any).mockResolvedValue({
      query: 'test',
      normalizedQuery: 'test',
      category: 'Candidates',
      totalMatches: 0,
      categoryCounts: { all: 0, candidates: 0, postings: 0, requisitions: 0 },
      items: [],
      page: 1,
      pageSize: 20,
      totalPages: 0,
    });

    const { result } = renderHook(() => useSearch({ initialQuery: 'test', initialPage: 3 }));

    act(() => {
      result.current.setCategory('Candidates');
    });

    expect(result.current.category).toBe('Candidates');
    expect(result.current.page).toBe(1);
  });
});
