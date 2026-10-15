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

  // ── Card legibility, 2026-08-29 ────────────────────────────────────────────────────────
  //
  // The product owner read the board on screen and said the cards were too small to read, with
  // text cut off. Both were true: the contact line was `truncate`d to one clipped line and the
  // cover note was `line-clamp-2`, which cut mid-sentence and left a sliver of the third line
  // showing — that last part reads as a rendering fault, not a deliberate summary.
  //
  // These assert CSS classes, which is normally a smell. It is the right test here precisely
  // because BOTH truncations are CSS-only: `truncate` and `line-clamp-2` leave the full string
  // in the DOM, so `getByText(longNote)` passes whether the reader can see it or not. A text
  // assertion cannot tell the difference between rendered and readable.
  it('shows the whole cover note and contact line — no CSS truncation', () => {
    const longNote =
      'Six years of product design, most recently on an HR SaaS product. Portfolio attached, '
      + 'and I can share the case study behind the onboarding redesign if that is useful.';

    render(
      <PipelineKanbanBoard
        items={[{ ...mockPipelineItems[0], coverNote: longNote, email: 'a.very.long.address@some-quite-long-company-domain.example.com' }]}
      />
    );

    const note = screen.getByText(new RegExp(longNote.slice(0, 40)));
    expect(note.className).not.toMatch(/line-clamp/);

    const contact = screen.getByText('a.very.long.address@some-quite-long-company-domain.example.com');
    expect(contact.className).not.toMatch(/truncate/);
    // And it has to be allowed to wrap, or removing `truncate` just pushes it out of the card.
    expect(contact.className).toMatch(/break-words/);
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
