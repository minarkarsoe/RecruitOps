import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { Component, type ReactNode } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ApprovalChain, CreateApprovalChainRequest } from '@recruitops/types';
import { auth } from '../lib/auth';
import { ApprovalChainsPage } from './ApprovalChainsPage';

/*
 * challenger_m1_1 — teamwork run tw2, milestone M1.
 *
 * M1 fixed a crash: the page asked `GET /users` for the approver picker, but that endpoint
 * returns PagedResult<UserListItemDto> — an object — so `users.map` (create form) and
 * `users.find` (existing chain steps, gated behind `chain.steps.length > 0`) threw
 * "is not a function" and the page rendered blank.
 *
 * The api mock below deliberately serves BOTH shapes:
 *   /users            -> the real paged object  { items, page, ... }
 *   /users/selectable -> the real bare array    [ { id, displayName, role } ]
 * so this file is a live contract pin: point the page back at `/users` and these tests
 * go red on the actual TypeError, not on a mock that was tuned to the new call.
 *
 * The ErrorBoundary is here because the crash happens in the re-render triggered by
 * setUsers() resolving, not in the first synchronous render — without a boundary React
 * reports it as an unhandled rejection and the assertions read as ordinary "not found".
 */

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

const AUNG = { id: 'usr-aung', displayName: 'Aung Myat', role: 'HrDirector' };
const THIDA = { id: 'usr-thida', displayName: 'Thida Win', role: 'HiringManager' };

const SELECTABLE = [AUNG, THIDA];

/** Exactly what UsersController.Get returns — PagedResult<UserListItemDto>. */
const PAGED_USERS = {
  items: [
    { id: AUNG.id, displayName: AUNG.displayName, email: 'aung@co.test', role: AUNG.role, isActive: true },
    { id: THIDA.id, displayName: THIDA.displayName, email: 'thida@co.test', role: THIDA.role, isActive: true },
  ],
  page: 1,
  pageSize: 20,
  totalCount: 2,
  totalPages: 1,
};

const DEPARTMENTS = [{ id: 'dept-eng', name: 'Engineering' }];

/** A chain that HAS steps — the condition gating the crashing `users.find` call. */
function chainWithSteps(overrides: Partial<ApprovalChain> = {}): ApprovalChain {
  return {
    id: 'chain-1',
    name: 'Default headcount approval',
    departmentId: 'dept-eng',
    isActive: true,
    steps: [
      { sequence: 1, approverUserId: AUNG.id, label: 'HR Review' },
      { sequence: 2, approverUserId: THIDA.id, label: 'Department sign-off' },
    ],
    ...overrides,
  };
}

let caught: Error | null = null;

class Boundary extends Component<{ children: ReactNode }, { err: Error | null }> {
  state = { err: null as Error | null };
  static getDerivedStateFromError(err: Error) { return { err }; }
  componentDidCatch(err: Error) { caught = err; }
  render() {
    return this.state.err
      ? <p data-testid="crash">{this.state.err.message}</p>
      : this.props.children;
  }
}

function serve(chains: ApprovalChain[], created?: ApprovalChain) {
  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    if (init?.method === 'POST' && path === '/approvalchains') {
      return Promise.resolve(created ?? chains[0]);
    }
    if (path === '/approvalchains') return Promise.resolve(chains);
    if (path === '/departments') return Promise.resolve(DEPARTMENTS);
    if (path === '/users/selectable') return Promise.resolve(SELECTABLE);
    if (path.startsWith('/users')) return Promise.resolve(PAGED_USERS); // the real shape
    return Promise.reject(new Error(`unexpected call: ${path}`));
  });
}

function renderAsAdmin() {
  return render(<Boundary><ApprovalChainsPage /></Boundary>);
}

