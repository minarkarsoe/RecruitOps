import { Input } from '@recruitops/ui';
import type { PipelineItem, PipelineStatus } from '@recruitops/types';

// Built against `design/internal/board.html` (ADR-0025), which is the kit's home surface.
//
// Three things changed shape here, not just colour:
//
//  1. The columns are WHITE on the canvas ground, not a grey fill on white. The kit's board is
//     a set of cards floating on the page, and a grey column holding white cards inverts that —
//     it makes the container louder than its contents on a screen that is nothing but contents.
//  2. The count is `font-mono tnum text-ink-500`, not a Badge. A badge is a status; a column
//     count is a number that changes every time a card moves, and tabular figures stop it
//     jittering. Eight coloured badges across the top of the board were eight things competing
//     with the stage names for attention.
//  3. Loading is a skeleton, empty is a sentence. The kit's rule, and it is not decoration:
//     "Loading…" tells the user to wait, a skeleton tells them what is coming; and the Hired
//     column's empty state teaches the one thing about it that is dangerous to learn by doing.

export const PIPELINE_STAGES: PipelineStatus[] = [
  'Sourced',
  'Applied',
  'Screening',
  'Shortlisted',
  'Interview',
  'Offer',
  'Hired',
  'Rejected',
];

/** The dot in each column header. Colours are the kit's, read off `board.html`: neutral before
 *  anyone has engaged, info while in play, brand at the decision point, warn once a commitment
 *  is outstanding, and the two terminal outcomes in positive/critical. `Rejected` is the one
 *  stage the kit does not draw — critical-500 is the only remaining member of that set. */
const STAGE_DOT: Record<PipelineStatus, string> = {
  Sourced: 'bg-ink-400',
  Applied: 'bg-info-500',
  Screening: 'bg-info-500',
  Shortlisted: 'bg-brand-700',
  Interview: 'bg-warn-500',
  Offer: 'bg-warn-500',
  Hired: 'bg-positive-500',
  Rejected: 'bg-critical-500',
};

/** An empty column is a chance to say something true about the stage. Only the two terminal
 *  stages get a specific line — they are the ones where the consequence is not obvious from the
 *  name, and the kit writes the Hired one out in full for exactly that reason. */
const EMPTY_HINT: Partial<Record<PipelineStatus, JSX.Element>> = {
  Hired: (
    <>
      Nobody yet. Moving a candidate here is <span className="font-medium text-ink-900">final</span> — it
      closes the requisition and feeds time-to-hire.
    </>
  ),
  Rejected: (
    <>
      Nobody yet. A rejection stays on the candidate&rsquo;s record and is visible to everyone on the
      requisition.
    </>
  ),
};

export interface PipelineKanbanBoardProps {
  postingId?: string;
  items: PipelineItem[];
  applicationFormFieldsJson?: string | null;
  onMoveStage?: (applicationId: string, toStatus: PipelineStatus) => Promise<void>;
  onSelectCandidate?: (candidateId: string) => void;
  isLoading?: boolean;
  isMoving?: boolean;
  searchQuery?: string;
  onSearchQueryChange?: (query: string) => void;
  className?: string;
}

function CardSkeleton() {
  return (
    <div className="rounded-md border border-line bg-white p-2.5">
      <span className="skeleton block h-4 w-32" />
      <span className="skeleton mt-1.5 block h-3 w-24" />
    </div>
  );
}

