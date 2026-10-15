import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PipelineItem } from '@recruitops/types';
import { CandidateSlideOver } from './pipeline/CandidateSlideOver';
import { PipelineKanbanBoard } from './pipeline/PipelineKanbanBoard';

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
      await user.click(screen.getByRole('button', { name: /Interviews/i }));
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

});
