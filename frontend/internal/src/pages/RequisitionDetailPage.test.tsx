import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { ApprovalStep, RequisitionDetail } from '@recruitops/types';
import { auth } from '../lib/auth';
import { RequisitionDetailPage } from './RequisitionDetailPage';

/*
 * The two rules a reader of this page has to be able to see, per ADR-0023 and ADR-0024:
 *
 *   - a senior approving forward is told, before they click, which steps they are closing
 *     for other people — and is not offered Reject for those steps;
 *   - a rejection survives the revision it caused, and the two attempts read as two
 *     attempts rather than one interleaved chain.
 *
 * Both are things the backend enforces, so a bug here does not throw. It silently shows
 * the wrong affordance or the wrong history, which is why they are pinned by what the
 * page actually says rather than by what it calls.
 */

const HR_ID = 'usr-hr';
const FINANCE_ID = 'usr-finance';
const REQUESTER_ID = 'usr-requester';

beforeEach(() => {
  sessionStorage.clear();
});

function signInAs(userId: string, permissions: string[]) {
  auth.set({
    accessToken: 'token',
    expiresAtUtc: '2099-01-01T00:00:00Z',
    role: 'Approver',
    displayName: 'Test User',
    userId,
    isSuperAdmin: false,
    permissions,
  });
}

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

function step(partial: Partial<ApprovalStep> & Pick<ApprovalStep, 'round' | 'sequence' | 'label' | 'approverUserId'>): ApprovalStep {
  return {
    decision: 'Waiting',
    decidedAt: null,
    comment: null,
    decidedByUserId: null,
    ...partial,
  };
}

