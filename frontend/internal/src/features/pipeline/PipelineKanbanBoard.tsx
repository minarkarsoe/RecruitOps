import { Badge, Input } from '@recruitops/ui';
import type { PipelineItem, PipelineStatus } from '@recruitops/types';

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
  // Group items by stage
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
    <div className={`space-y-4 ${className}`}>
      {/* Board Controls */}
      {onSearchQueryChange && (
        <div className="flex items-center justify-between gap-4 bg-surface-0 p-1">
          <div className="w-72 max-w-full">
            <Input
              type="search"
              placeholder="Search candidate name, email..."
              value={searchQuery}
              onChange={(e) => onSearchQueryChange(e.target.value)}
              className="h-8 text-xs"
            />
          </div>
          <div className="text-xs text-ink-600 font-medium">
            Total Candidates: <span className="font-semibold text-ink-900">{items.length}</span>
          </div>
        </div>
      )}

      {/* Kanban Stages Grid / Horizontal Scroll */}
      <div className="flex gap-4 overflow-x-auto pb-4 pt-1 snap-x">
        {PIPELINE_STAGES.map((stage) => {
          const stageItems = groupedStageMap[stage];
          const isTerminal = stage === 'Hired' || stage === 'Rejected';

          return (
            <div
              key={stage}
              className="flex w-72 shrink-0 flex-col rounded-lg border border-line-200 bg-surface-50 p-3 shadow-card snap-start"
            >
              {/* Column Header */}
              <div className="mb-3 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <h3 className="font-display text-sm font-semibold text-ink-900">{stage}</h3>
                  <Badge
                    variant={stage === 'Hired' ? 'success' : stage === 'Rejected' ? 'danger' : 'cyan'}
                    size="sm"
                  >
                    {stageItems.length}
                  </Badge>
                </div>
              </div>

              {/* Cards list */}
              <div className="flex-1 space-y-3 overflow-y-auto max-h-[calc(100vh-260px)] min-h-[120px] pr-1">
                {isLoading ? (
                  <div className="py-6 text-center text-xs text-ink-400">Loading...</div>
                ) : stageItems.length === 0 ? (
                  <div className="flex h-24 items-center justify-center rounded-md border border-dashed border-line-200 bg-surface-0/50 text-xs text-ink-400">
                    No candidates
                  </div>
                ) : (
                  stageItems.map((candidate) => (
                    <div
                      key={candidate.id}
                      onClick={() => onSelectCandidate?.(candidate.candidateId || candidate.id)}
                      className="group relative cursor-pointer rounded-md border border-line-200 bg-surface-0 p-3.5 shadow-xs transition-all hover:border-primary-600/40 hover:shadow-card"
                    >
                      {/* Candidate Name & Source */}
                      <div className="flex items-start justify-between gap-2">
                        <h4 className="font-semibold text-sm text-ink-900 group-hover:text-primary-600 transition-colors">
                          {candidate.candidateName}
                        </h4>
                        {candidate.source && (
                          <Badge variant="secondary" size="sm">
                            {candidate.source}
                          </Badge>
                        )}
                      </div>

                      {/* Contact Info */}
                      <p className="mt-1 text-xs text-ink-600 truncate">
                        {candidate.email || candidate.phone || 'No contact specified'}
                      </p>

                      {/* Cover Note Excerpt */}
                      {candidate.coverNote && (
                        <p className="mt-2 line-clamp-2 rounded-sm bg-surface-50 p-2 text-xs italic text-ink-600 border border-line-200/60">
                          &ldquo;{candidate.coverNote}&rdquo;
                        </p>
                      )}

                      {/* Applied Date & Quick Stage Movement */}
                      <div className="mt-3 flex items-center justify-between border-t border-line-200/60 pt-2 text-[11px] text-ink-400">
                        <span>
                          {candidate.appliedAt
                            ? new Date(candidate.appliedAt).toLocaleDateString(undefined, {
                                month: 'short',
                                day: 'numeric',
                              })
                            : 'Applied'}
                        </span>

                        {!isTerminal && onMoveStage && (
                          <div onClick={(e) => e.stopPropagation()}>
                            <select
                              aria-label={`Move ${candidate.candidateName} to stage`}
                              className="h-7 rounded border border-line-200 bg-surface-0 px-1.5 text-xs text-ink-700 hover:bg-surface-50 focus:outline-none focus:ring-1 focus:ring-primary-600"
                              value=""
                              disabled={isMoving}
                              onChange={(e) => {
                                if (e.target.value) {
                                  onMoveStage(candidate.id, e.target.value as PipelineStatus);
                                }
                              }}
                            >
                              <option value="">Move stage...</option>
                              {PIPELINE_STAGES.filter((s) => s !== candidate.status).map((s) => (
                                <option key={s} value={s}>
                                  {s}
                                </option>
                              ))}
                            </select>
                          </div>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
