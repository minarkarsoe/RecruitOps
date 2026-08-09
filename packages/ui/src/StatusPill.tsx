import type {
  InterviewStatus, JobStatus, PipelineStatus, RequisitionStatus,
} from '@recruitops/types';

// Signature component (design system §5.2). Fixed vocabulary — never invent labels.
// Covers the candidate pipeline, the requisition lifecycle, the job-posting lifecycle AND
// the interview lifecycle so one component carries the whole status language. Adding a
// status type here is cheaper than the alternative: a page-local badge that drifts from the
// design system the first time a colour changes.
export type ExtendedStatusVocabulary =
  | 'Sent to Client' | 'SentToClient'
  | 'Placed'
  | 'Accepted'
  | 'Need More Info' | 'NeedMoreInfo'
  | 'Active'
  | 'Expiring Soon' | 'ExpiringSoon'
  | 'Expired';

export type StatusPillVocabulary =
  | PipelineStatus
  | RequisitionStatus
  | JobStatus
  | InterviewStatus
  | ExtendedStatusVocabulary;

type Status = StatusPillVocabulary;

const STYLES: Record<string, string> = {
  // Candidate pipeline
  Sourced: 'bg-surface-50 text-ink-600',
  Applied: 'bg-info-100 text-info-600',
  Screening: 'bg-info-100 text-info-600',
  Shortlisted: 'bg-primary-100 text-primary-700',
  Interview: 'bg-warning-100 text-warning-600',
  Offer: 'bg-accent-100 text-warning-600',
  Hired: 'bg-success-100 text-success-600',
  // Requisition lifecycle
  Draft: 'bg-surface-50 text-ink-600',
  PendingApproval: 'bg-warning-100 text-warning-600',
  Approved: 'bg-success-100 text-success-600',
  Cancelled: 'bg-surface-50 text-ink-400',
  // Job posting lifecycle ('Draft' is shared with the requisition lifecycle above)
  Live: 'bg-success-100 text-success-600',
  Closed: 'bg-surface-50 text-ink-400',
  // Interview lifecycle ('Cancelled' is shared with the requisition lifecycle above).
  // NoShow is warning rather than danger: the candidate not turning up is a fact to record,
  // not a failure to flag red at a recruiter.
  Scheduled: 'bg-info-100 text-info-600',
  Completed: 'bg-success-100 text-success-600',
  NoShow: 'bg-warning-100 text-warning-600',
  // Shared
  Rejected: 'bg-danger-100 text-danger-600',
  // Extended vocabulary (Design System §5.2)
  'Sent to Client': 'bg-info-100 text-info-600',
  SentToClient: 'bg-info-100 text-info-600',
  Placed: 'bg-success-100 text-success-600',
  Accepted: 'bg-success-100 text-success-600',
  'Need More Info': 'bg-warning-100 text-warning-600',
  NeedMoreInfo: 'bg-warning-100 text-warning-600',
  Active: 'bg-success-100 text-success-600',
  'Expiring Soon': 'bg-warning-100 text-warning-600',
  ExpiringSoon: 'bg-warning-100 text-warning-600',
  Expired: 'bg-danger-100 text-danger-600',
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