export function PipelineKanbanBoard({
  items,
  onMoveStage,
  onSelectCandidate,
  isLoading = false,
  isMoving = false,
  searchQuery = '',
  onSearchQueryChange,
  className = '',
}: PipelineKanbanBoardProps) {
  const groupedStageMap = PIPELINE_STAGES.reduce<Record<PipelineStatus, PipelineItem[]>>(
    (acc, stage) => {
      acc[stage] = items.filter((item) => item.status === stage);
      return acc;
    },
    {
      Sourced: [],
      Applied: [],
      Screening: [],
      Shortlisted: [],
      Interview: [],
      Offer: [],
      Hired: [],
      Rejected: [],
    }
  );

  return (
    <div className={`space-y-3 ${className}`}>
      {onSearchQueryChange && (
        <div className="flex items-center justify-between gap-4">
          <div className="w-72 max-w-full">
            <Input
              type="search"
              placeholder="Search candidate name, email…"
              value={searchQuery}
              onChange={(e) => onSearchQueryChange(e.target.value)}
            />
          </div>
          <div className="text-sm text-ink-600">
            Total candidates <span className="ml-1 font-mono tnum text-ink-900">{items.length}</span>
          </div>
        </div>
      )}

      <div className="flex snap-x gap-3 overflow-x-auto pb-4">
        {PIPELINE_STAGES.map((stage) => {
          const stageItems = groupedStageMap[stage];
          const isTerminal = stage === 'Hired' || stage === 'Rejected';

          return (
            <section
              key={stage}
              className="flex w-[264px] shrink-0 snap-start flex-col rounded-lg border border-line bg-white"
            >
              <header className="flex h-11 shrink-0 items-center gap-2 border-b border-line px-3">
                <span className={`h-1.5 w-1.5 rounded-full ${STAGE_DOT[stage]}`} aria-hidden="true" />
                <h3 className="text-sm font-semibold">{stage}</h3>
                <span className="ml-auto font-mono text-xs tnum text-ink-500">{stageItems.length}</span>
              </header>

              <div className="max-h-[calc(100vh-260px)] min-h-[120px] flex-1 space-y-2 overflow-y-auto p-2">
                {isLoading ? (
                  <>
                    <CardSkeleton />
                    <CardSkeleton />
                  </>
                ) : stageItems.length === 0 ? (
                  <p className="p-2 text-sm text-ink-600">
                    {EMPTY_HINT[stage] ?? 'Nobody here yet.'}
                  </p>
                ) : (
                  stageItems.map((candidate) => (
                    <article
                      key={candidate.id}
                      onClick={() => onSelectCandidate?.(candidate.candidateId || candidate.id)}
                      className="cursor-pointer rounded-md border border-line bg-white p-2.5 transition-colors hover:border-line-strong"
                    >
                      <p className="text-base font-medium leading-5">{candidate.candidateName}</p>
                      <p className="mt-0.5 truncate text-sm text-ink-600">
                        {candidate.email || candidate.phone || 'No contact specified'}
                      </p>

                      {candidate.coverNote && (
                        <p className="mt-2 line-clamp-2 rounded-md border border-line bg-canvas p-2 text-sm text-ink-600">
                          &ldquo;{candidate.coverNote}&rdquo;
                        </p>
                      )}

                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        {candidate.source && (
                          <span className="inline-flex h-5 items-center rounded-full border border-line bg-canvas px-2 text-2xs text-ink-600">
                            {candidate.source}
                          </span>
                        )}
                        <span className="font-mono text-2xs tnum text-ink-500">
                          {candidate.appliedAt
                            ? new Date(candidate.appliedAt).toLocaleDateString(undefined, {
                                day: 'numeric',
                                month: 'short',
                              })
                            : '—'}
                        </span>
                      </div>

                      {!isTerminal && onMoveStage && (
                        <div className="mt-2 border-t border-line pt-2" onClick={(e) => e.stopPropagation()}>
                          <select
                            aria-label={`Move ${candidate.candidateName} to stage`}
                            className="h-7 w-full rounded-md border border-line bg-white px-1.5 text-sm text-ink-600
                              transition-colors hover:border-line-strong focus:border-brand-700 focus:outline-none"
                            value=""
                            disabled={isMoving}
                            onChange={(e) => {
                              if (e.target.value) {
                                onMoveStage(candidate.id, e.target.value as PipelineStatus);
                              }
                            }}
                          >
                            <option value="">Move to…</option>
                            {PIPELINE_STAGES.filter((s) => s !== candidate.status).map((s) => (
                              <option key={s} value={s}>
                                {s}
                              </option>
                            ))}
                          </select>
                        </div>
                      )}
                    </article>
                  ))
                )}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}
