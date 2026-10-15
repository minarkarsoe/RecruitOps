import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ApprovalChain, DepartmentListItem, SelectableUser } from '@recruitops/types';
import { ApiError } from '../lib/api';
import { ApprovalChainsPage } from './ApprovalChainsPage';

/*
 * challenger_m1_2 — FAILURE MODES, INPUTS AND DEFAULTS on the M1 fix.
 *
 * The original defect was not the crash, it was the *invisibility*: three `.catch`
 * handlers swallowed every error so a 403 rendered as "No approval chains yet".
 * These tests drive the three loads independently and assert what the user actually
 * reads on screen, not what the state variables hold.
 */

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

type Route = {
  chains?: ApprovalChain[] | unknown | (() => Promise<unknown>);
  departments?: DepartmentListItem[] | (() => Promise<unknown>);
  users?: SelectableUser[] | (() => Promise<unknown>);
  post?: () => Promise<unknown>;
};

/** Route `api()` by path so a test only states the responses it cares about. */
function serve(routes: Route) {
  const resolve = (v: unknown) =>
    typeof v === 'function' ? (v as () => Promise<unknown>)() : Promise.resolve(v);

  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    if (init?.method === 'POST' && path === '/approvalchains') {
      return routes.post ? routes.post() : Promise.resolve(chain());
    }
    if (path === '/approvalchains') return resolve(routes.chains ?? []);
    if (path === '/departments') return resolve(routes.departments ?? []);
    if (path === '/users/selectable') return resolve(routes.users ?? []);
    throw new Error(`unexpected call: ${path}`);
  });
}

const forbidden = (detail: string) => () => Promise.reject(new ApiError(403, detail));

function chain(over: Partial<ApprovalChain> = {}): ApprovalChain {
  return {
    id: 'ch-1',
    name: 'Default headcount approval',
    departmentId: null,
    isActive: true,
    steps: [{ sequence: 1, label: 'HR Review', approverUserId: 'usr-1' }],
    ...over,
  } as ApprovalChain;
}

const EMPTY_STATE = /No approval chains yet/;

// ───────────────────────────────────────────────────────────────────────────
// 1. Can a failure still masquerade as an empty state?
// ───────────────────────────────────────────────────────────────────────────

describe('a forbidden chain load', () => {
  it('shows the server error text', async () => {
    serve({ chains: forbidden('Forbidden: AdminOnly.') });
    render(<ApprovalChainsPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Forbidden: AdminOnly.');
  });

  it('DEFECT: still renders the "No approval chains yet" empty state alongside the error', async () => {
    serve({ chains: forbidden('Forbidden: AdminOnly.') });
    render(<ApprovalChainsPage />);
    await screen.findByRole('alert');

    // A failed load is not an empty list. The card claims the list is empty and invites
    // the user to "Create one" — an action that will also 403.
    expect(screen.queryByText(EMPTY_STATE)).not.toBeInTheDocument();
  });

  it('DEFECT: Cancel on the create form wipes the load error, restoring the original bug', async () => {
    serve({ chains: forbidden('Forbidden: AdminOnly.') });
    render(<ApprovalChainsPage />);
    await screen.findByRole('alert');

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'New chain' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    // Two clicks and the 403 is gone; the screen is now byte-identical to a tenant that
    // genuinely has no chains. This is exactly the bug M1 set out to remove.
    expect(screen.queryByRole('alert')).toBeInTheDocument();
  });

  it('FIXED: an ApiError carrying an empty message still renders a non-blank alert', async () => {
    // An ApiError can reach the page with a blank `message` (e.g. a ProblemDetails whose
    // `detail` and `title` were both empty). `errorMessage()` in ApprovalChainsPage.tsx
    // treats a blank/whitespace-only message the same as absent and falls back to a canned
    // string, so the page must never render an alert with no visible text.
    serve({ chains: forbidden('') });
    render(<ApprovalChainsPage />);

    const alert = await screen.findByRole('alert');
    expect((alert.textContent ?? '').trim()).not.toBe('');
  });

  it('falls back to a canned message when the rejection is not an Error', async () => {
    serve({ chains: () => Promise.reject({ status: 403 }) });
    render(<ApprovalChainsPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load approval chains.');
  });
});

// ───────────────────────────────────────────────────────────────────────────
// 2. Partial failure — one `error` state, three writers, last write wins.
// ───────────────────────────────────────────────────────────────────────────

describe('partial failure across the three loads', () => {
  it('DEFECT: a departments failure overwrites the chains failure that actually matters', async () => {
    serve({
      chains: forbidden('You are not permitted to view approval chains.'),
      departments: forbidden('Departments are admin-only.'),
      users: [{ id: 'usr-1', displayName: 'Aye Aye', role: 'Recruiter' } as SelectableUser],
    });
    render(<ApprovalChainsPage />);
    await screen.findByRole('alert');

    // Both fail; the user is told about the least important one.
    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent('You are not permitted to view approval chains.'),
    );
  });

  it('surfaces an approver-load failure even when chains load fine', async () => {
    serve({ chains: [chain()], users: forbidden('No access to the user list.') });
    render(<ApprovalChainsPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('No access to the user list.');
  });

  it('FIXED: the create form explains why the approver dropdown is unfillable after /users/selectable fails', async () => {
    serve({ chains: [], users: forbidden('No access to the user list.') });
    render(<ApprovalChainsPage />);
    await screen.findByRole('alert');

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'New chain' }));

    const selects = screen.getAllByRole('combobox');
    const approver = selects[selects.length - 1];
    // A raw option count is a weak proxy for "the user was told why" — assert on the text
    // of the extra option itself, and that it is disabled (not a selectable, fake approver).
    const options = Array.from(approver.querySelectorAll('option'));
    const explanatory = options.find((o) => /could not load approvers/i.test(o.textContent ?? ''));
    expect(explanatory).toBeDefined();
    expect(explanatory).toBeDisabled();
  });
});

