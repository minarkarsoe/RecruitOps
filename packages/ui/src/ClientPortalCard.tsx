import { useState } from 'react';
import { StatusPill } from './StatusPill';
import { Button } from './Button';

export type ClientFeedbackStatus = 'Accepted' | 'Need More Info' | 'Rejected';

export interface ClientFeedbackBarProps {
  selectedStatus?: ClientFeedbackStatus | null;
  onSelectStatus?: (status: ClientFeedbackStatus) => void;
  className?: string;
}

/**
 * Signature Component: Client Feedback Bar (Design System §6.2).
 * Three feedback buttons on each candidate card in the client portal (44px height row):
 * - Accept for Interview (solid success-600)
 * - Need More Info (secondary with warning-600 text)
 * - Reject (ghost with danger-600 text)
 */
export function ClientFeedbackBar({
  selectedStatus,
  onSelectStatus,
  className = '',
}: ClientFeedbackBarProps) {
  if (selectedStatus) {
    return (
      <div className={`flex h-11 items-center justify-between rounded-xl bg-surface-50 px-4 border border-line-200 ${className}`}>
        <div className="flex items-center gap-2">
          <span className="text-xs text-ink-600 font-medium">Feedback recorded:</span>
          <StatusPill status={selectedStatus} />
        </div>
        <button
          type="button"
          onClick={() => onSelectStatus?.(selectedStatus)}
          className="text-xs font-semibold text-primary-600 hover:text-primary-700 underline focus:outline-none"
        >
          Change
        </button>
      </div>
    );
  }

  return (
    <div className={`flex flex-col sm:flex-row items-stretch gap-2.5 w-full ${className}`}>
      <button
        type="button"
        onClick={() => onSelectStatus?.('Accepted')}
        className="flex-1 h-11 px-4 inline-flex items-center justify-center rounded-xl bg-success-600 text-white font-semibold text-sm hover:bg-success-700 transition-colors focus:outline-none focus:ring-2 focus:ring-success-600"
      >
        Accept for Interview
      </button>

      <button
        type="button"
        onClick={() => onSelectStatus?.('Need More Info')}
        className="flex-1 h-11 px-4 inline-flex items-center justify-center rounded-xl bg-white border border-line-200 text-warning-600 font-semibold text-sm hover:bg-surface-50 transition-colors focus:outline-none focus:ring-2 focus:ring-warning-600"
      >
        Need More Info
      </button>

      <button
        type="button"
        onClick={() => onSelectStatus?.('Rejected')}
        className="h-11 px-4 inline-flex items-center justify-center rounded-xl bg-transparent text-danger-600 font-semibold text-sm hover:bg-danger-100 transition-colors focus:outline-none focus:ring-2 focus:ring-danger-600"
      >
        Reject
      </button>
    </div>
  );
}

export interface ClientPortalCandidate {
  id: string;
  name: string;
  role: string;
  avatarUrl?: string;
  experience?: string;
  expectedSalary?: string;
  noticePeriod?: string;
  location?: string;
  summary?: string;
  skills?: string[];
  cvUrl?: string;
  status?: ClientFeedbackStatus | null;
}

export interface ClientPortalCardProps {
  candidate: ClientPortalCandidate;
  onFeedback?: (status: ClientFeedbackStatus) => void;
  onViewCv?: (candidate: ClientPortalCandidate) => void;
  className?: string;
}

function getInitials(name: string): string {
  return name
    .split(' ')
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

/**
 * Signature Component: Client Portal Candidate Card (Design System §6.3).
 * Premium surface candidate card: white card, radius 16, padding 32, avatar 56,
 * quiet fact chips, skills row, CV button, and integrated ClientFeedbackBar.
 */
export function ClientPortalCard({
  candidate,
  onFeedback,
  onViewCv,
  className = '',
}: ClientPortalCardProps) {
  const [currentStatus, setCurrentStatus] = useState<ClientFeedbackStatus | null>(
    candidate.status || null
  );

  const handleSelectStatus = (status: ClientFeedbackStatus) => {
    setCurrentStatus(status);
    onFeedback?.(status);
  };

  const quietChips = [
    candidate.experience && { label: 'Experience', value: candidate.experience },
    candidate.expectedSalary && { label: 'Expected Salary', value: candidate.expectedSalary },
    candidate.noticePeriod && { label: 'Notice Period', value: candidate.noticePeriod },
    candidate.location && { label: 'Location', value: candidate.location },
  ].filter(Boolean) as Array<{ label: string; value: string }>;

  return (
    <div
      className={`rounded-2xl bg-surface-0 p-8 shadow-card border border-line-200 flex flex-col gap-6 ${className}`}
    >
      {/* Header: Avatar 56 + Name + Role */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-4">
          {candidate.avatarUrl ? (
            <img
              src={candidate.avatarUrl}
              alt={candidate.name}
              className="w-14 h-14 rounded-full object-cover border border-line-200"
            />
          ) : (
            <div className="w-14 h-14 rounded-full bg-primary-100 text-primary-700 font-bold text-lg flex items-center justify-center font-mono">
              {getInitials(candidate.name)}
            </div>
          )}
          <div>
            <h2 className="text-xl font-bold text-ink-900 tracking-tight">
              {candidate.name}
            </h2>
            <p className="text-sm font-medium text-ink-600">{candidate.role}</p>
          </div>
        </div>

        {candidate.status && (
          <StatusPill status={candidate.status} />
        )}
      </div>

      {/* Quiet Chips Row */}
      {quietChips.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {quietChips.map((chip) => (
            <div
              key={chip.label}
              className="inline-flex items-center gap-1.5 rounded-md bg-surface-50 px-3 py-1 text-xs font-medium text-ink-600 border border-line-200"
            >
              <span className="text-ink-400">{chip.label}:</span>
              <span className="font-semibold text-ink-900">{chip.value}</span>
            </div>
          ))}
        </div>
      )}

      {/* Summary / Bio */}
      {candidate.summary && (
        <p className="text-sm text-ink-600 leading-relaxed">{candidate.summary}</p>
      )}

      {/* Skills Row */}
      {candidate.skills && candidate.skills.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {candidate.skills.map((skill) => (
            <span
              key={skill}
              className="inline-flex items-center rounded-full bg-primary-100 px-2.5 py-0.5 text-xs font-medium text-primary-700"
            >
              {skill}
            </span>
          ))}
        </div>
      )}

      {/* CV Button Row */}
      <div className="flex items-center justify-between border-t border-line-200 pt-4">
        <Button
          variant="secondary"
          className="h-8 px-3 text-xs gap-2"
          onClick={() => onViewCv?.(candidate)}
        >
          <svg className="w-4 h-4 text-ink-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
          View Attached CV
        </Button>
      </div>

      {/* Client Feedback Bar */}
      <div className="pt-2">
        <ClientFeedbackBar
          selectedStatus={currentStatus}
          onSelectStatus={handleSelectStatus}
        />
      </div>
    </div>
  );
}
