import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Preboarding, PreboardingDocument } from '@recruitops/types';
import { ApiError } from '../lib/api';
import { PreboardingPage } from './PreboardingPage';

/*
 * Pre-boarding review (`design/internal/preboarding-review.html`, ADR-0030 §5b).
 *
 * What a recruiter has to be able to rely on:
 *   - every decision sends the revision the page showed, so a document changed since is refused;
 *   - the account number stays masked until Reveal, and Reveal is the only way to read it;
 *   - a document opens one at a time, in a viewer, from the API — never as a thumbnail;
 *   - the handoff says what will go, to whom, and that it is held until the required set is approved.
 */

const { apiMock, blobMock } = vi.hoisted(() => ({ apiMock: vi.fn(), blobMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock, apiBlob: blobMock };
});

const BASE = '/offers/ofr-1/preboarding';

function doc(kind: PreboardingDocument['kind'], overrides: Partial<PreboardingDocument> = {}): PreboardingDocument {
  const labels: Record<PreboardingDocument['kind'], string> = {
    NrcFront: 'NRC — front', NrcBack: 'NRC — back', BankAccount: 'Bank account details',
    Education: 'Education certificates', Photo: 'Photo for the staff card',
  };
  return {
    kind, label: labels[kind], required: kind !== 'Photo', status: 'Missing', complete: false, revision: 0,
    changedAt: null, reviewedAt: null, reviewedByName: null, returnMessage: null, files: [],
    bank: kind === 'BankAccount' ? { bankName: null, accountName: null, accountNumberMasked: null } : null,
    ...overrides,
  };
}

const file = (id: string, contentType = 'image/jpeg') => ({ id, fileName: `${id}.jpg`, contentType, sizeBytes: 1_887_436, uploadedAt: '2026-10-23T09:00:00Z' });

function preboarding(docs: PreboardingDocument[] = [], overrides: Partial<Preboarding> = {}): Preboarding {
  const all = (['NrcFront', 'NrcBack', 'BankAccount', 'Education', 'Photo'] as const).map((k) => docs.find((d) => d.kind === k) ?? doc(k));
  return {
    offerId: 'ofr-1', reference: 'OFR-2026-0040', candidateId: 'cand-1', candidateName: 'Daw Nway Nway Oo',
    position: 'Treasury Dealer', departmentName: 'Finance', joiningDate: '2026-12-01', signedAt: '2026-10-23T08:00:00Z',
    windowOpen: true, documents: all, requiredDocuments: 4, requiredApproved: all.filter((d) => d.required && d.status === 'Approved').length,
    submittedAt: null, remindedAt: null, canRemind: true, handoffAt: null,
    handoff: [
      { team: 'IT', address: 'it-helpdesk@ymb.test', status: 'Held', sentAt: null, detail: null },
      { team: 'Admin', address: null, status: 'NotSetUp', sentAt: null, detail: null },
    ],
    ...overrides,
  };
}