// ───────────────────────────────────────────────────────────────────────────
// 3. Empty and degenerate data.
// ───────────────────────────────────────────────────────────────────────────

describe('degenerate chain and user data', () => {
  it('renders a zero-step chain without crashing and says "0 steps"', async () => {
    serve({ chains: [chain({ steps: [] })] });
    render(<ApprovalChainsPage />);

    expect(await screen.findByText('Default headcount approval')).toBeInTheDocument();
    expect(screen.getByText('0 steps')).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('DEFECT: shows a bare GUID when the step approver is no longer in the selectable list', async () => {
    // /users/selectable is active-only. An approver who left the company vanishes from it
    // while their chain step remains.
    serve({
      chains: [chain({ steps: [{ sequence: 1, label: 'HR Review', approverUserId: '3f9a1c22-7b4d-4e51-9c2a-0d8e6f5b1a90' }] })],
      users: [{ id: 'usr-other', displayName: 'Still Here', role: 'Recruiter' } as SelectableUser],
    });
    render(<ApprovalChainsPage />);

    await screen.findByText('HR Review');
    expect(screen.queryByText('3f9a1c22-7b4d-4e51-9c2a-0d8e6f5b1a90')).not.toBeInTheDocument();
  });

  it('DEFECT: a whitespace-only displayName renders as a blank approver cell', async () => {
    serve({
      chains: [chain()],
      users: [{ id: 'usr-1', displayName: '   ', role: 'Recruiter' } as SelectableUser],
    });
    const { container } = render(<ApprovalChainsPage />);
    await screen.findByText('HR Review');

    // The approver cell is the last span in the step <li> (the first is the sequence badge).
    const li = container.querySelector('li')!;
    const approverCell = li.querySelectorAll('span')[li.querySelectorAll('span').length - 1];
    // `?? s.approverUserId` only fires on null/undefined — a blank name wins the lookup and
    // the row silently loses its approver.
    expect((approverCell.textContent ?? '').trim()).not.toBe('');
  });

  it('renders a very long chain name and Zawgyi Burmese text without crashing', async () => {
    const long = 'A'.repeat(5000);
    serve({
      chains: [
        chain({ id: 'ch-long', name: long, steps: [] }),
        chain({ id: 'ch-my', name: 'ျမန္မာ အတည္ျပဳခ်က္', steps: [] }),
      ],
    });
    render(<ApprovalChainsPage />);

    expect(await screen.findByText(long)).toBeInTheDocument();
    expect(screen.getByText('ျမန္မာ အတည္ျပဳခ်က္')).toBeInTheDocument();
  });

  it('DEFECT: an empty /approvalchains body (204) renders neither a list, an empty state, nor an error', async () => {
    // lib/api.ts returns `undefined` for an empty body. `chains` is then undefined:
    // `chains === null` is false, `chains?.length === 0` is false. The page renders nothing.
    serve({ chains: () => Promise.resolve(undefined) });
    const { container } = render(<ApprovalChainsPage />);

    await waitFor(() => expect(apiMock).toHaveBeenCalledWith('/approvalchains'));
    expect(
      screen.queryByText(EMPTY_STATE) ?? screen.queryByRole('alert') ?? screen.queryByText('Loading…'),
    ).not.toBeNull();
    expect(container).toBeTruthy();
  });
});

// ───────────────────────────────────────────────────────────────────────────
// 3a. Cancelled mid-flight — the effect has no cleanup and no AbortController.
// ───────────────────────────────────────────────────────────────────────────

describe('navigating away before the loads settle', () => {
  it('does not raise a console error when all three reject after unmount', async () => {
    let rejectAll: () => void = () => {};
    const pending = new Promise<never>((_, rej) => {
      rejectAll = () => rej(new ApiError(403, 'Forbidden: AdminOnly.'));
    });
    serve({ chains: () => pending, departments: () => pending, users: () => pending });

    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const { unmount } = render(<ApprovalChainsPage />);
    unmount();
    rejectAll();
    await new Promise((r) => setTimeout(r, 20));

    const messages = spy.mock.calls.map((c) => String(c[0]));
    spy.mockRestore();
    expect(messages.filter((m) => /not wrapped in act|unmounted component/i.test(m))).toHaveLength(0);
  });
});

// ───────────────────────────────────────────────────────────────────────────
// 3b. What message does a *real* ASP.NET 403 actually produce?
//     The page prints `e.message` verbatim, so this is the literal user-facing string.
// ───────────────────────────────────────────────────────────────────────────

describe('the message a real 403 produces through lib/api', () => {
  it('is the raw "Request failed (403)" when the policy returns an empty body', async () => {
    const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response('', { status: 403 }));

    const err = await actual.apiFetch('/approvalchains').catch((e: unknown) => e);
    fetchSpy.mockRestore();

    expect(err).toBeInstanceOf(actual.ApiError);
    // Not "You do not have permission to manage approval chains" — a bare status code.
    expect((err as Error).message).toBe('Request failed (403)');
  });

  it('DEFECT: a ProblemDetails with an empty detail yields an empty message, which the page cannot render', async () => {
    const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ title: 'Forbidden', detail: '', status: 403 }), {
        status: 403,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const err = await actual.apiFetch('/approvalchains').catch((e: unknown) => e);
    fetchSpy.mockRestore();

    // `problem?.detail ?? problem?.title` — '' is not nullish, so the title fallback never
    // fires and the page's `{error && ...}` guard drops the alert entirely.
    expect((err as Error).message).not.toBe('');
  });
});

