import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
    if (path === '/settings/offers') return Promise.resolve({ requireOfferApproval: false, showOfferSalaryToHiringManagers: false });
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

/*
 * Offers (ADR-0030): a chain says what it approves, and the company's offer policy sits beside the
 * chains it routes offers through. The policy is two financial/access controls, so a reader without
 * settings:update must not be shown switches that look live, and a refused read shows no card.
 */
describe('ApprovalChainsPage — offers', () => {
  function serveOffers(settings: Promise<unknown>) {
    apiMock.mockImplementation((path: string, init?: RequestInit) => {
      if (path === '/settings/offers' && init?.method === 'PUT') {
        return Promise.resolve(JSON.parse(init.body as string));
      }
      if (path === '/settings/offers') return settings;
      if (path === '/approvalchains' && init?.method === 'POST') {
        return Promise.resolve({ ...JSON.parse(init.body as string), id: 'chain-new', isActive: true,
          steps: [{ sequence: 1, approverUserId: 'usr-aung', label: 'Finance' }] });
      }
      if (path === '/approvalchains') return Promise.resolve([{
        id: 'chain-offer', name: 'Sales offers', departmentId: null, isActive: true, purpose: 'Offer',
        steps: [{ sequence: 1, approverUserId: 'usr-aung', label: 'Finance' }],
      }]);
      if (path === '/departments') return Promise.resolve([]);
      if (path === '/users/selectable') return Promise.resolve([{ id: 'usr-aung', displayName: 'Aung Approver', role: 'Approver' }]);
      return Promise.reject(new Error(`unexpected call: ${path}`));
    });
  }

  // The role decides as much as the list: the SPA treats Admin as holding every permission.
  function signInWith(permissions: string[], role: 'Admin' | 'HrDirector' = 'HrDirector') {
    sessionStorage.clear();
    auth.set({
      accessToken: 'token', expiresAtUtc: '2099-01-01T00:00:00Z', role,
      displayName: 'Viewer', userId: 'usr-admin', isSuperAdmin: false, permissions,
    });
  }

  const READ = 'permission:settings:settings:read';
  const UPDATE = 'permission:settings:settings:update';

  it('says which chains approve offers', async () => {
    signInWith([READ]);
    serveOffers(Promise.resolve({ requireOfferApproval: false, showOfferSalaryToHiringManagers: false }));
    render(<ApprovalChainsPage />);

    const name = await screen.findByText('Sales offers');
    expect(name.nextElementSibling).toHaveTextContent(/^Offers · Company-wide/);
  });

  it('creates an offer chain when "Approves" is set to offers', async () => {
    signInWith([READ, UPDATE]);
    serveOffers(Promise.resolve({ requireOfferApproval: false, showOfferSalaryToHiringManagers: false }));
    render(<ApprovalChainsPage />);

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'New chain' }));
    await user.type(screen.getByPlaceholderText('e.g. Default headcount approval'), 'Offer approval');
    await user.selectOptions(screen.getByLabelText('Approves'), 'Offer');
    await user.type(screen.getByPlaceholderText('Step label (e.g. HR Review)'), 'Finance');
    const selects = screen.getAllByRole('combobox');
    await user.selectOptions(selects[selects.length - 1], 'usr-aung');
    await user.click(screen.getByRole('button', { name: 'Create chain' }));

    const post = apiMock.mock.calls.find(([p, init]) => p === '/approvalchains' && (init as RequestInit | undefined)?.method === 'POST');
    expect(post, 'no POST was issued').toBeDefined();
    expect(JSON.parse((post![1] as RequestInit).body as string)).toMatchObject({ purpose: 'Offer' });
  });

  it('changes the offer policy with settings:update, sending both switches', async () => {
    signInWith([READ, UPDATE], 'Admin');
    serveOffers(Promise.resolve({ requireOfferApproval: false, showOfferSalaryToHiringManagers: true }));
    render(<ApprovalChainsPage />);

    const user = userEvent.setup();
    const every = await screen.findByRole('checkbox', { name: /Every offer needs approval/ });
    expect(every).not.toBeChecked();
    await user.click(every);

    const put = apiMock.mock.calls.find(([p, init]) => p === '/settings/offers' && (init as RequestInit | undefined)?.method === 'PUT');
    expect(put, 'no PUT was issued').toBeDefined();
    // Both fields, always: a missing one must never read as "off" on the server.
    expect(JSON.parse((put![1] as RequestInit).body as string)).toEqual({
      requireOfferApproval: true, showOfferSalaryToHiringManagers: true,
    });
    expect(await screen.findByRole('status')).toHaveTextContent('Saved.');
    expect(screen.getByRole('checkbox', { name: /Every offer needs approval/ })).toBeChecked();
  });

  it('shows the policy read-only without settings:update', async () => {
    signInWith([READ]);
    serveOffers(Promise.resolve({ requireOfferApproval: true, showOfferSalaryToHiringManagers: false }));
    render(<ApprovalChainsPage />);

    const every = await screen.findByRole('checkbox', { name: /Every offer needs approval/ });
    expect(every).toBeChecked();
    expect(every).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: /Hiring Managers see offered salaries/ })).toBeDisabled();
  });

  it('shows no policy card when the read is refused, and the chains still load', async () => {
    signInWith([READ]);
    serveOffers(Promise.reject(new Error('Forbidden')));
    render(<ApprovalChainsPage />);

    expect(await screen.findByText('Sales offers')).toBeInTheDocument();
    expect(screen.queryByText('Offer policy')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