function serve(initial: Preboarding, writes: Record<string, (init?: RequestInit) => Promise<unknown>> = {}) {
  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${path}`;
    if (writes[key]) return writes[key](init);
    if (key === `GET ${BASE}`) return Promise.resolve(initial);
    return Promise.reject(new Error(`unexpected call: ${key}`));
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={[BASE]}>
      <Routes><Route path="/offers/:id/preboarding" element={<PreboardingPage />} /></Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  apiMock.mockReset();
  blobMock.mockReset();
  URL.createObjectURL = vi.fn(() => 'blob:doc-1');
  URL.revokeObjectURL = vi.fn();
});

describe('PreboardingPage', () => {
  it('shows each document’s state and the required count, with no file content on the page', async () => {
    serve(preboarding([
      doc('NrcFront', { status: 'Approved', complete: true, reviewedByName: 'U Thura Win', reviewedAt: '2026-10-23T09:22:00Z', files: [file('f1')] }),
      doc('NrcBack', { status: 'Submitted', complete: true, revision: 2, files: [file('f2')] }),
    ]));
    renderPage();

    expect(await screen.findByText('1 of 4 approved')).toBeInTheDocument();
    expect(within(screen.getByTestId('doc-NrcFront')).getByText(/Approved by U Thura Win/)).toBeInTheDocument();
    expect(within(screen.getByTestId('doc-NrcBack')).getByText('Awaiting your review')).toBeInTheDocument();
    expect(within(screen.getByTestId('doc-Education')).getByText('Not uploaded')).toBeInTheDocument();
    expect(within(screen.getByTestId('doc-Photo')).getByText('Optional')).toBeInTheDocument();
    // No thumbnails: nothing is fetched until a document is opened.
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(blobMock).not.toHaveBeenCalled();
  });

  it('approves with the revision it showed, and shows the refusal when the candidate changed it since', async () => {
    let body: unknown;
    serve(preboarding([doc('NrcBack', { status: 'Submitted', complete: true, revision: 2, files: [file('f2')] })]), {
      [`POST ${BASE}/NrcBack/approve`]: (init) => {
        body = JSON.parse(String(init?.body));
        return Promise.reject(new ApiError(409, 'The candidate changed this document after the page was loaded. Reload to see what they sent.'));
      },
    });
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: 'Approve NRC — back' }));

    expect(body).toEqual({ revision: 2 });
    expect(await screen.findByRole('alert')).toHaveTextContent('changed this document after the page was loaded');
    // …and it reloads, so what is shown is what is there now.
    await waitFor(() => expect(apiMock.mock.calls.filter(([p, i]) => p === BASE && !(i as RequestInit | undefined)?.method).length).toBe(2));
  });

  it('sends a document back with the recruiter’s own words', async () => {
    let body: unknown;
    const before = preboarding([doc('Education', { status: 'Submitted', complete: true, revision: 1, files: [file('e1')] })]);
    const after = preboarding([doc('Education', {
      status: 'Returned', complete: true, revision: 2, files: [file('e1')], returnMessage: 'ဓာတ်ပုံ မှုန်ဝါး\nPlease retake it.', reviewedAt: '2026-10-23T10:00:00Z',
    })]);
    serve(before, { [`POST ${BASE}/Education/return`]: (init) => { body = JSON.parse(String(init?.body)); return Promise.resolve(after); } });
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Reject Education certificates' }));
    const send = screen.getByRole('button', { name: 'Send request' });
    expect(send).toBeDisabled();
    await userEvent.type(screen.getByLabelText('What the candidate will be told, in your words'), 'ဓာတ်ပုံ မှုန်ဝါး{enter}Please retake it.');
    await userEvent.click(send);

    expect(body).toEqual({ revision: 1, message: 'ဓာတ်ပုံ မှုန်ဝါး\nPlease retake it.' });
    const row = screen.getByTestId('doc-Education');
    expect(await within(row).findByText('Re-submission needed')).toBeInTheDocument();
    expect(within(row).getByText(/Please retake it\./)).toBeInTheDocument();
  });

  it('keeps the account number masked until Reveal, which asks the API', async () => {
    serve(preboarding([doc('BankAccount', {
      status: 'Submitted', complete: true, revision: 3, files: [file('b1')],
      bank: { bankName: 'KBZ Bank', accountName: 'NWAY NWAY OO', accountNumberMasked: '•••• 4471' },
    })]), { [`POST ${BASE}/bank/reveal`]: () => Promise.resolve({ accountNumber: '0012345678904471' }) });
    renderPage();

    expect(await screen.findByTestId('account-number')).toHaveTextContent('•••• 4471');
    expect(screen.getByText('Revealing is recorded in the audit log with your name and the time.')).toBeInTheDocument();
    expect(document.body.textContent).not.toContain('0012345678904471');
    await userEvent.click(screen.getByRole('button', { name: 'Reveal' }));
    expect(await screen.findByText('0012345678904471')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Hide' }));
    expect(screen.getByTestId('account-number')).toHaveTextContent('•••• 4471');
  });

  it('opens one document at a time in a viewer, fetched from the API', async () => {
    blobMock.mockResolvedValue(new Blob(['x'], { type: 'image/jpeg' }));
    serve(preboarding([doc('NrcFront', { status: 'Submitted', complete: true, revision: 1, files: [file('f1')] })]));
    renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Open document: NRC — front' }));
    expect(blobMock).toHaveBeenCalledWith(`${BASE}/files/f1`);
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Opening this was recorded in the audit log.')).toBeInTheDocument();
    expect(await within(dialog).findByRole('img', { name: 'NRC — front' })).toHaveAttribute('src', 'blob:doc-1');
  });

  it('shows a PDF in a frame, not as an image', async () => {
    blobMock.mockResolvedValue(new Blob(['%PDF'], { type: 'application/pdf' }));
    serve(preboarding([doc('Education', { status: 'Submitted', complete: true, revision: 1, files: [file('e1', 'application/pdf')] })]));
    renderPage();
    await userEvent.click(await screen.findByRole('button', { name: 'Open document: Education certificates' }));
    expect(await screen.findByTitle('Education certificates')).toHaveAttribute('src', 'blob:doc-1');
  });

  it('says the handoff is held, where each email goes, and when a team has no address', async () => {
    serve(preboarding());
    renderPage();
    const it = await screen.findByTestId('handoff-IT');
    expect(within(it).getByText('Held')).toBeInTheDocument();
    expect(within(it).getByText('it-helpdesk@ymb.test')).toBeInTheDocument();
    expect(within(it).getByText(/No salary, no NRC, no bank details/)).toBeInTheDocument();
    const admin = screen.getByTestId('handoff-Admin');
    expect(within(admin).getByText('Not set up')).toBeInTheDocument();
    expect(within(admin).getByText(/No Admin address is set/)).toBeInTheDocument();
    expect(screen.getByText('Both are held until every required document is approved')).toBeInTheDocument();
    expect(screen.queryByText(/photo has not been approved yet/)).not.toBeInTheDocument();
  });

  it('warns while an uploaded photo would miss the Admin email', async () => {
    serve(preboarding([doc('Photo', { status: 'Submitted', complete: true, revision: 1, files: [file('p1', 'image/png')] })]));
    renderPage();
    expect(await within(await screen.findByTestId('handoff-Admin')).findByText(/photo has not been approved yet/)).toBeInTheDocument();
  });

  it('reminds the candidate of what is missing, and offers it only when the API allows', async () => {
    let reminded = false;
    serve(preboarding(), {
      [`POST ${BASE}/remind`]: () => { reminded = true; return Promise.resolve(preboarding([], { canRemind: false, remindedAt: '2026-10-23T11:00:00Z' })); },
    });
    renderPage();
    const remind = within(await screen.findByTestId('doc-Education')).getByRole('button', { name: 'Remind' });
    await userEvent.click(remind);
    expect(reminded).toBe(true);
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Remind' })).not.toBeInTheDocument());
    expect(screen.getByText(/reminded/)).toBeInTheDocument();
  });

  it('says so when the API refuses a non-recruiter', async () => {
    apiMock.mockRejectedValue(new ApiError(403, 'You do not have access to this.'));
    renderPage();
    expect(await screen.findByRole('alert')).toHaveTextContent('You do not have access to this.');
  });
});