// ───────────────────────────────────────────────────────────────────────────
// 4. Submitting against a degraded page.
// ───────────────────────────────────────────────────────────────────────────

describe('submitting when the approver list is empty', () => {
  it('does not POST a chain whose approver was never chosen', async () => {
    serve({ chains: [], users: [] });
    render(<ApprovalChainsPage />);
    await screen.findByText(EMPTY_STATE);

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'New chain' }));
    await user.type(screen.getByPlaceholderText('e.g. Default headcount approval'), 'My chain');
    await user.type(screen.getByPlaceholderText('Step label (e.g. HR Review)'), 'HR Review');
    await user.click(screen.getByRole('button', { name: 'Create chain' }));

    const posts = apiMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === 'POST');
    expect(posts).toHaveLength(0);
  });

  it('shows the server message when the POST is rejected', async () => {
    serve({
      chains: [],
      users: [{ id: 'usr-1', displayName: 'Aye Aye', role: 'Recruiter' } as SelectableUser],
      post: () => Promise.reject(new ApiError(404, "An approver isn't a user here.")),
    });
    render(<ApprovalChainsPage />);
    await screen.findByText(EMPTY_STATE);

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'New chain' }));
    await user.type(screen.getByPlaceholderText('e.g. Default headcount approval'), 'My chain');
    await user.type(screen.getByPlaceholderText('Step label (e.g. HR Review)'), 'HR Review');
    const selects = screen.getAllByRole('combobox');
    await user.selectOptions(selects[selects.length - 1], 'usr-1');
    await user.click(screen.getByRole('button', { name: 'Create chain' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("An approver isn't a user here.");
  });
});
