import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { JobPostingDetailPage } from './JobPostingDetailPage';
import type { JobPostingDetail, PipelineItem } from '@recruitops/types';
import * as apiModule from '../lib/api';
import { auth } from '../lib/auth';

/**
 * The pipeline board on the posting page (wired 2026-08-29 from `design/internal/board.html`).
 *
 * `PipelineKanbanBoard` and `CandidateSlideOver` had been written, tested and left with no
 * importer — 49 tests passing against components no browser ever loaded. These are the tests
 * that were missing: not "does the board render" but "is the board *reachable*, and does the
 * flow through it still do everything the list it replaced could do".
 *
 * The fourth case is the one that matters most. The drawer's own Interviews tab can only list
 * rounds; `ApplicationDebrief` is what schedules them. Wiring the drawer without passing the
 * debrief in would have produced a screen that looks finished and silently drops interview
 * scheduling out of the pipeline.
 */

// Typed, not cast. `as JobPostingDetail` compiled a fixture with three faults the cast hid:
// `publishedAt` (the field is `postedAt`), no `applicationCount`, and `status: 'Published'` —
// which is not a JobStatus at all; the union is Draft | Live | Closed. That is the exact failure
// mode behind this session's three broken AI contracts: a fixture agreeing with itself instead
// of with the type. Casts in test fixtures are how a contract drifts without anything going red.
const posting: JobPostingDetail = {
  id: 'post-1',
  requisitionId: 'req-1',
  departmentId: 'dep-1',
  departmentName: 'Credit Risk',
  title: 'Senior Credit Analyst',
  description: 'Assess and monitor credit exposure.',
  location: 'Yangon',
  employmentType: 'FullTime',
  headcount: 2,
  salaryMin: null,
  salaryMax: null,
  showSalary: false,
  status: 'Live',
  publicToken: 'tok-1',
  postedAt: '2026-08-01T00:00:00Z',
  closedAt: null,
  applicationCount: 1,
  applicationFormFieldsJson: null,
};

function candidate(over: Partial<PipelineItem> = {}): PipelineItem {
  return {
    id: 'app-1',
    candidateId: 'cand-1',
    candidateName: 'Daw Hnin Yu',
    email: 'hnin.yu@example.com',
    phone: '+95 9770001111',
    status: 'Screening',
    source: 'Direct',
    appliedAt: '2026-08-10T09:00:00Z',
    coverNote: null,
    customFieldsJson: null,
    ...over,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/jobpostings/post-1']}>
      <Routes>
        <Route path="/jobpostings/:id" element={<JobPostingDetailPage />} />
      </Routes>
    </MemoryRouter>
  );
}

/** Routes each GET to its own payload so the board and the drawer can be driven independently. */
function mockApi(pipeline: PipelineItem[]) {
  return vi.spyOn(apiModule, 'api').mockImplementation((async (path: string) => {
    if (path === '/jobpostings/post-1') return posting;
    if (path === '/jobpostings/post-1/pipeline') return pipeline;
    if (path.endsWith('/history')) return [];
    if (path.endsWith('/interviews')) return [];
    if (path === '/users/selectable') return [];
    return [];
  }) as unknown as typeof apiModule.api);
}

const FULL_PERMISSIONS = [
  'permission:applications:applications:read',
  'permission:applications:applications:move_stage',
  'permission:postings:postings:read',
];

function signIn(permissions: string[]) {
  auth.set({
    accessToken: 't',
    refreshToken: 'r',
    expiresAtUtc: '2099-01-01T00:00:00Z',
    refreshTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    userId: 'u-1',
    displayName: 'Ma Su Su Hlaing',
    role: 'Recruiter',
    permissions,
  } as never);
}

describe('JobPostingDetailPage — the pipeline board', () => {
  beforeEach(() => {
    localStorage.clear();
    signIn(FULL_PERMISSIONS);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('renders the board grouped by stage, not a flat list', async () => {
    mockApi([
      candidate({ id: 'app-1', candidateName: 'Daw Hnin Yu', status: 'Screening' }),
      candidate({ id: 'app-2', candidateName: 'U Kyaw Swar', status: 'Offer' }),
    ]);
    renderPage();

    expect(await screen.findByText('Daw Hnin Yu')).toBeInTheDocument();
    expect(screen.getByText('U Kyaw Swar')).toBeInTheDocument();
    // The stage columns are the board's whole point: "where is everyone?" answered by shape
    // rather than reconstructed by reading a status pill on every row.
    //
    // getAllByText, not getByText: each stage name also appears inside every card's "move to"
    // select, so a single-match assertion fails on the options rather than on the columns.
    for (const stage of ['Sourced', 'Applied', 'Screening', 'Shortlisted', 'Interview', 'Offer', 'Hired']) {
      expect(screen.getAllByText(stage).length).toBeGreaterThan(0);
    }
  });

  it('opens the candidate drawer from a card — the board is actually reachable', async () => {
    const user = userEvent.setup();
    mockApi([candidate()]);
    renderPage();

    await user.click(await screen.findByText('Daw Hnin Yu'));

    // The drawer's tab strip is what proves the slide-over mounted rather than the card merely
    // being clickable.
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Overview/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /AI Insights/i })).toBeInTheDocument();
    });
  });

  it('keeps interview SCHEDULING in the drawer, not just a read-only list', async () => {
    const user = userEvent.setup();
    mockApi([candidate()]);
    renderPage();

    await user.click(await screen.findByText('Daw Hnin Yu'));
    await user.click(await screen.findByRole('button', { name: /Interviews/i }));

    // ApplicationDebrief, passed through `interviewsSlot`. Without it the tab would render the
    // drawer's built-in list and say "No interview rounds scheduled yet" with no way to schedule
    // one — a finished-looking screen that had quietly lost a step of the workflow.
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Schedule/i })).toBeInTheDocument();
    });
  });

  it('does not put the note composer on two tabs', async () => {
    const user = userEvent.setup();
    mockApi([candidate()]);
    renderPage();

    await user.click(await screen.findByText('Daw Hnin Yu'));
    await user.click(await screen.findByRole('button', { name: /Interviews/i }));

    // Found by opening the screen, not by a test: `ApplicationDebrief` bundles the note thread
    // under its rounds, and the drawer already has a Notes tab, so wiring it in put the same
    // composer on both. `showNotes={false}` is what keeps each thing in one place — and the
    // Schedule assertion above passed happily while the duplicate was there, which is the
    // reminder that "the test is green" and "the screen is right" are different claims.
    expect(screen.queryByPlaceholderText(/Add a note/i)).toBeNull();

    await user.click(screen.getByRole('button', { name: /Notes & Debrief/i }));
    expect(await screen.findByPlaceholderText(/Add a note/i)).toBeInTheDocument();
  });

  it('withholds stage controls from a user without move_stage', async () => {
    signIn(['permission:applications:applications:read']);
    mockApi([candidate()]);
    renderPage();

    expect(await screen.findByText('Daw Hnin Yu')).toBeInTheDocument();
    // A read-only board, not controls that 403. `onMoveStage` is simply not passed.
    expect(screen.queryByRole('combobox')).toBeNull();
  });
});
