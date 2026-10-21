import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { ApprovalStep, OfferDetail, UserRole } from '@recruitops/types';
import { auth } from '../lib/auth';
import { OfferDetailPage, baseToFit } from './OfferDetailPage';

/*
 * One offer (`design/internal/offer-create.html`, ADR-0030).
 *
 * The rules a reader has to be able to see before acting:
 *   - the button says what it will do — "Send for internal approval" the moment the typed
 *     package passes the budget, before anything is saved;
 *   - with no offer chain, the editor says so instead of failing at submit;
 *   - an approver's decision names the round on screen, so a revision since is refused;
 *   - a Hiring Manager without the company's say-so sees no amount and no controls.
 */

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

const ME = 'usr-me';

function signInAs(role: UserRole) {
  auth.set({
    accessToken: 'token', expiresAtUtc: '2099-01-01T00:00:00Z', role,
    displayName: 'Test User', userId: ME, isSuperAdmin: false, permissions: [],
  });
}

function step(partial: Partial<ApprovalStep> & Pick<ApprovalStep, 'round' | 'sequence' | 'label' | 'approverUserId'>): ApprovalStep {
  return { decision: 'Waiting', decidedAt: null, comment: null, decidedByUserId: null, ...partial };
}

function offer(overrides: Partial<OfferDetail> = {}): OfferDetail {
  return {
    id: 'ofr-1', reference: 'OFR-2026-0001', status: 'Draft',
    jobApplicationId: 'app-1', requisitionId: 'req-1',
    candidateName: 'Daw Hnin Wai', position: 'Sales Ops Lead',
    departmentId: 'dept-sales', departmentName: 'Sales',
    salaryVisible: true, baseSalary: 800000, allowances: 0, salaryBudget: 800000,
    overBudget: false, needsApproval: false, policyRequiresApproval: false,
    joiningDate: '2026-11-20', probationMonths: 3, expiresOn: '2026-10-28',
    offerTemplateId: null, letterBody: 'Dear {{CandidateName}}', canManage: true,
    approvalPreview: ['Finance Controller'], approvals: [],
    createdAt: '2026-10-21T09:00:00Z', submittedAt: null, approvedAt: null, sentAt: null,
    ...overrides,
  };
}

