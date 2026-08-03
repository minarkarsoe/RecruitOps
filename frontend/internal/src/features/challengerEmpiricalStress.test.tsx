import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Interview, PipelineItem, RequisitionDetail, RequisitionListItem, StageHistoryItem } from '@recruitops/types';
import { CandidateSlideOver } from './pipeline/CandidateSlideOver';
import { BlindScorecardDrawer } from './interviews/BlindScorecardDrawer';
import { RequisitionTable } from './requisitions/RequisitionTable';
import { RequisitionDrawer } from './requisitions/RequisitionDrawer';
import { ApplicationNotes } from '../components/ApplicationNotes';

// Mock API and Auth libraries
vi.mock('../lib/api', () => ({
  api: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number;
    constructor(status: number, message: string) {
      super(message);
      this.status = status;
    }
  },
}));

vi.mock('../lib/auth', () => ({
  auth: {
    get: vi.fn(() => ({
      userId: 'usr-1',
      tenantId: 'tenant-1',
      role: 'Admin',
      permissions: [
        'permission:scorecards:scorecards:submit',
        'permission:requisitions:requisitions:approve',
        'permission:requisitions:requisitions:delete',
        'permission:requisitions:requisitions:update',
      ],
    })),
  },
  hasPermission: vi.fn(() => true),
}));

import { api } from '../lib/api';

