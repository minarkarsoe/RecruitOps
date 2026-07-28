import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { InterviewScorecards, MyScorecard, SaveScorecardRequest } from '@recruitops/types';
import { ApiError } from '../lib/api';
import { InterviewDetailPage } from './InterviewDetailPage';
import { interview, myScorecard, panel, scorecard } from '../test/fixtures';

/*
 * The blind rule (ADR-0017 §3) as the reader experiences it.
 *
 * Three audiences share this page, and picking the wrong rendering does not look like a
 * view bug — it looks like the *rule* is wrong, which is the expensive kind of wrong. So
 * each of the three is pinned by what the page actually says.
 */

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

/** Route `api()` by path, so a test only states the responses it cares about. */
function serve(
  { mine, scorecards }: { mine: MyScorecard | null; scorecards: InterviewScorecards },
) {
  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    if (init?.method) return Promise.resolve(undefined);
    if (path === '/interviews/iv-1') return Promise.resolve(interview());
    if (path === '/interviews/iv-1/scorecards') return Promise.resolve(scorecards);
    if (path === '/interviews/iv-1/scorecard') {
      // 404 here is an ordinary state — "you were not on this panel" — not an error.
      return mine ? Promise.resolve(mine) : Promise.reject(new ApiError(404, 'Not found'));
    }
    if (path === '/applications/app-1/notes') return Promise.resolve([]);
    throw new Error(`unexpected call: ${path}`);
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/interviews/iv-1']}>
      <Routes>
        <Route path="/interviews/:id" element={<InterviewDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('the blind panel view', () => {
  it('names what is withheld when a panel member has not submitted', async () => {
    serve({
      mine: myScorecard(),
      scorecards: panel({ hiddenCount: 2, blindedUntilYouSubmit: true }),
    });
    renderPage();

    expect(await screen.findByText(/2 evaluations are waiting for yours/)).toBeInTheDocument();
    // "No evaluations submitted yet" would read as "nobody has written one", which is the
    // opposite of what is true — two exist and are being withheld.
    expect(screen.queryByText(/No evaluations submitted yet/)).not.toBeInTheDocument();
  });

  it('says so plainly when blinded but there is nothing to withhold', async () => {
    serve({
      mine: myScorecard(),
      scorecards: panel({ hiddenCount: 0, blindedUntilYouSubmit: true }),
    });
    renderPage();

    expect(await screen.findByText(/Nobody else has submitted yet/)).toBeInTheDocument();
    // The warning banner counts something. Rendering it at zero would announce
    // "0 evaluations are waiting for yours" — technically true, and unreadable.
    expect(screen.queryByText(/waiting for yours/)).not.toBeInTheDocument();
    expect(screen.queryByText(/No evaluations submitted yet/)).not.toBeInTheDocument();
  });

  it('shows a recruiter who was not in the room the submitted scores, with no blind notice', async () => {
    serve({
      mine: null,
      scorecards: panel({ visible: [scorecard()], blindedUntilYouSubmit: false }),
    });
    renderPage();

    // The summary, not the name — the name is also on the panel roster, where it says only
    // that they were in the room.
    expect(await screen.findByText('Solid.')).toBeInTheDocument();
    expect(screen.getByText(/Yes · submitted/)).toBeInTheDocument();
    expect(screen.queryByText(/waiting for yours/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Nobody else has submitted yet/)).not.toBeInTheDocument();
    // Not on the panel means no form at all — the API refuses a scorecard from someone who
    // was not there, so offering one would be a button that can only fail.
    expect(screen.queryByText('Your evaluation')).not.toBeInTheDocument();
  });

  it('unlocks everything once the caller has submitted', async () => {
    serve({
      mine: myScorecard({ scorecard: scorecard({ interviewerUserId: 'u-me', interviewerName: 'Aye Aye' }) }),
      scorecards: panel({ visible: [scorecard()], hiddenCount: 0, blindedUntilYouSubmit: false }),
    });
    renderPage();

    expect(await screen.findByText(/cannot be changed afterwards/)).toBeInTheDocument();
    expect(screen.queryByText(/waiting for yours/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Submit evaluation/ })).not.toBeInTheDocument();
  });

  it('explains itself when no template resolves, instead of rendering an empty form', async () => {
    serve({ mine: myScorecard({ criteria: [] }), scorecards: panel() });
    renderPage();

    expect(await screen.findByText(/No scorecard template applies/)).toBeInTheDocument();
  });
});

describe('saving a draft', () => {
  it('omits an untouched criterion from the payload', async () => {
    serve({ mine: myScorecard(), scorecards: panel() });
    renderPage();

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: '4' }));
    await user.click(screen.getByRole('button', { name: 'Save draft' }));

    const put = apiMock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'PUT');
    expect(put).toBeDefined();

    const body = JSON.parse((put![1] as RequestInit).body as string) as SaveScorecardRequest;
    // One answer, not three nulls: the API validates every answer in the payload on a draft
    // save, so an untouched Rating sent as null would reject the lot.
    expect(body.answers).toEqual([
      { scorecardCriterionId: 'c-rating', rating: 4, yesNo: null, comment: null },
    ]);
  });

  it('will not submit until every required criterion is answered', async () => {
    serve({ mine: myScorecard(), scorecards: panel() });
    renderPage();

    const submit = await screen.findByRole('button', { name: 'Submit evaluation' });
    expect(submit).toBeDisabled();
    expect(screen.getByText(/Still needed to submit: Technical depth/)).toBeInTheDocument();
  });
});
