import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { DeliveryLogEntry, PagedResult } from '@recruitops/types';
import { DeliveryLogPage } from './DeliveryLogPage';
import { api } from '../lib/api';

vi.mock('../lib/api', () => ({ api: vi.fn() }));

const apiMock = api as unknown as ReturnType<typeof vi.fn>;

function entry(over: Partial<DeliveryLogEntry> = {}): DeliveryLogEntry {
  return {
    id: over.id ?? 'msg-1',
    kind: 'InterviewInvitation',
    kindLabel: 'Interview invitation',
    channel: 'Email',
    recipient: 'candidate@example.test',
    candidateName: 'Ma Thiri Aung',
    subjectType: 'Interview',
    subjectId: 'int-1',
    status: 'Sent',
    attempts: 1,
    nextAttemptAt: null,
    lastError: null,
    sentAt: '2026-08-25T09:48:00Z',
    createdAt: '2026-08-25T09:47:00Z',
    ...over,
  };
}

function page(items: DeliveryLogEntry[]): PagedResult<DeliveryLogEntry> {
  return { items, page: 1, pageSize: 25, totalCount: items.length, totalPages: 1 };
}

/**
 * Assertions about a row are scoped to the table on purpose.
 *
 * The filter chips and the status pills deliberately use the *same words* — "Failed", "Delivered",
 * "Not sent" — because a filter that says "Delivered" and a pill that says "Sent" makes the user
 * translate between two vocabularies for one idea. The cost is that a bare `getByText('Delivered')`
 * is ambiguous, which is the test's problem to solve, not the screen's.
 */
function inTable() {
  return within(screen.getByRole('table'));
}

describe('DeliveryLogPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows what was sent, to whom, and whether it arrived', async () => {
    apiMock.mockResolvedValue(page([entry()]));

    render(<DeliveryLogPage />);

    expect(await screen.findByText('Ma Thiri Aung')).toBeInTheDocument();
    expect(inTable().getByText('candidate@example.test')).toBeInTheDocument();
    expect(inTable().getByText('Interview invitation')).toBeInTheDocument();
    expect(inTable().getByText('Email')).toBeInTheDocument();
    // `Sent` reads as "Delivered" — the recruiter cares that it arrived, not about the enum.
    expect(inTable().getByText('Delivered')).toBeInTheDocument();
  });

  it('a failure row carries the reason, not just the word Failed', async () => {
    // The design's rule: "'Failed' alone makes a recruiter open a support ticket." The reason is
    // written by the handler, for a human, and this screen must not drop it.
    const reason =
      'There is no email address on record for this candidate, so no invitation could be sent.';
    apiMock.mockResolvedValue(page([entry({ status: 'Failed', lastError: reason, attempts: 3 })]));

    render(<DeliveryLogPage />);

    await screen.findByText('Ma Thiri Aung');
    expect(inTable().getByText('Failed')).toBeInTheDocument();
    expect(inTable().getByText(reason)).toBeInTheDocument();
  });

  it('a suppressed message is neutral, not an error', async () => {
    // ADR-0026 made Suppressed a first-class status precisely so it would not be coloured red.
    // An opt-out honoured is the system working. Colouring it as a failure is how recruiters
    // learn to ignore the colour that means something.
    apiMock.mockResolvedValue(
      page([entry({ status: 'Suppressed', lastError: 'The round was cancelled before the invitation went out.' })])
    );

    render(<DeliveryLogPage />);

    await screen.findByText('Ma Thiri Aung');
    const pill = inTable().getByText('Not sent');
    expect(pill.className).not.toContain('critical');
    expect(pill.className).toContain('text-ink-600');

    const reason = inTable().getByText(/The round was cancelled/);
    expect(reason.className).not.toContain('critical');
  });

  it('does not promise a retry on a message that has given up', async () => {
    apiMock.mockResolvedValue(
      page([entry({ status: 'Failed', attempts: 3, nextAttemptAt: null, lastError: 'The relay refused it.' })])
    );

    render(<DeliveryLogPage />);
    await screen.findByText('Ma Thiri Aung');

    expect(inTable().getByText('Failed')).toBeInTheDocument();
    expect(screen.queryByText(/Trying again automatically/)).not.toBeInTheDocument();
  });

  it('says a still-queued message is being retried', async () => {
    apiMock.mockResolvedValue(
      page([entry({ status: 'Pending', attempts: 2, nextAttemptAt: '2026-08-25T10:00:00Z', lastError: null })])
    );

    render(<DeliveryLogPage />);

    await screen.findByText('Ma Thiri Aung');
    expect(inTable().getByText('Waiting')).toBeInTheDocument();
    expect(inTable().getByText(/Attempt 2 did not go through/)).toBeInTheDocument();
  });

  it('filters by result and asks the server, not the browser', async () => {
    // The filter has to reach the API: filtering client-side would only ever search the first
    // page, so "show me the failures" would quietly miss every failure older than 25 messages.
    const user = userEvent.setup();
    apiMock.mockResolvedValue(page([entry()]));

    render(<DeliveryLogPage />);
    await screen.findByText('Ma Thiri Aung');
    expect(apiMock).toHaveBeenLastCalledWith('/delivery');

    await user.click(screen.getByRole('button', { name: 'Failed' }));

    await waitFor(() => expect(apiMock).toHaveBeenLastCalledWith('/delivery?status=Failed'));
  });

  it('announces the selected filter rather than only colouring it', async () => {
    apiMock.mockResolvedValue(page([entry()]));
    const user = userEvent.setup();

    render(<DeliveryLogPage />);
    await screen.findByText('Ma Thiri Aung');

    expect(screen.getByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('button', { name: 'Failed' }));

    expect(screen.getByRole('button', { name: 'Failed' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('an empty log explains where the first message will come from', async () => {
    apiMock.mockResolvedValue(page([]));

    render(<DeliveryLogPage />);

    expect(await screen.findByText('Nothing has been sent yet')).toBeInTheDocument();
    expect(screen.getByText(/invitation sent when you schedule an interview/)).toBeInTheDocument();
  });

  it('surfaces a load failure instead of showing an empty log', async () => {
    // An empty table and a broken request look identical, and one of them means "nobody was
    // told anything" — which is the exact thing this screen exists to make visible.
    apiMock.mockRejectedValue(new Error('Request failed (500)'));

    render(<DeliveryLogPage />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Request failed (500)');
    expect(screen.queryByText('Nothing has been sent yet')).not.toBeInTheDocument();
  });

  it('renders a candidate with no resolved name without crashing', async () => {
    apiMock.mockResolvedValue(
      page([entry({ candidateName: null, kind: 'ScheduledReport', kindLabel: 'Scheduled report' })])
    );

    render(<DeliveryLogPage />);

    expect(await screen.findByText('Scheduled report')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
  });
});
