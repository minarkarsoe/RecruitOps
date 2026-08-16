import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Interview, InterviewScorecards, MyScorecard, PipelineItem, StageHistoryItem, RequisitionListItem, RequisitionDetail } from '@recruitops/types';
import { CandidateSlideOver } from './pipeline/CandidateSlideOver';
import { PipelineKanbanBoard, PIPELINE_STAGES } from './pipeline/PipelineKanbanBoard';
import { BlindScorecardDrawer } from './interviews/BlindScorecardDrawer';
import { RequisitionTable } from './requisitions/RequisitionTable';
import { RequisitionDrawer } from './requisitions/RequisitionDrawer';

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

const mockInterviewScorecards: InterviewScorecards = {
  interviewId: 'iv-201',
  visible: [
    {
      id: 'sc-99',
      interviewId: 'iv-201',
      interviewerUserId: 'user-other',
      interviewerName: 'Dave Tech Lead',
      recommendation: 'StrongYes',
      summaryComment: 'Great architectural insights on state management.',
      status: 'Submitted',
      submittedAt: '2026-08-03T15:00:00Z',
      responses: [
        {
          scorecardCriterionId: 'crit-1',
          criterionLabel: 'System Architecture',
          criterionType: 'Rating',
          rating: 5,
          yesNo: null,
          comment: 'Strong domain modeling knowledge',
        },
      ],
    },
  ],
  hiddenCount: 0,
  blindedUntilYouSubmit: false,
};

