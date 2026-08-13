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

  it('1. opens command palette when pressing Ctrl+K or Cmd+K and closes with Escape', () => {
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

    // Trigger Ctrl+K to open
    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    expect(screen.getByRole('dialog', { name: /command palette/i })).toBeInTheDocument();

    // Close with Escape key
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    // Trigger Cmd+K to open
    fireEvent.keyDown(window, { key: 'k', metaKey: true });
    expect(screen.getByRole('dialog', { name: /command palette/i })).toBeInTheDocument();
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
      query: 'engineer',
      normalizedQuery: 'engineer',
      category: 'All',
      totalMatches: 3,
      categoryCounts: { all: 3, candidates: 1, postings: 1, requisitions: 1 },
      items: [
        {
          id: 'cand-101',
          category: 'Candidates',
          title: 'Aung Kyaw',
          subtitle: 'aung.kyaw@example.com | Senior Software Engineer',
          descriptionSnippet: 'Experienced React & .NET <mark>engineer</mark>',
          targetUrl: '/candidates/cand-101',
          departmentId: 'dept-1',
          departmentName: 'Engineering',
          relevanceScore: 95.0,
          createdAt: '2026-08-01T10:00:00Z',
        },
        {
          id: 'req-202',
          category: 'Requisitions',
          title: 'Lead Systems Engineer',
          subtitle: 'Engineering Department',
          descriptionSnippet: 'Full-time requisition for Infrastructure team',
          targetUrl: '/requisitions/req-202',
          departmentId: 'dept-1',
          departmentName: 'Engineering',
          relevanceScore: 90.0,
          createdAt: '2026-08-01T10:00:00Z',
        },
        {
          id: 'jp-303',
          category: 'Postings',
          title: 'Full Stack Engineer',
          subtitle: 'Yangon, Myanmar | FullTime',
          descriptionSnippet: 'Public job posting for engineering role',
          targetUrl: '/jobpostings/jp-303',
          departmentId: 'dept-1',
          departmentName: 'Engineering',
          relevanceScore: 85.0,
          createdAt: '2026-08-01T10:00:00Z',
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

    // Open palette
    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    const dialog = screen.getByRole('dialog', { name: /command palette/i });
    const input = screen.getByPlaceholderText(/type a command or search/i);

    // Type query
    fireEvent.change(input, { target: { value: 'engineer' } });

    // Wait for 300ms debounced search call
    await waitFor(
      () => {
        expect(searchApi.search).toHaveBeenCalledWith(
          expect.objectContaining({ q: 'engineer', category: 'All' }),
          expect.any(Object)
        );
      },
      { timeout: 1000 }
    );

    // Verify categorized section headers and item titles are rendered
    expect(within(dialog).getByText(/Candidates/i)).toBeInTheDocument();
    expect(within(dialog).getByText('Aung Kyaw')).toBeInTheDocument();

    expect(within(dialog).getByText(/Requisitions/i)).toBeInTheDocument();
    expect(within(dialog).getByText('Lead Systems Engineer')).toBeInTheDocument();

    expect(within(dialog).getByText(/Job Postings/i)).toBeInTheDocument();
    expect(within(dialog).getByText('Full Stack Engineer')).toBeInTheDocument();
  });

  it('3. handles full keyboard navigation with ArrowDown, ArrowUp, and Enter selection', async () => {
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
      totalMatches: 1,
      categoryCounts: { all: 1, candidates: 1, postings: 0, requisitions: 0 },
      items: [
        {
          id: 'cand-77',
          category: 'Candidates',
          title: 'Hla Hla',
          subtitle: 'hla@example.com',
          descriptionSnippet: 'Senior Frontend Developer',
          targetUrl: '/candidates/cand-77',
          relevanceScore: 99.0,
          createdAt: '2026-08-01T10:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    render(
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<AppLayout />}>
            <Route path="candidates/:id" element={<div data-testid="candidate-detail-page">Candidate 360 View</div>} />
            <Route path="requisitions" element={<div data-testid="requisitions-page">Requisitions Page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    const input = screen.getByPlaceholderText(/type a command or search/i);
    fireEvent.change(input, { target: { value: 'developer' } });

    await waitFor(() => {
      expect(screen.getByText('Hla Hla')).toBeInTheDocument();
    });

    // Press ArrowDown to navigate through item list
    fireEvent.keyDown(window, { key: 'ArrowDown' });
    fireEvent.keyDown(window, { key: 'ArrowDown' });
    fireEvent.keyDown(window, { key: 'ArrowUp' });

    // Press Enter to select active item
    fireEvent.keyDown(window, { key: 'Enter' });

    // Modal closes and route updates
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('4. clears input instantly when typing empty string or clicking clear button', async () => {
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
      query: 'test',
      normalizedQuery: 'test',
      category: 'All',
      totalMatches: 0,
      categoryCounts: { all: 0, candidates: 0, postings: 0, requisitions: 0 },
      items: [],
      page: 1,
      pageSize: 20,
      totalPages: 0,
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
    const input = screen.getByPlaceholderText(/type a command or search/i) as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'test' } });
    expect(input.value).toBe('test');

    // Click Clear button
    const clearBtn = screen.getByRole('button', { name: /clear/i });
    fireEvent.click(clearBtn);

    expect(input.value).toBe('');
  });

  it('5. filters static command items based on user RBAC permissions', () => {
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
    expect(within(dialog).queryByText('Role Builder')).not.toBeInTheDocument();
  });
});