/** Answers GET with `initial`, and each write with what `after` says the server returns. */
function serve(initial: OfferDetail, after: Partial<Record<string, OfferDetail>> = {}) {
  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    if (method === 'GET' && path === '/offers/ofr-1') return Promise.resolve(initial);
    const key = `${method} ${path}`;
    if (key in after) return Promise.resolve(after[key]);
    return Promise.reject(new Error(`unexpected call: ${key}`));
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/offers/ofr-1']}>
      <Routes>
        <Route path="/offers/:id" element={<OfferDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

function callsTo(method: string) {
  return apiMock.mock.calls.filter(([, init]) => ((init as RequestInit | undefined)?.method ?? 'GET') === method);
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
});

describe('OfferDetailPage — the budget check', () => {
  it('switches to internal approval the moment the typed package passes the budget', async () => {
    signInAs('Recruiter');
    serve(offer());
    renderPage();

    // Within budget: straight to the candidate — which is slice B, so the button says it is not here yet.
    expect(await screen.findByRole('button', { name: 'Send to candidate' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Send for internal approval' })).not.toBeInTheDocument();

    const user = userEvent.setup();
    await user.type(screen.getByLabelText('Allowances (MMK / month)'), '50000');

    // Nothing saved yet — the warning is about what is typed.
    expect(screen.getByRole('button', { name: 'Send for internal approval' })).toBeEnabled();
    expect(screen.getByText('This offer needs approval before it can be sent')).toBeInTheDocument();
    expect(screen.getByLabelText('Base salary (MMK / month)')).toHaveAttribute('aria-invalid', 'true');
    // 800,000 budget less 50,000 allowances: the base that would fit.
    expect(screen.getByText('750,000')).toBeInTheDocument();
    expect(screen.getByText('Finance Controller')).toBeInTheDocument();
    expect(callsTo('PUT')).toHaveLength(0);
  });

  it('says there is no offer chain instead of failing at submit', async () => {
    signInAs('Recruiter');
    serve(offer({ baseSalary: 900000, overBudget: true, needsApproval: true, approvalPreview: null }));
    renderPage();

    expect(await screen.findByText(/No offer approval chain is set up for this department/)).toBeInTheDocument();
  });

  it('routes a within-budget offer to approval when the company requires it, and says why', async () => {
    signInAs('Recruiter');
    serve(offer({ policyRequiresApproval: true, needsApproval: true }));
    renderPage();

    expect(await screen.findByRole('button', { name: 'Send for internal approval' })).toBeEnabled();
    expect(screen.getByText(/Your company requires approval for every offer/)).toBeInTheDocument();
    expect(screen.queryByText('This offer needs approval before it can be sent')).not.toBeInTheDocument();
  });

  it('saves the terms on screen before submitting, so the approver sees what was typed', async () => {
    signInAs('Recruiter');
    const pending = offer({
      status: 'PendingApproval', baseSalary: 900000, overBudget: true,
      approvals: [step({ round: 1, sequence: 1, label: 'Finance Controller', approverUserId: 'usr-fin' })],
    });
    serve(offer({ baseSalary: 900000, overBudget: true, needsApproval: true }), {
      'PUT /offers/ofr-1': offer({ baseSalary: 900000, overBudget: true }),
      'POST /offers/ofr-1/submit': pending,
    });
    renderPage();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Send for internal approval' }));

    await screen.findByRole('button', { name: 'Revise offer' });
    const writes = apiMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method);
    expect(writes.map(([p, init]) => `${(init as RequestInit).method} ${p}`)).toEqual([
      'PUT /offers/ofr-1', 'POST /offers/ofr-1/submit',
    ]);
    expect(JSON.parse((writes[0][1] as RequestInit).body as string)).toMatchObject({ baseSalary: 900000 });
  });
});

describe('OfferDetailPage — deciding', () => {
  it('binds the decision to the round on screen', async () => {
    signInAs('Approver');
    // Round 1 was rejected, the offer revised and resubmitted: round 2 is the live one.
    const approvals = [
      step({ round: 1, sequence: 1, label: 'Finance Controller', approverUserId: ME, decision: 'Rejected', comment: 'Too high', decidedAt: '2026-10-20T10:00:00Z' }),
      step({ round: 2, sequence: 1, label: 'Finance Controller', approverUserId: ME }),
    ];
    serve(offer({ status: 'PendingApproval', canManage: false, approvals }), {
      'POST /offers/ofr-1/decide': offer({ status: 'Approved', canManage: false, approvals }),
    });
    renderPage();

    const user = userEvent.setup();
    await user.type(await screen.findByLabelText(/Comment/), 'Fine at this level');
    await user.click(screen.getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(callsTo('POST')).toHaveLength(1));
    const [path, init] = callsTo('POST')[0];
    expect(path).toBe('/offers/ofr-1/decide');
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({ approve: true, comment: 'Fine at this level', round: 2 });
    // Both attempts stay on record.
    expect(screen.getByText(/Attempt 1/)).toBeInTheDocument();
    expect(screen.getByText(/Attempt 2/)).toBeInTheDocument();
  });

  it('offers no decision to someone not waiting on the live round', async () => {
    signInAs('Approver');
    serve(offer({
      status: 'PendingApproval', canManage: false,
      approvals: [step({ round: 1, sequence: 1, label: 'Finance Controller', approverUserId: 'usr-someone-else' })],
    }));
    renderPage();

    await screen.findByText('Approval');
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
  });

  it('shows the server\'s refusal when the offer was revised meanwhile', async () => {
    signInAs('Approver');
    const approvals = [step({ round: 1, sequence: 1, label: 'Finance Controller', approverUserId: ME })];
    apiMock.mockImplementation((_path: string, init?: RequestInit) => {
      if (!init?.method) return Promise.resolve(offer({ status: 'PendingApproval', canManage: false, approvals }));
      return Promise.reject(new Error('This offer was revised since you opened it. Reload to see the current terms.'));
    });
    renderPage();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Approve' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('This offer was revised since you opened it');
  });
});

describe('OfferDetailPage — a Hiring Manager', () => {
  it('reads the offer with no amount, no letter and no controls', async () => {
    signInAs('HiringManager');
    serve(offer({
      salaryVisible: false, baseSalary: null, allowances: null, salaryBudget: null,
      overBudget: null, needsApproval: null, policyRequiresApproval: null,
      letterBody: null, canManage: false, approvalPreview: null,
    }));
    renderPage();

    expect(await screen.findByText(/Salary is hidden/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Base salary (MMK / month)')).not.toBeInTheDocument();
    expect(screen.queryByText('Offer letter')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Joining date')).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Save draft|Send|Revise/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete draft' })).not.toBeInTheDocument();
  });
});

describe('baseToFit', () => {
  it('is the budget less the allowances, never below zero', () => {
    expect(baseToFit(800000, 50000)).toBe(750000);
    expect(baseToFit(800000, 900000)).toBe(0);
  });
});