beforeEach(() => {
  caught = null;
  vi.clearAllMocks();
  sessionStorage.clear();
  // The role this screen is for: an Admin configuring approval chains.
  auth.set({
    accessToken: 'token-admin',
    expiresAtUtc: '2099-01-01T00:00:00Z',
    role: 'Admin',
    displayName: 'Admin User',
    userId: 'usr-admin',
    isSuperAdmin: false,
    permissions: ['permission:settings:settings:read', 'permission:settings:settings:manage'],
  });
});

afterEach(() => {
  // A crash swallowed by the boundary must never read as a pass.
  expect(caught, caught ? `page crashed: ${caught.message}` : '').toBeNull();
});

describe('Admin views existing approval chains', () => {
  it('renders a chain that has steps without crashing', async () => {
    serve([chainWithSteps()]);
    renderAsAdmin();

    expect(await screen.findByText('Default headcount approval')).toBeInTheDocument();
    expect(screen.queryByTestId('crash')).not.toBeInTheDocument();
    expect(screen.getByText('2 steps')).toBeInTheDocument();
  });

  it('names each step approver instead of printing a raw GUID', async () => {
    serve([chainWithSteps()]);
    renderAsAdmin();

    // The lookup at ApprovalChainsPage.tsx falls back to s.approverUserId, so a render
    // showing the id means the picker data never arrived — a silent version of the bug.
    expect(await screen.findByText('Aung Myat')).toBeInTheDocument();
    expect(screen.getByText('Thida Win')).toBeInTheDocument();
    expect(screen.queryByText(AUNG.id)).not.toBeInTheDocument();
    expect(screen.queryByText(THIDA.id)).not.toBeInTheDocument();
    expect(screen.getByText('HR Review')).toBeInTheDocument();
    expect(screen.getByText('Department sign-off')).toBeInTheDocument();
  });

  it('resolves the department name for a department-scoped chain', async () => {
    serve([chainWithSteps()]);
    renderAsAdmin();

    expect(await screen.findByText(/Engineering/)).toBeInTheDocument();
    expect(screen.queryByText(/Specific department/)).not.toBeInTheDocument();
  });

  it('asks the endpoint that returns a bare array, never the paged directory', async () => {
    serve([chainWithSteps()]);
    renderAsAdmin();

    await screen.findByText('Aung Myat');
    const paths = apiMock.mock.calls.map(([p]) => p as string);
    expect(paths).toContain('/users/selectable');
    expect(paths).not.toContain('/users');
  });
});

