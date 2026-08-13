import React, { useState, useEffect, useRef } from 'react';

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
  error?: string | null;
  placeholder?: string;
}

const DEFAULT_ITEMS: CommandItem[] = [
  {
    id: 'nav-requisitions',
    title: 'Requisitions',
    description: 'View and manage active job requisitions',
    category: 'Navigation',
    path: '/requisitions',
    shortcut: 'G R',
  },
  {
    id: 'nav-pipeline',
    title: 'Candidate Pipeline',
    description: 'Track candidate stages and 360 profiles',
    category: 'Navigation',
    path: '/pipeline',
    shortcut: 'G P',
  },
  {
    id: 'nav-job-postings',
    title: 'Job Postings',
    description: 'Manage external and internal job boards',
    category: 'Navigation',
    path: '/jobpostings',
    shortcut: 'G J',
  },
  {
    id: 'nav-departments',
    title: 'Departments & Roles',
    description: 'Organization structure and permission settings',
    category: 'Navigation',
    path: '/departments',
    shortcut: 'G D',
  },
  {
    id: 'action-new-requisition',
    title: 'Create New Requisition',
    description: 'Start a new hiring request workflow',
    category: 'Quick Actions',
    path: '/requisitions/new',
    shortcut: 'N R',
  },
  {
    id: 'action-new-job',
    title: 'Create New Job Posting',
    description: 'Publish a new open role to public portal',
    category: 'Quick Actions',
    path: '/jobpostings/new',
    shortcut: 'N J',
  },
];

const CATEGORY_ORDER = ['Quick Actions', 'Navigation', 'Candidates', 'Requisitions', 'Job Postings'];

