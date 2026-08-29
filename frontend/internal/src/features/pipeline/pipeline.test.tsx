import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PipelineItem, StageHistoryItem } from '@recruitops/types';
import { PipelineKanbanBoard } from './PipelineKanbanBoard';
import { CandidateSlideOver } from './CandidateSlideOver';

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../lib/api')>('../../lib/api');
  return { ...actual, api: apiMock };
});

const mockPipelineItems: PipelineItem[] = [
  {
    id: 'app-1',
    candidateId: 'cand-1',
    candidateName: 'Alice Smith',
    email: 'alice@example.com',
    phone: '+1 555 0100',
    status: 'Screening',
    source: 'LinkedIn',
    appliedAt: '2026-08-02T12:00:00Z',
    coverNote: 'Excited about the role!',
    customFieldsJson: JSON.stringify({ yearsExperience: 5 }),
  },
  {
    id: 'app-2',
    candidateId: 'cand-2',
    candidateName: 'Bob Jones',
    email: 'bob@example.com',
    phone: '+1 555 0200',
    status: 'Interview',
    source: 'Referral',
    appliedAt: '2026-08-01T09:00:00Z',
    coverNote: null,
    customFieldsJson: null,
  },
];

const mockStageHistory: StageHistoryItem[] = [
  {
    fromStatus: 'Applied',
    toStatus: 'Screening',
    changedAt: '2026-08-02T14:00:00Z',
    changedByName: 'Recruiter Jane',
    note: 'Great background',
  },
];

describe('PipelineKanbanBoard', () => {
  it('renders stages and candidate cards', () => {
    render(<PipelineKanbanBoard items={mockPipelineItems} />);

    expect(screen.getByText('Screening')).toBeInTheDocument();
    expect(screen.getByText('Interview')).toBeInTheDocument();
    expect(screen.getByText('Alice Smith')).toBeInTheDocument();
    expect(screen.getByText('Bob Jones')).toBeInTheDocument();
    expect(screen.getByText(/Excited about the role!/)).toBeInTheDocument();
  });

  it('triggers onSelectCandidate when candidate card is clicked', async () => {
    const onSelectCandidate = vi.fn();
    const user = userEvent.setup();

    render(<PipelineKanbanBoard items={mockPipelineItems} onSelectCandidate={onSelectCandidate} />);

    await user.click(screen.getByText('Alice Smith'));
    // The APPLICATION id, not the candidate id. A candidate who applied to two postings has one
    // candidate id across both, so it cannot identify the row that was clicked — and the drawer,
    // the history endpoint and ApplicationDebrief are all keyed by application.
    expect(onSelectCandidate).toHaveBeenCalledWith('app-1');
  });

  it('triggers onMoveStage when stage dropdown is changed', async () => {
    const onMoveStage = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();

    render(<PipelineKanbanBoard items={mockPipelineItems} onMoveStage={onMoveStage} />);

    const selectDropdown = screen.getByRole('combobox', { name: /Move Alice Smith to stage/i });
    await user.selectOptions(selectDropdown, 'Interview');

    expect(onMoveStage).toHaveBeenCalledWith('app-1', 'Interview');
  });
});

describe('CandidateSlideOver', () => {
  it('renders Candidate 360 profile and tabs when open', () => {
    render(
      <CandidateSlideOver
        candidate={mockPipelineItems[0]}
        isOpen={true}
        onClose={vi.fn()}
        stageHistory={mockStageHistory}
      />
    );

    expect(screen.getAllByText('Alice Smith').length).toBeGreaterThan(0);
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Overview/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /CV Viewer/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Stage History/i })).toBeInTheDocument();
  });

  it('switches tabs when clicked', async () => {
    const user = userEvent.setup();

    render(
      <CandidateSlideOver
        candidate={mockPipelineItems[0]}
        isOpen={true}
        onClose={vi.fn()}
        stageHistory={mockStageHistory}
      />
    );

    const historyTab = screen.getByRole('button', { name: /Stage History/i });
    await user.click(historyTab);

    expect(screen.getByText('Moved to Screening')).toBeInTheDocument();
    expect(screen.getByText(/Recruiter Jane/)).toBeInTheDocument();
  });
});
