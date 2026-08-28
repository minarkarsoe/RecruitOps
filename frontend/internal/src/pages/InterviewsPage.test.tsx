import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { InterviewsPage } from './InterviewsPage';
import type { InterviewListItem } from '@recruitops/types';
import * as apiModule from '../lib/api';

/**
 * The interviews list (added 2026-08-28, built from `design/internal/interviews.html`).
 *
 * These pin the two things the screen can get wrong on its own, given a correct API:
 *
 *  1. **It must not render an evaluation.** The API deliberately sends none — panel *progress*
 *     is public, panel *opinions* are not (ADR-0017 §3) — and this page is the last place that
 *     could reintroduce one by reading a field off a widened response.
 *  2. **The filter must not restate the API's default.** "Upcoming" sends no `status` at all and
 *     lets the server apply "everything except Cancelled". Listing the statuses here would mean a
 *     status added later appears in one place and not the other.
 */

function row(over: Partial<InterviewListItem> = {}): InterviewListItem {
  return {
    id: 'iv-1',
    jobApplicationId: 'app-1',
    candidateName: 'Ma Ei Phyu Sin',
    jobPostingTitle: 'Senior Frontend Engineer',
    departmentId: 'dep-1',
    departmentName: 'Engineering',
    round: 2,
    scheduledStart: '2026-08-22T10:30:00Z',
    durationMinutes: 45,
    mode: 'Video',
    location: null,
    status: 'Scheduled',
    panelNames: ['Ma Su Su Hlaing', 'U Aung Kyaw Moe', 'Ma Thiri Kyaw'],
    panelSize: 3,
    submittedCount: 1,
    isOnPanel: true,
    myScorecardOutstanding: false,
    ...over,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <InterviewsPage />
    </MemoryRouter>
  );
}

/** Declared via a helper so the spy's type is inferred from `api` itself rather than annotated. */
const spyOnApi = () => vi.spyOn(apiModule, 'api');

describe('InterviewsPage', () => {
  let apiSpy: ReturnType<typeof spyOnApi>;

  beforeEach(() => {
    apiSpy = spyOnApi();
    apiSpy.mockResolvedValue([row()]);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders a round with its candidate, posting, round number and department', async () => {
    renderPage();

    expect(await screen.findByText('Ma Ei Phyu Sin')).toBeInTheDocument();
    expect(screen.getByText(/Senior Frontend Engineer/)).toBeInTheDocument();
    expect(screen.getByText(/Round 2/)).toBeInTheDocument();
    expect(screen.getByText('Engineering')).toBeInTheDocument();
  });

  it('shows panel progress as a count and never as a score', async () => {
    renderPage();

    expect(await screen.findByText('1 of 3 in')).toBeInTheDocument();
    // Nothing resembling an evaluation. If the API is ever widened, this is what fails first.
    for (const leak of [/strong ?yes/i, /recommendation/i, /rating/i, /\b[1-5]\s*\/\s*5\b/]) {
      expect(screen.queryByText(leak)).not.toBeInTheDocument();
    }
  });

  it('calls out the caller’s own outstanding scorecard, and links to it', async () => {
    apiSpy.mockResolvedValue([row({ myScorecardOutstanding: true })]);
    renderPage();

    expect(await screen.findByText('Yours is not in')).toBeInTheDocument();
    // The action changes with it: "Score", not "Open".
    expect(screen.getByRole('link', { name: 'Score' })).toHaveAttribute('href', '/interviews/iv-1');
  });

  it('shows the panel names when nothing is outstanding for the caller', async () => {
    renderPage();

    expect(await screen.findByText(/Ma Su Su Hlaing, U Aung Kyaw Moe, Ma Thiri Kyaw/)).toBeInTheDocument();
    expect(screen.queryByText('Yours is not in')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open' })).toBeInTheDocument();
  });

  it('sends NO status for the default view, leaving the rule to the API', async () => {
    renderPage();
    await screen.findByText('Ma Ei Phyu Sin');

    // Not `?status=Scheduled&status=Completed`. The API's default is "everything except
    // Cancelled"; restating it here is how the two drift when a status is added.
    expect(apiSpy).toHaveBeenCalledWith('/interviews');
  });

  it('asks for one status when a filter is chosen', async () => {
    renderPage();
    await screen.findByText('Ma Ei Phyu Sin');

    fireEvent.click(screen.getByRole('button', { name: 'Cancelled' }));

    await waitFor(() => expect(apiSpy).toHaveBeenLastCalledWith('/interviews?status=Cancelled'));
  });

  it('passes onlyMine through', async () => {
    renderPage();
    await screen.findByText('Ma Ei Phyu Sin');

    fireEvent.click(screen.getByLabelText('Only mine'));

    await waitFor(() => expect(apiSpy).toHaveBeenLastCalledWith('/interviews?onlyMine=true'));
  });

  it('teaches where interviews come from when the list is empty', async () => {
    apiSpy.mockResolvedValue([]);
    renderPage();

    // An interviewer with no rounds this week has done nothing wrong; the empty state must not
    // read like an error.
    const empty = await screen.findByText('No interviews here');
    // Scoped to the empty state's own card: the page header carries the same sentence, which is
    // deliberate — it is the answer to "where do these come from" whether or not the list is empty.
    const card = empty.parentElement as HTMLElement;
    expect(within(card).getByText(/scheduled against a candidate/i)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('surfaces a load failure instead of showing an empty list', async () => {
    apiSpy.mockRejectedValue(new Error('Service unavailable'));
    renderPage();

    // The distinction matters: "nothing scheduled" and "we could not ask" are different facts,
    // and rendering the empty state for both is how an outage looks like a quiet week.
    expect(await screen.findByText('Service unavailable')).toBeInTheDocument();
    expect(screen.queryByText('No interviews here')).not.toBeInTheDocument();
  });

  it('counts what it is showing', async () => {
    apiSpy.mockResolvedValue([row(), row({ id: 'iv-2' })]);
    renderPage();

    expect(await screen.findByText('2 interviews')).toBeInTheDocument();
  });

  it('uses the singular for one', async () => {
    renderPage();
    expect(await screen.findByText('1 interview')).toBeInTheDocument();
  });

  it('renders a cancelled round without colouring it as an error', async () => {
    apiSpy.mockResolvedValue([row({ status: 'Cancelled' })]);
    renderPage();

    const pill = await screen.findByText('Cancelled');
    // Neutral, like `Suppressed` on the delivery log: a cancelled round is usually the process
    // working, and colouring a correct outcome red teaches people to ignore the colour.
    expect(pill.className).not.toMatch(/critical/);
    expect(pill.className).toMatch(/ink-500|ink-600|canvas/);
  });

  it('renders every mode label the API can send', async () => {
    apiSpy.mockResolvedValue([
      row({ id: 'a', mode: 'OnSite' }),
      row({ id: 'b', mode: 'Video' }),
      row({ id: 'c', mode: 'Phone' }),
    ]);
    renderPage();

    const table = await screen.findByRole('table');
    expect(within(table).getByText(/Onsite/)).toBeInTheDocument();
    expect(within(table).getByText(/Online/)).toBeInTheDocument();
    expect(within(table).getByText(/Phone/)).toBeInTheDocument();
  });
});