export function CommandPalette({
  isOpen,
  onClose,
  onSelectRoute,
  items = DEFAULT_ITEMS,
  searchResults = [],
  query: propQuery,
  onQueryChange,
  isLoading = false,
  error,
  placeholder = 'Type a command or search...',
}: CommandPaletteProps) {
  const [localQuery, setLocalQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  const currentQuery = propQuery !== undefined ? propQuery : localQuery;

  const handleQueryChange = (val: string) => {
    setLocalQuery(val);
    if (onQueryChange) {
      onQueryChange(val);
    }
  };

  // Filter static items by query
  const q = currentQuery.toLowerCase().trim();
  const filteredStaticItems = items.filter((item) => {
    if (!q) return true;
    return (
      item.title.toLowerCase().includes(q) ||
      (item.description && item.description.toLowerCase().includes(q)) ||
      (item.category && item.category.toLowerCase().includes(q))
    );
  });

  // Combine static filtered items and dynamic search results (deduplicating by ID)
  const combinedMap = new Map<string, CommandItem>();
  filteredStaticItems.forEach((item) => combinedMap.set(item.id, item));
  searchResults.forEach((item) => combinedMap.set(item.id, item));

  const allCombinedItems = Array.from(combinedMap.values()).sort((a, b) => {
    const catA = CATEGORY_ORDER.indexOf(a.category ?? 'Quick Actions');
    const catB = CATEGORY_ORDER.indexOf(b.category ?? 'Quick Actions');
    const orderA = catA === -1 ? 999 : catA;
    const orderB = catB === -1 ? 999 : catB;
    return orderA - orderB;
  });

  // Reset index when query changes
  useEffect(() => {
    setSelectedIndex(0);
  }, [currentQuery]);

  // Focus input and reset local query when opened
  useEffect(() => {
    if (isOpen) {
      setLocalQuery('');
      if (onQueryChange) {
        onQueryChange('');
      }
      setSelectedIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [isOpen]);

  // Keyboard navigation inside modal
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!isOpen) return;

      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
      } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        setSelectedIndex((prev) =>
          allCombinedItems.length > 0 ? (prev + 1) % allCombinedItems.length : 0
        );
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        setSelectedIndex((prev) =>
          allCombinedItems.length > 0
            ? (prev - 1 + allCombinedItems.length) % allCombinedItems.length
            : 0
        );
      } else if (e.key === 'Enter') {
        e.preventDefault();
        if (allCombinedItems[selectedIndex]) {
          handleExecuteItem(allCombinedItems[selectedIndex]);
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, allCombinedItems, selectedIndex, onClose]);

  const handleExecuteItem = (item: CommandItem) => {
    if (item.onSelect) {
      item.onSelect();
    } else if (item.path && onSelectRoute) {
      onSelectRoute(item.path);
    }
    onClose();
  };

  if (!isOpen) return null;

  // Group items by category in preferred order
  const categorySet = new Set(allCombinedItems.map((item) => item.category || 'General'));
  const categories: string[] = [];
  CATEGORY_ORDER.forEach((cat) => {
    if (categorySet.has(cat)) {
      categories.push(cat);
      categorySet.delete(cat);
    }
  });
  categorySet.forEach((cat) => categories.push(cat));

  let globalIndexCounter = 0;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center pt-16 sm:pt-20 px-4">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-ink-900/50 backdrop-blur-xs transition-opacity"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Palette Box */}
      <div
        className="relative w-full max-w-2xl bg-surface-0 rounded-md shadow-pop border border-line-200 overflow-hidden z-10 flex flex-col max-h-[80vh]"
        role="dialog"
        aria-modal="true"
        aria-label="Command Palette"
      >
        {/* Search Header */}
        <div className="flex items-center border-b border-line-200 px-4 py-3 bg-surface-0">
          <svg
            className="h-5 w-5 text-ink-400 mr-3 flex-shrink-0"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
            />
          </svg>
          <input
            ref={inputRef}
            type="text"
            className="w-full bg-transparent text-base text-ink-900 placeholder-ink-400 focus:outline-none"
            placeholder={placeholder}
            value={currentQuery}
            onChange={(e) => handleQueryChange(e.target.value)}
          />
          {isLoading && (
            <svg
              className="animate-spin h-4 w-4 text-primary-600 ml-2 flex-shrink-0"
              viewBox="0 0 24 24"
              fill="none"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
              />
            </svg>
          )}
          {currentQuery && (
            <button
              type="button"
              onClick={() => handleQueryChange('')}
              className="text-xs text-ink-400 hover:text-ink-600 px-2 py-1"
            >
              Clear
            </button>
          )}
          <span className="text-[11px] font-mono font-medium text-ink-400 bg-surface-50 border border-line-200 px-1.5 py-0.5 rounded ml-2">
            ESC
          </span>
        </div>

        {/* Error Banner */}
        {error && (
          <div className="px-4 py-2 bg-amber-50 border-b border-amber-200 text-xs text-amber-800 flex items-center gap-2">
            <svg
              className="h-4 w-4 text-amber-600 flex-shrink-0"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            <span>Failed to search backend. Displaying navigation shortcuts.</span>
          </div>
        )}

        {/* Results List */}
        <div className="overflow-y-auto p-2 flex-1">
          {allCombinedItems.length === 0 ? (
            <div className="p-8 text-center text-sm text-ink-400">
              {isLoading
                ? 'Searching RecruitOps CRM...'
                : `No matching commands or routes found for "${currentQuery}"`}
            </div>
          ) : (
            categories.map((category) => {
              const categoryItems = allCombinedItems.filter(
                (item) => (item.category || 'General') === category
              );
              if (categoryItems.length === 0) return null;

              return (
                <div key={category} className="mb-3 last:mb-0">
                  <div className="px-3 py-1.5 text-[11px] font-semibold uppercase tracking-wider text-ink-400">
                    {category}
                  </div>
                  <div className="space-y-0.5">
                    {categoryItems.map((item) => {
                      const currentIndex = globalIndexCounter++;
                      const isSelected = currentIndex === selectedIndex;

                      return (
                        <div
                          key={item.id}
                          onClick={() => handleExecuteItem(item)}
                          onMouseEnter={() => setSelectedIndex(currentIndex)}
                          className={`flex items-center justify-between px-3 py-2.5 rounded-md cursor-pointer text-sm transition-colors ${
                            isSelected
                              ? 'bg-primary-100/70 text-ink-900 font-medium'
                              : 'text-ink-600 hover:bg-surface-50'
                          }`}
                        >
                          <div className="flex items-center gap-3 min-w-0">
                            {item.icon ? (
                              <span className="text-ink-600">{item.icon}</span>
                            ) : (
                              <svg
                                className="h-4 w-4 text-ink-400 flex-shrink-0"
                                fill="none"
                                viewBox="0 0 24 24"
                                stroke="currentColor"
                                strokeWidth="2"
                              >
                                <path
                                  strokeLinecap="round"
                                  strokeLinejoin="round"
                                  d="M13 7l5 5m0 0l-5 5m5-5H6"
                                />
                              </svg>
                            )}
                            <div className="truncate">
                              <span className="text-ink-900 font-medium">
                                {item.title}
                              </span>
                              {item.description && (
                                <span className="ml-2 text-xs text-ink-400 truncate">
                                  {item.description}
                                </span>
                              )}
                            </div>
                          </div>
                          {item.shortcut && (
                            <kbd className="text-[11px] font-mono text-ink-400 bg-surface-0 border border-line-200 px-1.5 py-0.5 rounded shadow-xs ml-3 flex-shrink-0">
                              {item.shortcut}
                            </kbd>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })
          )}
        </div>

        {/* Footer */}
        <div className="border-t border-line-200 px-4 py-2 bg-surface-50 text-xs text-ink-400 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <span>
              <kbd className="font-mono bg-surface-0 border border-line-200 px-1 rounded">↑↓</kbd> Navigate
            </span>
            <span>
              <kbd className="font-mono bg-surface-0 border border-line-200 px-1 rounded">↵</kbd> Select
            </span>
          </div>
          <span>RecruitOps CRM</span>
        </div>
      </div>
    </div>
  );
}
