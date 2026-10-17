import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { auth } from '../lib/auth';
import { ApprovalChainsPage } from './ApprovalChainsPage';

const apiMock = vi.hoisted(() => vi.fn());
vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

/**
 * The approval-chain builder went blank the moment one chain existed (found 2026-10-15). It read
 * `GET /api/users` as an array, but Milestone 4 made that endpoint paged — so naming a step's
 * approver called `.find` on an object and React unmounted the page. With no chains yet nothing
 * called `.find`, which is why it looked fine on an empty install.
 *
 * The mock answers with the API's real shapes, both of them, so this fails against either mistake.
 */
function serveRealShapes() {
  apiMock.mockImplementation((path: string) => {
    if (path === '/approvalchains') return Promise.resolve([{
      id: 'chain-1', name: 'Engineering approval', departmentId: 'dept-eng', isActive: true,
      steps: [{ sequence: 1, approverUserId: 'usr-aung', label: 'Finance sign-off' }],
    }]);
    if (path === '/departments') return Promise.resolve([{ id: 'dept-eng', name: 'Engineering', code: 'ENG' }]);
    if (path === '/users/selectable') return Promise.resolve([{ id: 'usr-aung', displayName: 'Aung Approver', role: 'Approver' }]);
    if (path.startsWith('/users')) return Promise.resolve({
      items: [{ id: 'usr-aung', email: 'aung@x.test', displayName: 'Aung Approver', role: 'Approver', isActive: true }],
      page: 1, pageSize: 20, totalCount: 1, totalPages: 1,
    });
    throw new Error(`unexpected call: ${path}`);
  });
}

describe('ApprovalChainsPage', () => {
  it('names each step’s approver once a chain exists', async () => {
    serveRealShapes();
    render(<ApprovalChainsPage />);

    expect(await screen.findByText('Engineering approval')).toBeInTheDocument();
    expect(screen.getAllByText(/Aung Approver/).length).toBeGreaterThan(0);
  });

  function signIn(permissions: string[]) {
    sessionStorage.clear();
    auth.set({
      accessToken: 'token', expiresAtUtc: '2099-01-01T00:00:00Z', role: 'HrDirector',
      displayName: 'Viewer', userId: 'usr-1', isSuperAdmin: false, permissions,
    });
  }

  // ADR-0022 amendment (2026-10-17): the HR Director reads chains (settings:read) but may not
  // create one (settings:update) — so the button that could only answer 403 is not offered.
  it('offers "New chain" only with settings:update', async () => {
    serveRealShapes();
    signIn(['permission:settings:settings:read']);
    const { unmount } = render(<ApprovalChainsPage />);
    expect(await screen.findByText('Engineering approval')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New chain' })).not.toBeInTheDocument();
    unmount();

    signIn(['permission:settings:settings:read', 'permission:settings:settings:update']);
    render(<ApprovalChainsPage />);
    expect(await screen.findByRole('button', { name: 'New chain' })).toBeInTheDocument();
  });
});
