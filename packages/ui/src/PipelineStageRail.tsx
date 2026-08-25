export interface PipelineStageItem {
  id?: string;
  label: string;
  count: number;
  status?: string;
}

export interface PipelineStageRailProps {
  stages?: PipelineStageItem[];
  activeStage?: string;
  onStageClick?: (stageLabel: string) => void;
  className?: string;
}

// The in-house funnel, in `PipelineStatus` order. `Rejected` is deliberately absent: it is a
// terminal exit from the funnel, not a stage along it, and putting it in the rail would imply
// candidates flow into it from `Hired`. These defaults previously read
// `Sourced → Shortlisted → Sent to Client → Interview → Placed`, two of which are agency-era
// labels ADR-0001 deleted.
const DEFAULT_STAGES: PipelineStageItem[] = [
  { label: 'Sourced', count: 24, status: 'Sourced' },
  { label: 'Applied', count: 18, status: 'Applied' },
  { label: 'Screening', count: 12, status: 'Screening' },
  { label: 'Shortlisted', count: 8, status: 'Shortlisted' },
  { label: 'Interview', count: 4, status: 'Interview' },
  { label: 'Offer', count: 2, status: 'Offer' },
  { label: 'Hired', count: 1, status: 'Hired' },
];

/**
 * Signature Component: Pipeline Stage Rail (Design System §6.1).
 * Horizontal row of stage counts at the top of a job posting or pipeline view:
 * Sourced 24 → Applied 18 → Screening 12 → Shortlisted 8 → Interview 4 → Offer 2 → Hired 1
 */
export function PipelineStageRail({
  stages = DEFAULT_STAGES,
  activeStage,
  onStageClick,
  className = '',
}: PipelineStageRailProps) {
  return (
    <div
      aria-label="Pipeline Stages"
      className={`flex flex-wrap items-center gap-2 rounded-xl bg-white p-3 shadow-card border border-line ${className}`}
    >
      {stages.map((stage, index) => {
        const isActive = activeStage === stage.label || activeStage === stage.status;

        return (
          <div key={stage.id || stage.label} className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => onStageClick?.(stage.label)}
              className={`inline-flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-brand-600 ${
                isActive
                  ? 'bg-brand-100 text-brand-700 ring-1 ring-brand-600 font-semibold'
                  : 'bg-canvas text-ink-600 hover:bg-line'
              }`}
            >
              <span>{stage.label}</span>
              <span className="font-mono text-xs font-semibold px-1.5 py-0.5 rounded bg-white border border-line">
                {stage.count}
              </span>
            </button>

            {index < stages.length - 1 && (
              <span className="text-ink-400 font-bold select-none" aria-hidden="true">
                →
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
}