describe('what the picker carries', () => {
  it('offers every active user, not just the first page of the directory', async () => {
    const many = Array.from({ length: 120 }, (_, i) => ({
      id: `usr-${i}`, displayName: `Staff ${i}`, role: 'Recruiter',
    }));
    apiMock.mockImplementation((path: string) => {
      if (path === '/approvalchains') return Promise.resolve([]);
      if (path === '/departments') return Promise.resolve(DEPARTMENTS);
      if (path === '/users/selectable') return Promise.resolve(many);
      if (path.startsWith('/users')) return Promise.resolve(PAGED_USERS);
      return Promise.reject(new Error(`unexpected call: ${path}`));
    });
    renderAsAdmin();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'New chain' }));

    // GET /users defaults to PageSize=20 (UserQueryParameters.cs), so unwrapping `.items`
    // would have silently truncated the picker at 20 on any company with more staff.
    expect(await screen.findByRole('option', { name: 'Staff 0 (Recruiter)' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Staff 119 (Recruiter)' })).toBeInTheDocument();
  });

  it('renders a Burmese approver name intact', async () => {
    const burmese = { id: 'usr-mm', displayName: 'ဒေါ်သီတာဝင်း', role: 'HrDirector' };
    apiMock.mockImplementation((path: string) => {
      if (path === '/approvalchains') {
        return Promise.resolve([chainWithSteps({
          steps: [{ sequence: 1, approverUserId: burmese.id, label: 'ဌာနအတည်ပြုချက်' }],
        })]);
      }
      if (path === '/departments') return Promise.resolve(DEPARTMENTS);
      if (path === '/users/selectable') return Promise.resolve([burmese]);
      if (path.startsWith('/users')) return Promise.resolve(PAGED_USERS);
      return Promise.reject(new Error(`unexpected call: ${path}`));
    });
    renderAsAdmin();

    expect(await screen.findByText('ဒေါ်သီတာဝင်း')).toBeInTheDocument();
    expect(screen.getByText('ဌာနအတည်ပြုချက်')).toBeInTheDocument();
  });

  /*
   * FIXED (was DEFECT RECORD, challenger_m1_1 🟡 → closed by the M1 remediation Worker,
   * D5): /users/selectable filters on IsActive (UsersController.cs), and the page reuses
   * that same list to *label* the steps of chains that already exist. Deactivate an
   * approver and the chain that routes to them stops naming them via the picker list — the
   * lookup now falls back to a readable sentence instead of the raw GUID.
   * Per this test's own original instruction ("flip the two expectations when the display
   * lookup stops depending on the picker list"): expectations flipped here.
   */
  it('a deactivated approver renders a readable fallback, not a raw GUID', async () => {
    apiMock.mockImplementation((path: string) => {
      if (path === '/approvalchains') {
        return Promise.resolve([chainWithSteps({
          steps: [{ sequence: 1, approverUserId: 'usr-gone', label: 'HR Review' }],
        })]);
      }
      if (path === '/departments') return Promise.resolve(DEPARTMENTS);
      if (path === '/users/selectable') return Promise.resolve(SELECTABLE); // usr-gone absent
      if (path.startsWith('/users')) return Promise.resolve(PAGED_USERS);
      return Promise.reject(new Error(`unexpected call: ${path}`));
    });
    renderAsAdmin();

    await screen.findByText('HR Review');
    expect(screen.getByText('Unknown approver (no longer active)')).toBeInTheDocument();
    expect(screen.queryByText('usr-gone')).not.toBeInTheDocument();
  });
});

describe('Admin opens the create form', () => {
  it('populates the approver dropdown with selectable users', async () => {
    serve([chainWithSteps()]);
    renderAsAdmin();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'New chain' }));

    // Two dropdowns exist on the form: department, and the step's approver picker.
    expect(screen.getAllByRole('combobox')).toHaveLength(2);

    // Both users must be offerable, labelled name + role.
    expect(await screen.findByRole('option', { name: 'Aung Myat (HrDirector)' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Thida Win (HiringManager)' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Engineering' })).toBeInTheDocument();
  });

  it('creates a chain end to end and shows it with the approver name', async () => {
    const created: ApprovalChain = {
      id: 'chain-2',
      name: 'Engineering headcount',
      departmentId: 'dept-eng',
      isActive: true,
      steps: [{ sequence: 1, approverUserId: THIDA.id, label: 'Dept sign-off' }],
    };
    serve([], created);
    renderAsAdmin();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'New chain' }));

    await user.type(screen.getByPlaceholderText('e.g. Default headcount approval'), 'Engineering headcount');
    await user.type(screen.getByPlaceholderText('Step label (e.g. HR Review)'), 'Dept sign-off');

    const selects = screen.getAllByRole('combobox');
    await user.selectOptions(selects[0], 'dept-eng');           // department
    await user.selectOptions(selects[1], THIDA.id);             // approver — needs the picker
    await user.click(screen.getByRole('button', { name: 'Create chain' }));

    const post = apiMock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST');
    expect(post, 'no POST was issued').toBeDefined();
    const body = JSON.parse((post![1] as RequestInit).body as string) as CreateApprovalChainRequest;
    expect(body).toEqual({
      name: 'Engineering headcount',
      departmentId: 'dept-eng',
      steps: [{ label: 'Dept sign-off', approverUserId: THIDA.id }],
    });

    await waitFor(() => expect(screen.getByText('Engineering headcount')).toBeInTheDocument());
    expect(screen.getByText('Thida Win')).toBeInTheDocument();
  });
});
