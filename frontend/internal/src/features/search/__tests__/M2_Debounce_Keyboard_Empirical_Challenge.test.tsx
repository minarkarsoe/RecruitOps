import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, renderHook, act } from '@testing-library/react';
import { useSearch } from '../useSearch';
import { searchApi } from '../searchApi';
import { CommandPalette, CommandItem } from '@recruitops/ui';

vi.mock('../searchApi', () => ({
  searchApi: {
    search: vi.fn(),
  },
}));

describe('Empirical Challenge: Milestone 2 Debounce, AbortController & Keyboard Navigation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useRealTimers();
  });

  describe('1. 300ms Debouncing & AbortController Cancellation', () => {
    it('1.1. holds API execution during typing and only fires 300ms after final keystroke', async () => {
      vi.useFakeTimers();

      (searchApi.search as any).mockResolvedValue({
        query: 'engineer',
        normalizedQuery: 'engineer',
        category: 'All',
        totalMatches: 1,
        categoryCounts: { all: 1, candidates: 1, postings: 0, requisitions: 0 },
        items: [],
        page: 1,
        pageSize: 20,
        totalPages: 1,
      });

      const { result } = renderHook(() => useSearch({ debounceMs: 300 }));

      // Type "e" at t=0ms
      act(() => {
        result.current.setQuery('e');
      });
      expect(result.current.isDebouncing).toBe(true);
      expect(searchApi.search).not.toHaveBeenCalled();

      // Advance 100ms (t=100ms)
      act(() => {
        vi.advanceTimersByTime(100);
      });
      expect(searchApi.search).not.toHaveBeenCalled();

      // Type "eng" at t=100ms
      act(() => {
        result.current.setQuery('eng');
      });
      expect(searchApi.search).not.toHaveBeenCalled();

      // Advance 200ms (t=300ms total, 200ms after last keystroke)
      act(() => {
        vi.advanceTimersByTime(200);
      });
      expect(searchApi.search).not.toHaveBeenCalled();

      // Advance remaining 100ms (t=400ms total, 300ms after last keystroke)
      act(() => {
        vi.advanceTimersByTime(100);
      });

      // API should be called now with final query "eng"
      expect(searchApi.search).toHaveBeenCalledTimes(1);
      expect(searchApi.search).toHaveBeenCalledWith(
        expect.objectContaining({ q: 'eng' }),
        expect.any(Object)
      );

      vi.useRealTimers();
    });

    it('1.2. aborts in-flight network request via AbortController when a new search request is initiated', async () => {
      let firstSignal: AbortSignal | null = null;
      let secondSignal: AbortSignal | null = null;

      (searchApi.search as any).mockImplementation((_params: any, signal: AbortSignal) => {
        if (!firstSignal) {
          firstSignal = signal;
        } else {
          secondSignal = signal;
        }
        return new Promise((resolve) => setTimeout(() => resolve({ items: [] }), 200));
      });

      const { result } = renderHook(() => useSearch({ debounceMs: 50 }));

      // First query trigger
      act(() => {
        result.current.setQuery('react');
      });

      await waitFor(() => expect(searchApi.search).toHaveBeenCalledTimes(1));
      expect(firstSignal).not.toBeNull();
      expect(firstSignal!.aborted).toBe(false);

      // Trigger second query before first promise resolves
      act(() => {
        result.current.setQuery('react dotnet');
      });

      await waitFor(() => expect(searchApi.search).toHaveBeenCalledTimes(2));

      // First signal MUST be aborted
      expect(firstSignal!.aborted).toBe(true);
      expect(secondSignal!.aborted).toBe(false);
    });

    it('1.3. immediately aborts in-flight request and clears state when query is set to empty string', async () => {
      let activeSignal: AbortSignal | null = null;

      (searchApi.search as any).mockImplementation((_params: any, signal: AbortSignal) => {
        activeSignal = signal;
        return new Promise(() => {}); // Never resolves
      });

      const { result } = renderHook(() => useSearch({ debounceMs: 50 }));

      act(() => {
        result.current.setQuery('backend');
      });

      await waitFor(() => expect(searchApi.search).toHaveBeenCalledTimes(1));
      expect(activeSignal!.aborted).toBe(false);

      // Clear query immediately
      act(() => {
        result.current.setQuery('');
      });

      expect(activeSignal!.aborted).toBe(true);
      expect(result.current.query).toBe('');
      expect(result.current.debouncedQuery).toBe('');
      expect(result.current.isDebouncing).toBe(false);
      expect(result.current.items).toEqual([]);
    });

    it('1.4. aborts active AbortController when hook unmounts', async () => {
      let activeSignal: AbortSignal | null = null;

      (searchApi.search as any).mockImplementation((_params: any, signal: AbortSignal) => {
        activeSignal = signal;
        return new Promise(() => {});
      });

      const { result, unmount } = renderHook(() => useSearch({ debounceMs: 50 }));

      act(() => {
        result.current.setQuery('unmount-test');
      });

      await waitFor(() => expect(searchApi.search).toHaveBeenCalledTimes(1));
      expect(activeSignal!.aborted).toBe(false);

      unmount();

      expect(activeSignal!.aborted).toBe(true);
    });
  });

  describe('2. Rapid Keyboard Navigation & Edge Case Selection Indexing', () => {
    // Note: Items provided in array order: Item 1 (Navigation), Item 2 (Navigation), Item 3 (Quick Actions)
    const mockItems: CommandItem[] = [
      { id: 'item-1', title: 'Item 1', category: 'Navigation', path: '/path-1' },
      { id: 'item-2', title: 'Item 2', category: 'Navigation', path: '/path-2' },
      { id: 'item-3', title: 'Item 3', category: 'Quick Actions', path: '/path-3' },
    ];

    it('2.1. verifies category display order aligns 1:1 with keyboard execution indexing', () => {
      const onSelectRoute = vi.fn();
      const onClose = vi.fn();

      const { container } = render(
        <CommandPalette
          isOpen={true}
          onClose={onClose}
          onSelectRoute={onSelectRoute}
          items={mockItems}
        />
      );

      // Category display order in UI: 1. Quick Actions (Item 3), 2. Navigation (Item 1, Item 2)
      // Visual rendering assigns currentIndex = 0 to Item 3.
      // So when selectedIndex = 0, the DOM highlights Item 3 (Quick Actions).
      // Selected-row tint in CommandPalette. `brand` since ADR-0025 (was `primary`).
      const highlightedEl = container.querySelector('.bg-brand-100\\/70');
      expect(highlightedEl).not.toBeNull();
      expect(highlightedEl?.textContent).toContain('Item 3');

      // Pressing Enter executes allCombinedItems[0], which MUST be Item 3 (Quick Actions)!
      fireEvent.keyDown(window, { key: 'Enter' });

      // Verification: visually highlighted item (Item 3) is executed!
      expect(onSelectRoute).toHaveBeenCalledWith('/path-3');
      expect(onSelectRoute).not.toHaveBeenCalledWith('/path-1');
    });

    it('2.2. safe when pressing keyboard arrows and Enter on empty results list (0 items)', () => {
      const onSelectRoute = vi.fn();

      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          onSelectRoute={onSelectRoute}
          items={[]}
          searchResults={[]}
          query="nonexistent"
        />
      );

      expect(() => {
        fireEvent.keyDown(window, { key: 'ArrowDown' });
        fireEvent.keyDown(window, { key: 'ArrowUp' });
        fireEvent.keyDown(window, { key: 'Enter' });
      }).not.toThrow();

      expect(onSelectRoute).not.toHaveBeenCalled();
    });

    it('2.3. handles dynamic searchResults array changes without throwing', () => {
      const onSelectRoute = vi.fn();

      const { rerender } = render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          onSelectRoute={onSelectRoute}
          items={mockItems}
          searchResults={[
            { id: 'sr-1', title: 'Dynamic Result 1', category: 'Candidates', path: '/cand-1' },
          ]}
        />
      );

      // Move selection down
      fireEvent.keyDown(window, { key: 'ArrowDown' });

      // Dynamic search results disappear or update
      rerender(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          onSelectRoute={onSelectRoute}
          items={[]}
          searchResults={[]}
        />
      );

      // Pressing ArrowDown / Enter on newly empty list does not throw
      expect(() => {
        fireEvent.keyDown(window, { key: 'ArrowDown' });
        fireEvent.keyDown(window, { key: 'Enter' });
      }).not.toThrow();
    });

    it('2.4. closes palette on Escape key and triggers onClose', () => {
      const onClose = vi.fn();

      render(
        <CommandPalette
          isOpen={true}
          onClose={onClose}
          items={mockItems}
        />
      );

      fireEvent.keyDown(window, { key: 'Escape' });
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('2.5. displays error banner when search error occurs', () => {
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          error="Failed to connect to search API"
        />
      );

      expect(
        screen.getByText(/Failed to search backend. Displaying navigation shortcuts./i)
      ).toBeInTheDocument();
    });
  });
});
