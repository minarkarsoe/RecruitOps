import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {
  Interview,
  InterviewScorecards,
  MyScorecard,
  PipelineItem,
  RequisitionDetail,
  RequisitionListItem,
} from '@recruitops/types';
import { CandidateSlideOver } from './pipeline/CandidateSlideOver';
import { PipelineKanbanBoard } from './pipeline/PipelineKanbanBoard';
import { BlindScorecardDrawer } from './interviews/BlindScorecardDrawer';
import { RequisitionTable } from './requisitions/RequisitionTable';
import { RequisitionDrawer } from './requisitions/RequisitionDrawer';
import { ApplicationNotes } from '../components/ApplicationNotes';

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

describe('Empirical Stress Testing Suite (Milestone 3 Retry 2)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('1. Requisition Components Resilience & Omitted DTO Optional Fields', () => {
    it('renders RequisitionTable cleanly when DTO optional fields are omitted or null', () => {
      const itemWithMissingFields: RequisitionListItem = {
        id: 'req-minimal',
        title: 'Minimal Role',
        departmentId: 'dept-1',
        departmentName: 'Engineering',
        headcount: 1,
        salaryBudget: null, // Omitted budget
        status: 'Draft',
        awaitingApprovalFrom: null, // Omitted approver
        yourStepLabel: null,
        submittedAt: null,
      };

      render(
        <RequisitionTable
          items={[itemWithMissingFields]}
          statusFilter="all"
          searchQuery=""
          canCreate={true}
          onSelectRequisition={vi.fn()}
        />
      );

      expect(screen.getByText('Minimal Role')).toBeInTheDocument();
      expect(screen.getByText('Engineering')).toBeInTheDocument();
      // Should show fallback dash '—' for missing salaryBudget and awaitingApprovalFrom
      const dashes = screen.getAllByText('—');
      expect(dashes.length).toBeGreaterThanOrEqual(2);
    });

    it('renders RequisitionDrawer cleanly with null/omitted optional DTO fields', () => {
      const minimalRequisition: RequisitionDetail = {
        id: 'req-minimal-detail',
        title: 'Draft Minimal Req',
        departmentId: 'dept-1',
        departmentName: 'Product',
        headcount: 3,
        salaryBudget: null,
        status: 'Draft',
        awaitingApprovalFrom: null,

        yourStepLabel: null,
        submittedAt: null,
        decidedAt: null,
        jobDescription: '',
        requestedByUserId: 'user-me',
        approvals: [],
      };

      render(
        <RequisitionDrawer
          requisition={minimalRequisition}
          isOpen={true}
          onClose={vi.fn()}
        />
      );

      expect(screen.getByText('Draft Minimal Req')).toBeInTheDocument();
      expect(screen.getByText('Product')).toBeInTheDocument();
      expect(screen.getByText('No description provided.')).toBeInTheDocument();
      expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(3);
    });

    it('handles co-rendered RequisitionTable and RequisitionDrawer without element query collisions', () => {
      const reqItem: RequisitionListItem = {
        id: 'req-shared',
        title: 'Principal Staff Engineer',
        departmentId: 'dept-eng',
        departmentName: 'Core Platform',
        headcount: 2,
        salaryBudget: 250000,
        status: 'PendingApproval',
        awaitingApprovalFrom: 'CTO Alice',

        yourStepLabel: 'CTO Alice',
        submittedAt: '2026-08-01T00:00:00Z',
      };

      const reqDetail: RequisitionDetail = {
        ...reqItem,
        decidedAt: null,
        jobDescription: 'Lead core architecture team.',
        requestedByUserId: 'user-me',
        approvals: [
          {
            round: 1,
            sequence: 1,
            label: 'Tech Lead Approval',
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
          <RequisitionTable items={[reqItem]} />
          <RequisitionDrawer requisition={reqDetail} isOpen={true} onClose={vi.fn()} />
        </div>
      );

      // Co-rendered text: Title, department name, salary, approver exist in BOTH components simultaneously
      const titleElements = screen.getAllByText('Principal Staff Engineer');
      expect(titleElements.length).toEqual(2);

      const deptElements = screen.getAllByText('Core Platform');
      expect(deptElements.length).toEqual(2);

      const budgetElements = screen.getAllByText('$250,000');
      expect(budgetElements.length).toEqual(2);

      const approverElements = screen.getAllByText(/CTO Alice/);
      expect(approverElements.length).toEqual(2);
    });
  });

  describe('2. Candidate Pipeline & 360 SlideOver Edge Case Resilience', () => {
    it('renders CandidateSlideOver cleanly with minimal/null candidate DTO fields', async () => {
      const minimalCandidate: PipelineItem = {
        id: 'app-min',
        candidateId: 'cand-min',
        candidateName: 'Alex Minimal',
        email: null,
        phone: null,
        status: 'Sourced',
        source: 'Direct',
        appliedAt: '2026-08-03T10:00:00Z',
        coverNote: null,
        customFieldsJson: null,
      };

      render(
        <CandidateSlideOver
          candidate={minimalCandidate}
          isOpen={true}
          onClose={vi.fn()}
          stageHistory={[]}
          interviews={[]}
        />
      );

      // Alex Minimal appears in header title and in overview tab profile summary
      expect(screen.getAllByText('Alex Minimal').length).toBeGreaterThanOrEqual(2);
      expect(screen.getByText(/No email · No phone/)).toBeInTheDocument();
      expect(screen.getByText('No cover note submitted.')).toBeInTheDocument();
      expect(screen.getByText('No custom response submitted.')).toBeInTheDocument();

      // Switch to Stage History tab button
      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: /Stage History/i }));
      expect(screen.getByText('No stage history recorded yet.')).toBeInTheDocument();

      // Switch to Scorecards tab button
      await user.click(screen.getByRole('button', { name: /Scorecards/i }));
      expect(screen.getByText('No interview rounds scheduled yet.')).toBeInTheDocument();
    });

    it('renders PipelineKanbanBoard with omitted candidate fields without errors', () => {
      const items: PipelineItem[] = [
        {
          id: 'app-1',
          candidateId: 'c-1',
          candidateName: 'Candidate One',
          email: null,
          phone: null,
          status: 'Applied',
          source: 'Direct',
          appliedAt: '2026-08-02T12:00:00Z',
          coverNote: null,
          customFieldsJson: null,
        },
      ];

      render(<PipelineKanbanBoard items={items} />);

      expect(screen.getByText('Candidate One')).toBeInTheDocument();
      expect(screen.getByText('No contact specified')).toBeInTheDocument();
    });
  });

  describe('3. BlindScorecardDrawer & ApplicationNotes Edge Case Resilience', () => {
    it('handles ApplicationNotes when note.mentions is null or undefined without throwing', async () => {
      apiMock.mockResolvedValue([
        {
          id: 'note-1',
          applicationId: 'app-1',
          authorUserId: 'user-other',
          authorName: 'Bob Smith',
          body: 'Note body text',
          bodyHtml: 'Note body text',
          createdAt: '2026-08-03T12:00:00Z',
          interviewId: null,
          mentions: undefined, // Missing mentions field!
        },
        {
          id: 'note-2',
          applicationId: 'app-1',
          authorUserId: 'user-me',
          authorName: 'Jane Staff',
          body: 'Second note body text',
          bodyHtml: 'Second note body text',
          createdAt: '2026-08-03T12:30:00Z',
          interviewId: null,
          mentions: null, // Null mentions field!
        },
      ]);

      render(<ApplicationNotes applicationId="app-1" />);

      expect(await screen.findByText('Note body text')).toBeInTheDocument();
      expect(screen.getByText('Second note body text')).toBeInTheDocument();
      // Mentions text should not be rendered, but no exception thrown
      expect(screen.queryByText(/Mentioned:/)).not.toBeInTheDocument();
    });

    it('renders BlindScorecardDrawer with missing/null optional fields on Interview and Scorecard DTOs', async () => {
      const minimalInterview: Interview = {
        id: 'iv-min',
        jobApplicationId: 'app-1',
        round: 1,
        scheduledStart: '2026-08-03T14:00:00Z',
        durationMinutes: 45,
        mode: 'Phone',
        location: null, // Omitted location
        status: 'Scheduled',
        agenda: null,
        cancellationReason: null,
        scorecardTemplateId: 'tpl-1',
        scorecardTemplateName: null, // Omitted template name
        participants: [
          {
            userId: 'user-me',
            displayName: 'Jane Staff',
            email: 'jane@example.com',
            isLead: true,
            hasSubmittedScorecard: false,
          },
        ],
      };

      const mockPanel: InterviewScorecards = {
        interviewId: 'iv-min',
        visible: [],
        hiddenCount: 0,
        blindedUntilYouSubmit: true,
      };

      const mockMyScorecard: MyScorecard = {
        interviewId: 'iv-min',
        scorecardTemplateId: 'tpl-1',
        scorecardTemplateName: null,
        scorecard: null,
        criteria: [
          {
            id: 'crit-1',
            sequence: 1,
            label: 'Technical Depth',
            guidance: null,
            type: 'Rating',
            isRequired: true,
          },
        ],
      };

      apiMock.mockImplementation((url: string) => {
        if (url === '/interviews/iv-min') return Promise.resolve(minimalInterview);
        if (url === '/interviews/iv-min/scorecards') return Promise.resolve(mockPanel);
        if (url === '/interviews/iv-min/scorecard') return Promise.resolve(mockMyScorecard);
        if (url.includes('/notes')) return Promise.resolve([]);
        return Promise.reject(new Error('Not found'));
      });

      render(
        <BlindScorecardDrawer
          interviewId="iv-min"
          isOpen={true}
          onClose={vi.fn()}
        />
      );

      expect(await screen.findByText('Round 1 Scorecard Evaluation')).toBeInTheDocument();
      expect(screen.getByText('Technical Depth')).toBeInTheDocument();
      expect(screen.getByText(/Jane Staff/)).toBeInTheDocument();
      expect(screen.getByText(/Nobody else has submitted yet/)).toBeInTheDocument();
    });
  });
});
