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

// Text on a tint uses the -700 step, not -600. The -600/-100 pairs the design system used to
// mandate do not reach 4.5:1 at this size — measured 2026-08-17: warning 2.97, success 3.62,
// danger 4.08, info 4.23. `ink-400` on `surface-50` (2.77) is out for the same reason.
const STYLES: Record<string, string> = {
  // Candidate pipeline
  Sourced: 'bg-surface-50 text-ink-600',
  Applied: 'bg-info-100 text-info-700',
  Screening: 'bg-info-100 text-info-700',
  Shortlisted: 'bg-primary-100 text-primary-700',
  Interview: 'bg-warning-100 text-warning-700',
  Offer: 'bg-accent-100 text-accent-700',
  Hired: 'bg-success-100 text-success-700',
  // Requisition lifecycle
  Draft: 'bg-surface-50 text-ink-600',
  PendingApproval: 'bg-warning-100 text-warning-700',
  Approved: 'bg-success-100 text-success-700',
  Cancelled: 'bg-surface-50 text-ink-600',
  // Job posting lifecycle ('Draft' is shared with the requisition lifecycle above)
  Live: 'bg-success-100 text-success-700',
  Closed: 'bg-surface-50 text-ink-600',
  // Interview lifecycle ('Cancelled' is shared with the requisition lifecycle above).
  // NoShow is warning rather than danger: the candidate not turning up is a fact to record,
  // not a failure to flag red at a recruiter.
  Scheduled: 'bg-info-100 text-info-700',
  Completed: 'bg-success-100 text-success-700',
  NoShow: 'bg-warning-100 text-warning-700',
  // Shared
  Rejected: 'bg-danger-100 text-danger-700',
};

// Insert a space before capitals so "PendingApproval" reads as "Pending Approval",
// without inventing a second vocabulary to keep in sync.
function humanise(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export function StatusPill({ status }: { status: Status | string }) {
  const styleClass = STYLES[status] || 'bg-surface-50 text-ink-600';
  return (
    <span
      className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-[13px] font-semibold ${styleClass}`}
    >
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {humanise(status)}
    </span>
  );
}