describe('Challenger Empirical Stress Suite — Milestone 3 Modules', () => {
  const mockApi = api as unknown as ReturnType<typeof vi.fn>;

  describe('CandidateSlideOver (Candidate 360 Profile)', () => {
    const mockCandidate: PipelineItem = {
      id: 'app-100',
      candidateId: 'cand-100',
      candidateName: 'Khin Maung Aye',
      email: 'khin@example.com',
      phone: '+95912345678',
      status: 'Interview',
      appliedAt: '2026-08-01T10:00:00Z',
      source: 'LinkedIn',
      coverNote: 'Excited about the role. \nမင်္ဂလာပါ။',
      customFieldsJson: JSON.stringify({
        yearsExperience: 5,
        noticePeriodDays: 30,
        relocate: true,
        comments: 'Ready to start asap',
      }),
    };

    const mockStageHistory: StageHistoryItem[] = [
      {
        fromStatus: 'Applied',
        toStatus: 'Interview',
        changedAt: '2026-08-01T12:00:00Z',
        changedByName: 'Recruiter Admin',
        note: 'Strong experience match',
      },
    ];

    const mockInterviews: Interview[] = [
      {
        id: 'int-1',
        jobApplicationId: 'app-100',
        round: 1,
        status: 'Scheduled',
        mode: 'Video',
        scheduledStart: '2026-08-05T09:00:00Z',
        durationMinutes: 45,
        location: 'Google Meet',
        agenda: null,
        cancellationReason: null,
        scorecardTemplateId: null,
        scorecardTemplateName: null,
        participants: [
          { userId: 'usr-1', displayName: 'Interviewer One', email: 'usr1@example.com', isLead: true, hasSubmittedScorecard: true },
          { userId: 'usr-2', displayName: 'Interviewer Two', email: 'usr2@example.com', isLead: false, hasSubmittedScorecard: false },
        ],
      },
    ];

    it('renders candidate 360 profile cleanly when open', () => {
      render(
        <CandidateSlideOver
          isOpen={true}
          onClose={() => {}}
          candidate={mockCandidate}
          stageHistory={mockStageHistory}
          interviews={mockInterviews}
          applicationFormFieldsJson={JSON.stringify([
            { key: 'yearsExperience', label: 'Years of Experience', type: 'number' },
            { key: 'noticePeriodDays', label: 'Notice Period (Days)', type: 'number' },
            { key: 'relocate', label: 'Willing to Relocate', type: 'boolean' },
          ])}
        />
      );

      expect(screen.getAllByText('Khin Maung Aye').length).toBeGreaterThan(0);
      expect(screen.getAllByText(/khin@example.com/).length).toBeGreaterThan(0);
      expect(screen.getByText(/Excited about the role/i)).toBeInTheDocument();
      expect(screen.getByText('Years of Experience')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.getByText('Yes')).toBeInTheDocument();
    });

    it('handles tab switches smoothly across all 5 candidate 360 tabs', () => {
      mockApi.mockResolvedValue([]);
      render(
        <CandidateSlideOver
          isOpen={true}
          onClose={() => {}}
          candidate={mockCandidate}
          stageHistory={mockStageHistory}
          interviews={mockInterviews}
        />
      );

      // CV tab
      fireEvent.click(screen.getByRole('button', { name: /CV Viewer/i }));
      expect(screen.getByText(/Khin Maung Aye_Resume.pdf/i)).toBeInTheDocument();

      // Stage History tab
      fireEvent.click(screen.getByRole('button', { name: /Stage History/i }));
      expect(screen.getByText('Moved to Interview')).toBeInTheDocument();
      expect(screen.getByText(/Recruiter Admin/i)).toBeInTheDocument();

      // Scorecards tab
      fireEvent.click(screen.getByRole('button', { name: /Scorecards/i }));
      expect(screen.getByText('Round 1')).toBeInTheDocument();
      expect(screen.getByText(/Interviewer One, Interviewer Two/i)).toBeInTheDocument();

      // Notes tab
      fireEvent.click(screen.getByRole('button', { name: /Notes & Debrief/i }));
      expect(screen.getByText('Loading notes…')).toBeInTheDocument();
    });

    it('handles missing/malformed optional props gracefully without uncaught exceptions', () => {
      render(
        <CandidateSlideOver
          isOpen={true}
          onClose={() => {}}
          candidate={{
            ...mockCandidate,
            email: null,
            phone: null,
            source: 'Direct',
            coverNote: null,
            customFieldsJson: 'invalid json content {{{',
          }}
          stageHistory={[]}
          interviews={[]}
        />
      );

      expect(screen.getAllByText('Khin Maung Aye').length).toBeGreaterThan(0);
      expect(screen.getByText(/No email · No phone/i)).toBeInTheDocument();
      expect(screen.getByText('No cover note submitted.')).toBeInTheDocument();
      expect(screen.getByText('Unable to parse custom responses.')).toBeInTheDocument();
    });
  });

  describe('BlindScorecardDrawer (Interviews)', () => {
    const mockInterview: Interview = {
      id: 'int-200',
      jobApplicationId: 'app-100',
      round: 2,
      status: 'Scheduled',
      mode: 'Video',
      scheduledStart: '2026-08-04T10:00:00Z',
      durationMinutes: 60,
      location: 'Zoom',
      agenda: null,
      cancellationReason: null,
      scorecardTemplateId: 'tpl-1',
      scorecardTemplateName: 'Senior Software Engineer Template',
      participants: [
        { userId: 'usr-1', displayName: 'Lead Interviewer', email: 'usr1@example.com', isLead: true, hasSubmittedScorecard: false },
        { userId: 'usr-2', displayName: 'Panel Reviewer', email: 'usr2@example.com', isLead: false, hasSubmittedScorecard: true },
      ],
    };

    const mockPanelScorecards = {
      blindedUntilYouSubmit: true,
      hiddenCount: 1,
      visible: [],
    };

    const mockMyScorecard = {
      criteria: [
        {
          id: 'crit-1',
          label: 'Technical Architecture & System Design',
          type: 'Rating' as const,
          isRequired: true,
          guidance: 'Evaluate distributed systems knowledge',
        },
        {
          id: 'crit-2',
          label: 'Cultural Alignment & Leadership',
          type: 'YesNo' as const,
          isRequired: false,
          guidance: 'Evaluates teamwork and communication',
        },
      ],
      scorecard: null,
    };

    it('loads and renders BlindScorecardDrawer in draft evaluation state', async () => {
      mockApi.mockImplementation((url: string) => {
        if (url === '/interviews/int-200') return Promise.resolve(mockInterview);
        if (url === '/interviews/int-200/scorecards') return Promise.resolve(mockPanelScorecards);
        if (url === '/interviews/int-200/scorecard') return Promise.resolve(mockMyScorecard);
        if (url.includes('/notes')) return Promise.resolve([]);
        return Promise.reject(new Error(`Unhandled URL: ${url}`));
      });

      render(
        <BlindScorecardDrawer
          isOpen={true}
          interviewId="int-200"
          onClose={() => {}}
        />
      );

      expect(screen.getByText('Loading interview scorecard...')).toBeInTheDocument();

      await waitFor(() => {
        expect(screen.getByText('Round 2 Scorecard Evaluation')).toBeInTheDocument();
      });

      expect(screen.getByText('Technical Architecture & System Design')).toBeInTheDocument();
      expect(screen.getByText('Cultural Alignment & Leadership')).toBeInTheDocument();
      expect(screen.getByText('1 evaluation is waiting for yours. Submit your evaluation to unlock panel feedback.')).toBeInTheDocument();
    });

    it('allows entering score ratings, selecting recommendation, and submitting scorecard', async () => {
      let putCalled = false;
      let postCalled = false;

      let currentMyScorecard: any = { ...mockMyScorecard };

      mockApi.mockImplementation((url: string, options?: any) => {
        if (url === '/interviews/int-200') return Promise.resolve(mockInterview);
        if (url === '/interviews/int-200/scorecards') return Promise.resolve(mockPanelScorecards);
        if (url === '/interviews/int-200/scorecard' && (!options || options.method === 'GET')) {
          return Promise.resolve(currentMyScorecard);
        }
        if (url === '/interviews/int-200/scorecard' && options?.method === 'PUT') {
          putCalled = true;
          const body = JSON.parse(options.body);
          currentMyScorecard = {
            ...currentMyScorecard,
            scorecard: {
              id: 'sc-1',
              interviewerUserId: 'usr-1',
              interviewerName: 'Lead Interviewer',
              status: 'Draft',
              recommendation: body.recommendation,
              summaryComment: body.summaryComment,
              submittedAt: null,
              responses: [
                {
                  scorecardCriterionId: 'crit-1',
                  criterionLabel: 'Technical Architecture & System Design',
                  criterionType: 'Rating',
                  rating: 4,
                  yesNo: null,
                  comment: null,
                },
              ],
            },
          };
          return Promise.resolve({ success: true });
        }
        if (url === '/interviews/int-200/scorecard/submit' && options?.method === 'POST') {
          postCalled = true;
          return Promise.resolve({ success: true });
        }
        if (url.includes('/notes')) return Promise.resolve([]);
        return Promise.reject(new Error(`Unhandled URL: ${url}`));
      });

      // Confirm dialog mock
      vi.spyOn(window, 'confirm').mockReturnValue(true);

      render(
        <BlindScorecardDrawer
          isOpen={true}
          interviewId="int-200"
          onClose={() => {}}
        />
      );

      await waitFor(() => {
        expect(screen.getByText('Technical Architecture & System Design')).toBeInTheDocument();
      });

      // Click Rating button 4
      const ratingButtons = screen.getAllByRole('button', { name: '4' });
      fireEvent.click(ratingButtons[0]);

      // Select recommendation
      const user = userEvent.setup();
      const select = screen.getByLabelText('Overall Recommendation');
      await user.selectOptions(select, 'StrongYes');

      // Click Save Draft
      const saveButton = screen.getByRole('button', { name: 'Save Draft' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(putCalled).toBe(true);
      });

      // Click Submit Evaluation
      const submitButton = screen.getByRole('button', { name: 'Submit Evaluation' });
      await user.click(submitButton);

      await waitFor(() => {
        expect(postCalled).toBe(true);
      });
    });
  });

  describe('ApplicationNotes Component & Mentions', () => {
    it('handles notes with null mentions, bodyHtml Burmese/Unicode, and empty thread gracefully', async () => {
      const mockNotes = [
        {
          id: 'note-1',
          applicationId: 'app-100',
          authorUserId: 'usr-2',
          authorName: 'Aung Zaw',
          createdAt: '2026-08-02T14:30:00Z',
          body: 'Candidate speaks Burmese fluently. @khin',
          bodyHtml: 'Candidate speaks Burmese fluently. <span class="mention">@khin</span>',
          mentions: [
            { userId: 'usr-3', displayName: 'Khin Aye' }
          ]
        },
        {
          id: 'note-2',
          applicationId: 'app-100',
          authorUserId: 'usr-1',
          authorName: 'You',
          createdAt: '2026-08-03T10:00:00Z',
          body: 'Note without mentions array',
          bodyHtml: 'Note without mentions array',
          mentions: undefined, // test undefined mentions
        }
      ];

      mockApi.mockImplementation((url: string) => {
        if (url === '/applications/app-100/notes') return Promise.resolve(mockNotes);
        return Promise.reject(new Error(`Unhandled URL: ${url}`));
      });

      render(<ApplicationNotes applicationId="app-100" />);

      expect(screen.getByText('Loading notes…')).toBeInTheDocument();

      await waitFor(() => {
        expect(screen.getByText('Notes · 2')).toBeInTheDocument();
      });

      expect(screen.getByText('Aung Zaw')).toBeInTheDocument();
      expect(screen.getByText('Mentioned: Khin Aye')).toBeInTheDocument();
      expect(screen.getByText('Note without mentions array')).toBeInTheDocument();
    });
  });

  describe('Requisitions Feature Module', () => {
    const mockRequisitions: RequisitionListItem[] = [
      {
        id: 'req-1',
        title: 'Lead Frontend Engineer',
        departmentId: 'dept-1',
        departmentName: 'Engineering',
        headcount: 2,
        salaryBudget: 150000,
        status: 'PendingApproval',
        submittedAt: '2026-07-20T00:00:00Z',
        awaitingApprovalFrom: 'VP Engineering',
      },
      {
        id: 'req-2',
        title: 'Product Manager',
        departmentId: 'dept-2',
        departmentName: 'Product',
        headcount: 1,
        salaryBudget: null,
        status: 'Draft',
        submittedAt: null,
        awaitingApprovalFrom: null,
      },
    ];

    const mockReqDetail: RequisitionDetail = {
      ...mockRequisitions[0],
      requestedByUserId: 'usr-1',
      jobDescription: 'Leading the RecruitOps frontend modernization initiative.',
      submittedAt: '2026-07-21T10:00:00Z',
      decidedAt: null,
      approvals: [
        {
          sequence: 1,
          label: 'Engineering Manager Approval',
          decision: 'Approved',
          approverUserId: 'usr-9',
          decidedAt: '2026-07-22T08:00:00Z',
          comment: 'Approved for Q3 hiring plan.',
        },
        {
          sequence: 2,
          label: 'VP Engineering Approval',
          decision: 'Waiting',
          approverUserId: 'usr-1', // current user is active approver
          decidedAt: null,
          comment: null,
        },
      ],
    };

    it('renders RequisitionTable correctly and supports filtering/searching', () => {
      const onSearchChange = vi.fn();
      const onStatusChange = vi.fn();

      render(
        <RequisitionTable
          items={mockRequisitions}
          searchQuery=""
          onSearchQueryChange={onSearchChange}
          statusFilter="all"
          onStatusFilterChange={onStatusChange}
          canCreate={true}
          onNewRequisition={() => {}}
        />
      );

      expect(screen.getByText('Lead Frontend Engineer')).toBeInTheDocument();
      expect(screen.getByText('Product Manager')).toBeInTheDocument();
      expect(screen.getByText('$150,000')).toBeInTheDocument();
      expect(screen.getAllByText('—').length).toBeGreaterThan(0); // null salaryBudget & null awaiting approver

      const filterInput = screen.getByPlaceholderText('Filter requisitions...');
      fireEvent.change(filterInput, { target: { value: 'Frontend' } });
      expect(onSearchChange).toHaveBeenCalledWith('Frontend');
    });

    it('renders RequisitionDrawer with approval action when user is active approver', async () => {
      const onDecide = vi.fn().mockResolvedValue(undefined);

      render(
        <RequisitionDrawer
          isOpen={true}
          requisition={mockReqDetail}
          onClose={() => {}}
          onDecide={onDecide}
        />
      );

      expect(screen.getByText('Approval Action Required — VP Engineering')).toBeInTheDocument();
      expect(screen.getByText('Leading the RecruitOps frontend modernization initiative.')).toBeInTheDocument();

      const approveButton = screen.getByRole('button', { name: 'Approve Requisition' });
      fireEvent.click(approveButton);

      await waitFor(() => {
        expect(onDecide).toHaveBeenCalledWith('req-1', true, '');
      });
    });
  });
});
