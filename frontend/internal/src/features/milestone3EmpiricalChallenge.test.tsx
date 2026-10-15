import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Interview, PipelineItem, StageHistoryItem } from '@recruitops/types';
import { CandidateSlideOver } from './pipeline/CandidateSlideOver';
import { PipelineKanbanBoard, PIPELINE_STAGES } from './pipeline/PipelineKanbanBoard';

const apiMock = vi.fn();

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: (...args: any[]) => apiMock(...args) };
});

vi.mock('../lib/auth', () => ({
  auth: {
    get: () => ({
      userId: 'user-me',
      role: 'Admin',
      tenantId: 'tenant-1',
      permissions: [
        'permission:scorecards:scorecards:submit',
        'permission:requisitions:requisitions:approve',
        'permission:requisitions:requisitions:delete',
        'permission:requisitions:requisitions:update',
      ],
    }),
  },
  hasPermission: () => true,
}));

const mockCandidate: PipelineItem = {
  id: 'app-100',
  candidateId: 'cand-100',
  candidateName: 'Jane Doe',
  email: 'jane.doe@example.com',
  phone: '+1 555 9999',
  status: 'Interview',
  source: 'LinkedIn',
  appliedAt: '2026-08-01T10:00:00Z',
  coverNote: 'Proven track record in TypeScript and React UI architecture.',
  customFieldsJson: JSON.stringify({ experienceYears: '7', noticePeriod: '30 days' }),
};

const mockStageHistory: StageHistoryItem[] = [
  {
    fromStatus: 'Applied',
    toStatus: 'Screening',
    changedAt: '2026-08-01T11:00:00Z',
    changedByName: 'Recruiter Bob',
    note: 'Initial phone screen passed',
  },
  {
    fromStatus: 'Screening',
    toStatus: 'Interview',
    changedAt: '2026-08-02T09:00:00Z',
    changedByName: 'HM Sarah',
    note: 'Shortlisted for tech round',
  },
];

const mockInterviews: Interview[] = [
  {
    id: 'iv-201',
    jobApplicationId: 'app-100',
    round: 1,
    scheduledStart: '2026-08-03T14:00:00Z',
    durationMinutes: 60,
    mode: 'Video',
    location: 'https://meet.google.com/abc-defg-hij',
    status: 'Scheduled',
    agenda: 'Architecture and System Design',
    cancellationReason: null,
    scorecardTemplateId: 'tpl-1',
    scorecardTemplateName: 'Senior Eng Scorecard',
    participants: [
      {
        userId: 'user-me',
        displayName: 'Jane Staff Eng',
        email: 'jane.staff@example.com',
        isLead: true,
        hasSubmittedScorecard: false,
      },
      {
        userId: 'user-other',
        displayName: 'Dave Tech Lead',
        email: 'dave@example.com',
        isLead: false,
        hasSubmittedScorecard: true,
      },
    ],
  },
];

