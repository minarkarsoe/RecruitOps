import type {
  InterviewStatus, JobStatus, PipelineStatus, RequisitionStatus,
} from '@recruitops/types';

// Signature component (design system §5.2). Fixed vocabulary — never invent labels.
// Covers the candidate pipeline, the requisition lifecycle, the job-posting lifecycle AND
// the interview lifecycle so one component carries the whole status language. Adding a
// status type here is cheaper than the alternative: a page-local badge that drifts from the
// design system the first time a colour changes.
//
// The vocabulary is exactly the four backend enums and nothing else. An
// `ExtendedStatusVocabulary` used to live here carrying `Sent to Client`, `Placed`,
// `Accepted`, `Need More Info`, `Active`, `Expiring Soon` and `Expired` — every one of them
// an agency-era client-feedback or contract label that ADR-0001 deleted from the domain in
// July 2026. They survived here for a month because the type was additive and nothing
// rendered them. Do not reintroduce a free-form extension point: a label with no enum behind
// it is a status the product cannot actually be in.
export type StatusPillVocabulary =
  | PipelineStatus
  | RequisitionStatus
  | JobStatus
  | InterviewStatus;

type Status = StatusPillVocabulary;

// V1.0 (ADR-0025), built against `design/internal/components.html` → "Status pills".
//
// Text on a tint uses the -700 step, never -600. That rule came from measurement, not taste:
// the -600/-100 pairs the old design system mandated do not reach 4.5:1 at this size —
// measured 2026-08-17: warning 2.97, success 3.62, danger 4.08, info 4.23.
//
// The kit moves the tint from -100 to -50, so every pair was re-measured 2026-08-21 rather
// than assumed. All six pass AA, and the lighter tint improves each one:
//
//   ink-600      on canvas        7.24     brand-800    on brand-50      7.27
//   info-700     on info-50       6.16     critical-700 on critical-50   5.91
//   positive-700 on positive-50   5.21     warn-700     on warn-50       4.84  ← tightest
//
// warn is the pair with the least headroom. If a future palette darkens `warn-50` or lightens
// `warn-700`, that is the one that breaks first — re-measure before changing either.
const NEUTRAL = 'bg-canvas border border-line text-ink-600';

const STYLES: Record<string, string> = {
  // Candidate pipeline
  Sourced: NEUTRAL,
  Applied: 'bg-info-50 text-info-700',
  Screening: 'bg-info-50 text-info-700',
  Shortlisted: 'bg-brand-50 text-brand-800',
  Interview: 'bg-warn-50 text-warn-700',
  Offer: 'bg-warn-50 text-warn-700',
  Hired: 'bg-positive-50 text-positive-700',
  // Requisition lifecycle
  Draft: NEUTRAL,
  PendingApproval: 'bg-warn-50 text-warn-700',
  Approved: 'bg-positive-50 text-positive-700',
  Cancelled: NEUTRAL,
  // Job posting lifecycle ('Draft' is shared with the requisition lifecycle above)
  Live: 'bg-positive-50 text-positive-700',
  Closed: NEUTRAL,
  // Interview lifecycle ('Cancelled' is shared with the requisition lifecycle above).
  // NoShow is warn rather than critical: the candidate not turning up is a fact to record,
  // not a failure to flag red at a recruiter.
  Scheduled: 'bg-info-50 text-info-700',
  Completed: 'bg-positive-50 text-positive-700',
  NoShow: 'bg-warn-50 text-warn-700',
  // Shared
  Rejected: 'bg-critical-50 text-critical-700',
};

// Insert a space before capitals so "PendingApproval" reads as "Pending Approval",
// without inventing a second vocabulary to keep in sync.
function humanise(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export function StatusPill({ status }: { status: Status | string }) {
  // An unknown status falls back to neutral rather than throwing. It is a label the backend
  // sent that this build does not know yet — showing it plainly beats a blank space.
  const styleClass = STYLES[status] || NEUTRAL;
  return (
    <span
      className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-xs font-medium ${styleClass}`}
    >
      {/* Never colour alone — the dot is reinforcement, the label carries the meaning. */}
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {humanise(status)}
    </span>
  );
}

