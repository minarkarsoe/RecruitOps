import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { OfferListItem, UserRole } from '@recruitops/types';
import { auth } from '../lib/auth';
import { OffersPage, rangeStart } from './OffersPage';

/*
 * The Offer Dashboard (`design/internal/offer-dashboard.html`, ADR-0030).
 *
 * What a reader must be able to trust here: a hidden salary reads as hidden rather than as an
 * empty or zero amount; a `Rejected` pill is never mistaken for a pipeline rejection; and the
 * one row waiting on the viewer says so. The server decides who sees what — this page must only
 * render it faithfully.
 */

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

function signInAs(role: UserRole) {
  auth.set({
    accessToken: 'token', expiresAtUtc: '2099-01-01T00:00:00Z', role,
    displayName: 'Test User', userId: 'usr-me', isSuperAdmin: false, permissions: [],
  });
}

function offer(overrides: Partial<OfferListItem> = {}): OfferListItem {
  return {
    id: 'ofr-1', reference: 'OFR-2026-0001', jobApplicationId: 'app-1',
    candidateName: 'Daw Hnin Wai', position: 'Sales Ops Lead',
    departmentId: 'dept-sales', departmentName: 'Sales', baseSalary: 800000,
    status: 'Draft', createdAt: '2026-10-20T09:00:00Z', sentAt: null,
    awaitingMe: false, waitingOn: null,
    ...overrides,
  };
}

function serve(items: OfferListItem[]) {
  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    if (path.startsWith('/offers?') || path === '/offers') {
      if (init?.method === 'POST') return Promise.resolve({ id: 'ofr-new' });
      return Promise.resolve(items);
    }
    if (path === '/departments') return Promise.resolve([{ id: 'dept-sales', name: 'Sales', code: 'SAL' }]);
    if (path === '/offers/eligible-applications') return Promise.resolve([{
      jobApplicationId: 'app-9', candidateName: 'U Kyaw Zin', position: 'Account Manager',
      departmentName: 'Sales', appliedAt: '2026-10-01T00:00:00Z',
    }]);
    if (path === '/offer-templates') return Promise.resolve([
      { id: null, name: 'Standard offer letter', body: '…', isActive: true },
      { id: 'tpl-old', name: 'Retired letter', body: '…', isActive: false },
    ]);
    return Promise.reject(new Error(`unexpected call: ${path}`));
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/offers']}>
      <Routes>
        <Route path="/offers" element={<OffersPage />} />
        <Route path="/offers/:id" element={<p>Offer page</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
});

describe('OffersPage', () => {
  it('says "Hidden" for a salary the viewer may not see, never a blank or a zero', async () => {
    signInAs('HiringManager');
    serve([offer({ baseSalary: null })]);
    renderPage();

    const row = (await screen.findByText('Daw Hnin Wai')).closest('tr')!;
    expect(within(row).getByText('Hidden')).toBeInTheDocument();
    expect(within(row).queryByText('0')).not.toBeInTheDocument();
  });

  it('shows a visible salary as an amount', async () => {
    signInAs('Recruiter');
    serve([offer({ baseSalary: 1250000 })]);
    renderPage();

    const row = (await screen.findByText('Daw Hnin Wai')).closest('tr')!;
    expect(within(row).getByText('1,250,000')).toBeInTheDocument();
  });

  it('marks the offer waiting on the viewer, and says who the others wait on', async () => {
    signInAs('Approver');
    serve([
      offer({ id: 'ofr-mine', candidateName: 'Mine', status: 'PendingApproval', awaitingMe: true, waitingOn: 'Finance Controller' }),
      offer({ id: 'ofr-theirs', candidateName: 'Theirs', status: 'PendingApproval', waitingOn: 'HR Director' }),
    ]);
    renderPage();

    const mine = (await screen.findByText('Mine')).closest('tr')!;
    expect(within(mine).getByText('Your decision')).toBeInTheDocument();
    expect(within(mine).getByRole('link', { name: 'Review' })).toHaveAttribute('href', '/offers/ofr-mine');
    const theirs = screen.getByText('Theirs').closest('tr')!;
    expect(within(theirs).getByText('With HR Director')).toBeInTheDocument();
    expect(within(theirs).getByRole('link', { name: 'Open' })).toBeInTheDocument();
  });

  it('says a Rejected offer was declined by the candidate — not a pipeline rejection', async () => {
    signInAs('Recruiter');
    serve([offer({ status: 'Rejected' })]);
    renderPage();

    const row = (await screen.findByText('Daw Hnin Wai')).closest('tr')!;
    expect(within(row).getByText('Declined by candidate')).toBeInTheDocument();
  });

  it('asks the server for the chosen group and period', async () => {
    signInAs('Recruiter');
    serve([]);
    renderPage();
    await screen.findByText('No offers here');

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Awaiting approval' }));
    await waitFor(() => expect(apiMock).toHaveBeenCalledWith(expect.stringMatching(/^\/offers\?group=awaiting&from=\d{4}-\d{2}-\d{2}$/)));

    await user.selectOptions(screen.getByRole('combobox', { name: 'Period' }), 'all');
    await waitFor(() => expect(apiMock).toHaveBeenLastCalledWith('/offers?group=awaiting'));
  });

  it('offers "Create offer" to recruitment staff only', async () => {
    signInAs('HiringManager');
    serve([]);
    const { unmount } = renderPage();
    await screen.findByText('No offers here');
    expect(screen.queryByRole('button', { name: 'Create offer' })).not.toBeInTheDocument();
    unmount();

    signInAs('Recruiter');
    renderPage();
    expect(await screen.findByRole('button', { name: 'Create offer' })).toBeInTheDocument();
  });

  it('creates an offer for a candidate at the Offer stage and opens it', async () => {
    signInAs('Recruiter');
    serve([]);
    renderPage();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Create offer' }));
    const dialog = await screen.findByRole('dialog');

    // A retired template is not offered for a new letter.
    expect(within(dialog).getByRole('option', { name: 'Standard offer letter' })).toBeInTheDocument();
    expect(within(dialog).queryByRole('option', { name: 'Retired letter' })).not.toBeInTheDocument();

    const create = within(dialog).getByRole('button', { name: 'Create offer' });
    expect(create).toBeDisabled(); // nobody chosen yet
    await user.click(await within(dialog).findByRole('radio', { name: /U Kyaw Zin/ }));
    await user.click(create);

    const post = apiMock.mock.calls.find(([p, init]) => p === '/offers' && (init as RequestInit | undefined)?.method === 'POST');
    expect(post, 'no POST /offers').toBeDefined();
    expect(JSON.parse((post![1] as RequestInit).body as string)).toEqual({ jobApplicationId: 'app-9', offerTemplateId: null });
    expect(await screen.findByText('Offer page')).toBeInTheDocument();
  });
});

describe('rangeStart', () => {
  const today = new Date('2026-10-21T12:00:00Z');

  it('starts each window where its label says', () => {
    expect(rangeStart('90d', today)).toBe('2026-07-23');
    expect(rangeStart('quarter', today)).toBe('2026-10-01');
    expect(rangeStart('year', today)).toBe('2026-01-01');
    expect(rangeStart('all', today)).toBeUndefined();
  });
});
