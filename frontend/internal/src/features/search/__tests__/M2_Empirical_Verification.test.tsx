import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CommandPalette, CommandItem } from '@recruitops/ui';

describe('Empirical Verification: Milestone 2 Command Palette Integration & Routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('1. Route Navigation & Indexing Mismatch Verification', () => {
    it('1.1 navigates to target path when search result is selected via click', () => {
      const onSelectRoute = vi.fn();
      const onClose = vi.fn();

      const searchResults: CommandItem[] = [
        { id: 'cand-1', title: 'Aung San', category: 'Candidates', path: '/candidates/cand-1' },
      ];

      render(
        <CommandPalette
          isOpen={true}
          onClose={onClose}
          onSelectRoute={onSelectRoute}
          items={[]}
          searchResults={searchResults}
          query="Aung"
        />
      );

      const resultItem = screen.getByText('Aung San');
      fireEvent.click(resultItem);

      expect(onSelectRoute).toHaveBeenCalledWith('/candidates/cand-1');
      expect(onClose).toHaveBeenCalled();
    });

    it('1.2 EMPIRICAL BUG TEST: verifies whether keyboard selection matches visual category ordering', () => {
      const onSelectRoute = vi.fn();
      const onClose = vi.fn();

      // items provided in order: Navigation first, then Quick Actions
      const items: CommandItem[] = [
        { id: 'nav-1', title: 'Nav Requisitions', category: 'Navigation', path: '/requisitions' },
        { id: 'act-1', title: 'Action Create Req', category: 'Quick Actions', path: '/requisitions/new' },
      ];

      render(
        <CommandPalette
          isOpen={true}
          onClose={onClose}
          onSelectRoute={onSelectRoute}
          items={items}
          query=""
        />
      );

      // CATEGORY_ORDER puts 'Quick Actions' BEFORE 'Navigation'.
      // So visually on screen, 'Action Create Req' is displayed at the top (1st rendered item).
      // 'Nav Requisitions' is displayed below it (2nd rendered item).
      //
      // If selectedIndex starts at 0, pressing Enter SHOULD select the 1st visually displayed item ('/requisitions/new').
      // Let's test what actually happens!

      fireEvent.keyDown(window, { key: 'Enter' });

      // If the bug exists in CommandPalette.tsx, allCombinedItems[0] is '/requisitions',
      // but the top rendered item is '/requisitions/new'.
      const selectedPath = onSelectRoute.mock.calls[0]?.[0];
      console.log('Empirical Keyboard Selection Path at index 0:', selectedPath);

      // Desired behavior: index 0 selects top rendered item ('/requisitions/new')
      // If this fails, it empirically proves the indexing bug!
      expect(selectedPath).toBe('/requisitions/new');
    });
  });

  describe('2. Empty State, Loading & Error Fallback Indicators', () => {
    it('2.1 shows loading spinner and searching text when isLoading is true with empty results', () => {
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          items={[]}
          searchResults={[]}
          query="software"
          isLoading={true}
        />
      );

      expect(screen.getByText('Searching RecruitOps CRM...')).toBeInTheDocument();
    });

    it('2.2 displays empty state message when search query has no matches', () => {
      render(
        <CommandPalette
          isOpen={true}
          onClose={() => {}}
          items={[]}
          searchResults={[]}
          query="nonexistentquery123"
          isLoading={false}
        />
      );

      expect(
        screen.getByText('No matching commands or routes found for "nonexistentquery123"')
      ).toBeInTheDocument();
    });
  });
});