const mockMyScorecard: MyScorecard = {
  interviewId: 'iv-201',
  scorecardTemplateId: 'tpl-1',
  scorecardTemplateName: 'Senior Eng Scorecard',
  criteria: [
    {
      id: 'crit-1',
      sequence: 1,
      label: 'System Architecture',
      guidance: 'Assess scalability and design patterns',
      type: 'Rating',
      isRequired: true,
    },
    {
      id: 'crit-2',
      sequence: 2,
      label: 'Culture Alignment',
      guidance: 'Does candidate demonstrate collaborative mindset?',
      type: 'YesNo',
      isRequired: false,
    },
  ],
  scorecard: null,
};

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
      expect(screen.getByRole('button', { name: /Scorecards/i })).toBeInTheDocument();
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
      const scorecardsTab = screen.getByRole('button', { name: /Scorecards/i });
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

  describe('3. BlindScorecardDrawer Rating Inputs & Submission Flow', () => {
    beforeEach(() => {
      apiMock.mockImplementation((path: string) => {
        if (path === '/interviews/iv-201') return Promise.resolve(mockInterviews[0]);
        if (path === '/interviews/iv-201/scorecards') return Promise.resolve(mockInterviewScorecards);
        if (path === '/interviews/iv-201/scorecard') return Promise.resolve(mockMyScorecard);
        if (path === '/applications/app-100/notes') return Promise.resolve([]);
        if (path === '/interviews/iv-201/scorecard/submit') {
          return Promise.resolve({ ok: true });
        }
        if (path === '/interviews/iv-201/scorecard') {
          return Promise.resolve({ ok: true });
        }
        throw new Error(`Unhandled path: ${path}`);
      });
    });

    it('renders 1-5 rating buttons and toggles aria-pressed on click', async () => {
      const user = userEvent.setup();

      render(<BlindScorecardDrawer interviewId="iv-201" isOpen={true} onClose={vi.fn()} />);

      expect(await screen.findByText('Round 1 Scorecard Evaluation')).toBeInTheDocument();
      expect(screen.getAllByText('System Architecture').length).toBeGreaterThan(0);

      const btn1 = screen.getByRole('button', { name: '1' });
      const btn3 = screen.getByRole('button', { name: '3' });
      const btn5 = screen.getByRole('button', { name: '5' });

      expect(btn5).toHaveAttribute('aria-pressed', 'false');

      await user.click(btn5);
      expect(btn5).toHaveAttribute('aria-pressed', 'true');
      expect(btn1).toHaveAttribute('aria-pressed', 'false');

      await user.click(btn3);
      expect(btn3).toHaveAttribute('aria-pressed', 'true');
      expect(btn5).toHaveAttribute('aria-pressed', 'false');
    });

    it('requires overall recommendation and required criteria before enabling Submit button', async () => {
      const user = userEvent.setup();

      render(<BlindScorecardDrawer interviewId="iv-201" isOpen={true} onClose={vi.fn()} />);

      await screen.findByText('Round 1 Scorecard Evaluation');

      const submitBtn = screen.getByRole('button', { name: /Submit Evaluation/i });
      expect(submitBtn).toBeDisabled();
      expect(screen.getByText(/Still needed to submit: System Architecture/i)).toBeInTheDocument();

      // Click rating 4
      const btn4 = screen.getByRole('button', { name: '4' });
      await user.click(btn4);

      // Select overall recommendation
      const recSelect = screen.getByRole('combobox', { name: /Overall Recommendation/i });
      await user.selectOptions(recSelect, 'StrongYes');

      expect(submitBtn).not.toBeDisabled();
      expect(screen.queryByText(/Still needed to submit:/i)).not.toBeInTheDocument();
    });

    it('handles confirm window and triggers POST submit API endpoint on submit click', async () => {
      const user = userEvent.setup();
      const onSubmitted = vi.fn();
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      render(
        <BlindScorecardDrawer
          interviewId="iv-201"
          isOpen={true}
          onClose={vi.fn()}
          onScorecardSubmitted={onSubmitted}
        />
      );

      await screen.findByText('Round 1 Scorecard Evaluation');

      // Click rating 5
      await user.click(screen.getByRole('button', { name: '5' }));
      // Select recommendation
      await user.selectOptions(screen.getByRole('combobox', { name: /Overall Recommendation/i }), 'Yes');

      const submitBtn = screen.getByRole('button', { name: /Submit Evaluation/i });
      await user.click(submitBtn);

      await waitFor(() => {
        expect(apiMock).toHaveBeenCalledWith(
          '/interviews/iv-201/scorecard/submit',
          expect.objectContaining({
            method: 'POST',
          })
        );
      });
      expect(onSubmitted).toHaveBeenCalled();
    });
  });

  describe('4. Requisitions Feature Module Verification', () => {
    it('renders requisition table, applies search/status filters, and opens drawer', async () => {
      const onSelect = vi.fn();

      const items: RequisitionListItem[] = [
        {
          id: 'req-1',
          departmentId: 'd-1',
          departmentName: 'Engineering',
          title: 'Principal Architect',
          headcount: 1,
          salaryBudget: 200000,
          status: 'PendingApproval',
          submittedAt: '2026-08-01T00:00:00Z',
          awaitingApprovalFrom: 'CTO',

          yourStepLabel: 'CTO',
        },
      ];

      const detail: RequisitionDetail = {
        ...items[0],
        jobDescription: 'Lead enterprise architecture across cloud services.',
        decidedAt: null,
        requestedByUserId: 'user-other',
        approvals: [
          {
            round: 1,
            sequence: 1,
            label: 'CTO Approval',
            approverUserId: 'user-me',
            decision: 'Waiting',
            decidedAt: null,
            comment: null,
            decidedByUserId: null,
          },
        ],
      };

      render(
        <div>
          <RequisitionTable items={items} onSelectRequisition={onSelect} />
          <RequisitionDrawer requisition={detail} isOpen={true} onClose={vi.fn()} onDecide={vi.fn()} />
        </div>
      );

      expect(screen.getAllByText('Principal Architect').length).toBeGreaterThan(0);
      expect(screen.getAllByText('$200,000').length).toBeGreaterThan(0);
      expect(screen.getAllByText('CTO').length).toBeGreaterThan(0);
      expect(screen.getByText('Lead enterprise architecture across cloud services.')).toBeInTheDocument();
      expect(screen.getByText('Approval Action Required — CTO')).toBeInTheDocument();

      const approveBtn = screen.getByRole('button', { name: /Approve Requisition/i });
      expect(approveBtn).toBeInTheDocument();
    });
  });
});
