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

const DEFAULT_STAGES: PipelineStageItem[] = [
  { label: 'Sourced', count: 24, status: 'Sourced' },
  { label: 'Shortlisted', count: 8, status: 'Shortlisted' },
  { label: 'Sent to Client', count: 5, status: 'Sent to Client' },
  { label: 'Interview', count: 2, status: 'Interview' },
  { label: 'Placed', count: 1, status: 'Placed' },
];

/**
 * Signature Component: Pipeline Stage Rail (Design System §6.1).
 * Horizontal row of stage counts at top of job order or pipeline views:
 * Sourced 24 → Shortlisted 8 → Sent to Client 5 → Interview 2 → Placed 1
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
      className={`flex flex-wrap items-center gap-2 rounded-xl bg-surface-0 p-3 shadow-card border border-line-200 ${className}`}
    >
      {stages.map((stage, index) => {
        const isActive = activeStage === stage.label || activeStage === stage.status;

        return (
          <div key={stage.id || stage.label} className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => onStageClick?.(stage.label)}
              className={`inline-flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-primary-600 ${
                isActive
                  ? 'bg-primary-100 text-primary-700 ring-1 ring-primary-600 font-semibold'
                  : 'bg-surface-50 text-ink-600 hover:bg-line-200'
              }`}
            >
              <span>{stage.label}</span>
              <span className="font-mono text-xs font-semibold px-1.5 py-0.5 rounded bg-surface-0 border border-line-200">
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