describe('Milestone 3 Empirical Challenge Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('1. Candidate 360 Profile Drawer & Tab Interactions', () => {
    it('opens candidate 360 drawer without page refresh and displays candidate header info', () => {
      const onClose = vi.fn();
      render(
        <CandidateSlideOver
          candidate={mockCandidate}
          isOpen={true}
          onClose={onClose}
          stageHistory={mockStageHistory}
          interviews={mockInterviews}
        />
      );

      expect(screen.getAllByText('Jane Doe').length).toBeGreaterThan(0);
      expect(screen.getByText('jane.doe@example.com · +1 555 9999 · Applied 8/1/2026')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Overview/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /CV Viewer/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Stage History/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Interviews/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Notes & Debrief/i })).toBeInTheDocument();
    });

    it('switches between all 5 tabs correctly without throwing or refreshing', async () => {
      const user = userEvent.setup();
      const onOpenScorecard = vi.fn();

      apiMock.mockImplementation((path: string) => {
        if (path === '/applications/app-100/notes') {
          return Promise.resolve([
            {
              id: 'note-1',
              jobApplicationId: 'app-100',
              interviewId: null,
              authorUserId: 'user-me',
              authorName: 'Jane Staff Eng',
              body: 'Strong candidate @Dave Tech Lead please review',
              bodyHtml: 'Strong candidate <span class="mention">@Dave Tech Lead</span> please review',
              createdAt: '2026-08-03T11:00:00Z',
              mentions: [{ userId: 'user-other', displayName: 'Dave Tech Lead' }],
            },
          ]);
        }
        return Promise.resolve([]);
      });

      render(
        <CandidateSlideOver
          candidate={mockCandidate}
          isOpen={true}
          onClose={vi.fn()}
          stageHistory={mockStageHistory}
          interviews={mockInterviews}
          onOpenScorecard={onOpenScorecard}
          applicationFormFieldsJson={JSON.stringify([
            { key: 'experienceYears', label: 'Years of Experience', type: 'number' },
            { key: 'noticePeriod', label: 'Notice Period', type: 'text' },
          ])}
        />
      );

      // Default Overview tab
      expect(screen.getByText('Candidate Profile Summary')).toBeInTheDocument();
      expect(screen.getByText('Proven track record in TypeScript and React UI architecture.')).toBeInTheDocument();
      expect(screen.getByText('Years of Experience')).toBeInTheDocument();
      expect(screen.getByText('7')).toBeInTheDocument();

      // Tab 2: CV Viewer
      const cvTab = screen.getByRole('button', { name: /CV Viewer/i });
      await user.click(cvTab);
      expect(screen.getByText('Jane Doe_Resume.pdf')).toBeInTheDocument();
      expect(screen.getByText('CV Document Preview')).toBeInTheDocument();

      // Tab 3: Stage History
      const historyTab = screen.getByRole('button', { name: /Stage History/i });
      await user.click(historyTab);
      expect(screen.getByText('Moved to Screening')).toBeInTheDocument();
      expect(screen.getByText('Moved to Interview')).toBeInTheDocument();
      expect(screen.getByText(/Recruiter Bob/)).toBeInTheDocument();

      // Tab 4: Scorecards
      const scorecardsTab = screen.getByRole('button', { name: /Interviews/i });
      await user.click(scorecardsTab);
      expect(screen.getByText('Round 1')).toBeInTheDocument();
      expect(screen.getByText(/Jane Staff Eng, Dave Tech Lead/)).toBeInTheDocument();
      const openScorecardBtn = screen.getByRole('button', { name: /Open Scorecard →/i });
      await user.click(openScorecardBtn);
      expect(onOpenScorecard).toHaveBeenCalledWith('iv-201');

      // Tab 5: Notes & Debrief
      const notesTab = screen.getByRole('button', { name: /Notes & Debrief/i });
      await user.click(notesTab);
      expect(await screen.findByText(/Mentioned: Dave Tech Lead/)).toBeInTheDocument();
    });

    it('gracefully handles null candidate and empty stage history', () => {
      render(
        <CandidateSlideOver
          candidate={null}
          isOpen={true}
          onClose={vi.fn()}
        />
      );
      expect(screen.getByText('No candidate profile selected.')).toBeInTheDocument();
    });
  });

  describe('2. Stage Movement in PipelineKanbanBoard', () => {
    it('renders candidate columns for all 8 standard stages', () => {
      render(<PipelineKanbanBoard items={[mockCandidate]} />);
      PIPELINE_STAGES.forEach((stage) => {
        expect(screen.getByRole('heading', { name: stage })).toBeInTheDocument();
      });
    });

    it('triggers stage movement dropdown and calls onMoveStage with proper args', async () => {
      const onMoveStage = vi.fn().mockResolvedValue(undefined);
      const user = userEvent.setup();

      render(<PipelineKanbanBoard items={[mockCandidate]} onMoveStage={onMoveStage} />);

      const select = screen.getByRole('combobox', { name: /Move Jane Doe to stage/i });
      await user.selectOptions(select, 'Offer');

      expect(onMoveStage).toHaveBeenCalledWith('app-100', 'Offer');
    });

    it('prevents stage movement dropdown on terminal stages (Hired / Rejected)', () => {
      const hiredCandidate: PipelineItem = { ...mockCandidate, status: 'Hired' };
      render(<PipelineKanbanBoard items={[hiredCandidate]} onMoveStage={vi.fn()} />);

      expect(screen.queryByRole('combobox', { name: /Move Jane Doe to stage/i })).not.toBeInTheDocument();
    });
  });

});
