import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Outlet } from 'react-router-dom';
import type { ReactNode } from 'react';
import { AppRoutes } from './App';

/**
 * Every link `SearchService` sends must land on a real page.
 *
 * `SearchResultItemDto.TargetUrl` is a route in THIS router, written as a string in C#. Until
 * 2026-09-17 two of its three shapes — `/jobs/{id}` and `/candidates/{id}` — matched nothing
 * here, fell through to the `*` catch-all, and put the user on /requisitions without a word.
 * The palette's own test declared a `candidates/:id` route of its own, so it could not notice.
 *
 * The shapes below are the ones `SearchApiTests` asserts exactly on the backend. Change one side,
 * change the other.
 */

// Only the routing is under test, so every destination is a label and the shell is a pass-through.
vi.mock('./components/RequireAuth', () => ({ RequireAuth: ({ children }: { children: ReactNode }) => <>{children}</> }));
vi.mock('./components/AppLayout', () => ({ AppLayout: () => <Outlet /> }));
vi.mock('./pages/RequisitionsPage', () => ({ RequisitionsPage: () => <p>page:requisitions</p> }));
vi.mock('./pages/RequisitionDetailPage', () => ({ RequisitionDetailPage: () => <p>page:requisition-detail</p> }));
vi.mock('./pages/JobPostingDetailPage', () => ({ JobPostingDetailPage: () => <p>page:posting-detail</p> }));
vi.mock('./pages/InterviewDetailPage', () => ({ InterviewDetailPage: () => <p>page:interview-detail</p> }));

function renderAt(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <AppRoutes />
    </MemoryRouter>
  );
}

describe('search result links resolve to real pages', () => {
  it.each([
    ['a posting', '/jobpostings/5c1b2f7e-0000-4000-8000-000000000001', 'page:posting-detail'],
    ['a candidate, on their board', '/jobpostings/5c1b2f7e-0000-4000-8000-000000000001?application=9a0d-app', 'page:posting-detail'],
    ['a candidate, reached through a panel', '/interviews/7e3a-round', 'page:interview-detail'],
    ['a requisition', '/requisitions/1f2e-req', 'page:requisition-detail'],
  ])('%s', (_kind, url, page) => {
    renderAt(url);
    expect(screen.getByText(page)).toBeInTheDocument();
  });

  // The harness has to be able to tell a dead link from a live one, or the cases above prove
  // nothing. These are the two shapes the service used to send.
  it.each(['/candidates/0b1c-cand', '/jobs/5c1b2f7e-0000-4000-8000-000000000001'])(
    'still sends %s to the catch-all — which is why the service no longer emits it',
    (url) => {
      renderAt(url);
      expect(screen.getByText('page:requisitions')).toBeInTheDocument();
    }
  );
});
