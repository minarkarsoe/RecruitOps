import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHook, act } from '@testing-library/react';
import type { Interview, InterviewScorecards, MyScorecard } from '@recruitops/types';
import { auth } from '../../lib/auth';
import { BlindScorecardDrawer } from './BlindScorecardDrawer';
import { useInterviews } from './useInterviews';

// The scorecard submit control is permission-gated. These suites ran with no session and
// still rendered it, because hasPermission() granted a null session everything.
beforeEach(() => {
  sessionStorage.clear();
  auth.set({
    accessToken: 'token-interviewer',
    expiresAtUtc: '2099-01-01T00:00:00Z',
    role: 'Recruiter',
    displayName: 'Panel Interviewer',
    userId: 'usr-interviewer',
    isSuperAdmin: false,
    permissions: ['permission:scorecards:scorecards:submit'],
  });
});

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../lib/api')>('../../lib/api');
  return { ...actual, api: apiMock };
});

const mockInterview: Interview = {
  id: 'iv-101',
  jobApplicationId: 'app-1',
  round: 1,
  scheduledStart: '2026-08-03T10:00:00Z',
  durationMinutes: 45,
  mode: 'Video',
  location: 'https://meet.example.com/iv-101',
  status: 'Scheduled',
  agenda: 'Technical system design interview',
  cancellationReason: null,
  scorecardTemplateId: 'tpl-1',
  scorecardTemplateName: 'Engineering Standard Scorecard',
  participants: [
    {
      userId: 'user-me',
      displayName: 'Alice Lead',
      email: 'alice@example.com',
      isLead: true,
      hasSubmittedScorecard: false,
    },
    {
      userId: 'user-2',
      displayName: 'Bob Member',
      email: 'bob@example.com',
      isLead: false,
      hasSubmittedScorecard: true,
    },
  ],
};

const mockScorecards: InterviewScorecards = {
  interviewId: 'iv-101',
  visible: [],
  hiddenCount: 1,
  blindedUntilYouSubmit: true,
};

const mockMyScorecard: MyScorecard = {
  interviewId: 'iv-101',
  scorecardTemplateId: 'tpl-1',
  scorecardTemplateName: 'Engineering Standard Scorecard',
  criteria: [
    {
      id: 'c-1',
      sequence: 1,
      label: 'Technical Depth',
      guidance: 'Evaluate system design and problem solving',
      type: 'Rating',
      isRequired: true,
    },
  ],
  scorecard: null,
};

describe('BlindScorecardDrawer', () => {
  it('renders split view with left evaluation form and right blind panel notice when open', async () => {
    apiMock.mockImplementation((path: string) => {
      if (path === '/interviews/iv-101') return Promise.resolve(mockInterview);
      if (path === '/interviews/iv-101/scorecards') return Promise.resolve(mockScorecards);
      if (path === '/interviews/iv-101/scorecard') return Promise.resolve(mockMyScorecard);
      if (path === '/applications/app-1/notes') return Promise.resolve([]);
      throw new Error(`Unexpected endpoint: ${path}`);
    });

    render(
      <BlindScorecardDrawer
        interviewId="iv-101"
        isOpen={true}
        onClose={vi.fn()}
      />
    );

    expect(await screen.findByText('Round 1 Scorecard Evaluation')).toBeInTheDocument();
    expect(screen.getByText('Technical Depth')).toBeInTheDocument();
    expect(screen.getByText(/1 evaluation is waiting for yours/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save Draft' })).toBeInTheDocument();
  });

  it('allows clicking rating button and selecting recommendation', async () => {
    apiMock.mockImplementation((path: string) => {
      if (path === '/interviews/iv-101') return Promise.resolve(mockInterview);
      if (path === '/interviews/iv-101/scorecards') return Promise.resolve(mockScorecards);
      if (path === '/interviews/iv-101/scorecard') return Promise.resolve(mockMyScorecard);
      if (path === '/applications/app-1/notes') return Promise.resolve([]);
      throw new Error(`Unexpected endpoint: ${path}`);
    });

    const user = userEvent.setup();

    render(
      <BlindScorecardDrawer
        interviewId="iv-101"
        isOpen={true}
        onClose={vi.fn()}
      />
    );

    const rating4 = await screen.findByRole('button', { name: '4' });
    await user.click(rating4);
    expect(rating4).toHaveAttribute('aria-pressed', 'true');

    const selectRec = screen.getByRole('combobox', { name: /Overall Recommendation/i });
    await user.selectOptions(selectRec, 'StrongYes');
    expect(selectRec).toHaveValue('StrongYes');
  });
});

describe('useInterviews hook', () => {
  it('loads interview data and manages scorecard draft state', async () => {
    apiMock.mockImplementation((path: string) => {
      if (path === '/interviews/iv-101') return Promise.resolve(mockInterview);
      if (path === '/interviews/iv-101/scorecards') return Promise.resolve(mockScorecards);
      if (path === '/interviews/iv-101/scorecard') return Promise.resolve(mockMyScorecard);
      throw new Error(`Unexpected path: ${path}`);
    });

    const { result } = renderHook(() => useInterviews({ interviewId: 'iv-101' }));

    await act(async () => {
      await result.current.loadInterviewData();
    });

    expect(result.current.interview?.id).toBe('iv-101');
    expect(result.current.mine?.criteria).toHaveLength(1);
    expect(result.current.panel?.hiddenCount).toBe(1);
  });
});