function requisition(overrides: Partial<RequisitionDetail> = {}): RequisitionDetail {
  return {
    id: 'req-1',
    departmentId: 'dept-1',
    departmentName: 'Alpha Sales',
    title: 'Sales Ops Lead',
    headcount: 1,
    salaryBudget: null,
    status: 'PendingApproval',
    submittedAt: '2026-08-10T09:00:00Z',
    awaitingApprovalFrom: 'HR',
    yourStepLabel: null,
    decidedAt: null,
    jobDescription: 'Because we need one.',
    requestedByUserId: REQUESTER_ID,
    approvals: [
      step({ round: 1, sequence: 1, label: 'HR', approverUserId: HR_ID }),
      step({ round: 1, sequence: 2, label: 'Finance', approverUserId: FINANCE_ID }),
    ],
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/requisitions/req-1']}>
      <Routes>
        <Route path="/requisitions/:id" element={<RequisitionDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('skipping ahead (ADR-0024)', () => {
  it('warns the senior which steps they are closing for other people, and hides Reject', async () => {
    signInAs(FINANCE_ID, ['permission:requisitions:requisitions:approve']);
    apiMock.mockResolvedValue(requisition());

    renderPage();

    // Finance holds step 2 while the chain waits on HR, so the decision form must appear —
    // this is the whole feature. Before ADR-0024 it was hidden.
    expect(await screen.findByRole('button', { name: /Approve 2 steps/ })).toBeInTheDocument();

    // And it must say whose step is being closed. "Approve" with no warning would let a
    // senior sign for someone without realising it.
    expect(screen.getByText(/not your turn yet/i)).toBeInTheDocument();
    expect(screen.getByText('HR', { selector: 'strong' })).toBeInTheDocument();

    // Reject is forward-blocked, so offering it would produce a 409 the user cannot act on.
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
  });

  it('offers a plain Approve and Reject when it genuinely is your turn', async () => {
    signInAs(HR_ID, ['permission:requisitions:requisitions:approve']);
    apiMock.mockResolvedValue(requisition());

    renderPage();

    expect(await screen.findByRole('button', { name: 'Approve' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
    expect(screen.queryByText(/not your turn yet/i)).not.toBeInTheDocument();
  });

  it('names who really decided a step that a senior closed', async () => {
    signInAs(REQUESTER_ID, []);
    apiMock.mockResolvedValue(requisition({
      status: 'Approved',
      approvals: [
        step({
          round: 1, sequence: 1, label: 'HR', approverUserId: HR_ID,
          decision: 'Approved', decidedAt: '2026-08-11T09:00:00Z',
          decidedByUserId: FINANCE_ID,
        }),
        step({
          round: 1, sequence: 2, label: 'Finance', approverUserId: FINANCE_ID,
          decision: 'Approved', decidedAt: '2026-08-11T09:00:00Z',
        }),
      ],
    }));

    renderPage();

    // The product owner's requirement in their own words: "it must show the record of what
    // I did." Rendering this as a plain "Approved" would make the chain claim HR decided it.
    expect(await screen.findByText(/Approved by Finance on behalf of HR/)).toBeInTheDocument();
  });
});

describe('revise and resubmit (ADR-0023)', () => {
  it('keeps the rejection readable beside the new attempt, labelled as separate rounds', async () => {
    signInAs(REQUESTER_ID, ['permission:requisitions:requisitions:update']);
    apiMock.mockResolvedValue(requisition({
      approvals: [
        step({
          round: 1, sequence: 1, label: 'HR', approverUserId: HR_ID,
          decision: 'Rejected', decidedAt: '2026-08-11T09:00:00Z',
          comment: 'Headcount not justified.',
        }),
        step({ round: 1, sequence: 2, label: 'Finance', approverUserId: FINANCE_ID }),
        step({ round: 2, sequence: 1, label: 'HR', approverUserId: HR_ID }),
        step({ round: 2, sequence: 2, label: 'Finance', approverUserId: FINANCE_ID }),
      ],
    }));

    renderPage();

    // The reviewer's reasoning is the reason the revision exists, so it has to survive.
    expect(await screen.findByText(/Headcount not justified/)).toBeInTheDocument();

    // Two attempts, distinguishable. Flattened, the reader sees HR twice with no
    // explanation and cannot tell which decision is live.
    expect(screen.getByText(/Attempt 1/)).toBeInTheDocument();
    expect(screen.getByText(/superseded/)).toBeInTheDocument();
    expect(screen.getByText(/Attempt 2/)).toBeInTheDocument();
    expect(screen.getByText(/current/)).toBeInTheDocument();
  });

  it('offers Revise to the requester of a rejected requisition, and calls the endpoint', async () => {
    signInAs(REQUESTER_ID, ['permission:requisitions:requisitions:update']);
    const rejected = requisition({
      status: 'Rejected',
      awaitingApprovalFrom: null,
      approvals: [
        step({
          round: 1, sequence: 1, label: 'HR', approverUserId: HR_ID,
          decision: 'Rejected', decidedAt: '2026-08-11T09:00:00Z',
          comment: 'Headcount not justified.',
        }),
      ],
    });
    apiMock.mockResolvedValue(rejected);

    renderPage();

    const button = await screen.findByRole('button', { name: /Revise this requisition/ });
    await userEvent.click(button);

    await waitFor(() => {
      expect(apiMock).toHaveBeenCalledWith('/requisitions/req-1/revise', { method: 'POST' });
    });
  });

  it('does not offer Revise on a terminal requisition', async () => {
    signInAs(REQUESTER_ID, ['permission:requisitions:requisitions:update']);
    apiMock.mockResolvedValue(requisition({ status: 'Approved', awaitingApprovalFrom: null }));

    renderPage();

    // Approved and Cancelled stay terminal — the backend 409s, so an affordance here would
    // only ever produce an error.
    await screen.findByText('Sales Ops Lead');
    expect(screen.queryByRole('button', { name: /Revise/ })).not.toBeInTheDocument();
  });

  it('does not offer Revise to someone who did not raise it', async () => {
    signInAs(FINANCE_ID, ['permission:requisitions:requisitions:update']);
    apiMock.mockResolvedValue(requisition({ status: 'Rejected', awaitingApprovalFrom: null }));

    renderPage();

    await screen.findByText('Sales Ops Lead');
    expect(screen.queryByRole('button', { name: /Revise/ })).not.toBeInTheDocument();
  });
});
