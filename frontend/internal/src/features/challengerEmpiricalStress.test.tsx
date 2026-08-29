import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import type { Interview, PipelineItem, StageHistoryItem } from '@recruitops/types';
import { CandidateSlideOver } from './pipeline/CandidateSlideOver';
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

});
