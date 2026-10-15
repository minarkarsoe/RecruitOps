import { useCallback, useEffect, useState } from 'react';
import type { DeliveryLogEntry, OutboundMessageStatus, PagedResult } from '@recruitops/types';
import { api } from '../lib/api';

// Built against the Delivery log section of `design/internal/channels.html` (ADR-0025).
//
// The screen exists because the outbox has recorded every send since 2026-08-20 and nothing
// rendered it: a Failed invitation — wrong address, dead relay — was written down faithfully and
// shown to nobody, so "was this candidate told?" was answerable only from a psql prompt. Silence
// is the failure mode that costs a hire.
//
// The design's one real rule, and the reason this is not just a table:
//
//   "A failure row says what to do next. 'Failed' alone makes a recruiter open a support ticket."
//
// So the reason sits under the pill, in the recruiter's terms, and it comes from the server —
// the handlers already write for a human ("The round was cancelled before the invitation went
// out — the candidate was not emailed"), which is why nothing here invents wording.

/** Kept in the same order the design draws them: the failures first, then the rest. */
const STATUS_FILTERS: { value: OutboundMessageStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Pending', label: 'Waiting' },
  { value: 'Sent', label: 'Delivered' },
  { value: 'Suppressed', label: 'Not sent' },
];

/**
 * ⚠️ `Suppressed` is NEUTRAL, not red, and that is a decision rather than a palette choice.
 * An opt-out honoured, or an invitation dropped because the round was cancelled, is the system
 * doing the right thing. ADR-0026 made it a first-class status precisely so this screen would
 * not colour it as an error — rendering a correct outcome in red is how recruiters learn to
 * ignore the colour that means something.
 *
 * `Pending` is neutral for a different reason: nothing has gone wrong yet.
 */
const STATUS_PILL: Record<string, { label: string; className: string; dot: string }> = {
  Sent: {
    label: 'Delivered',
    className: 'bg-positive-50 text-positive-700',
    dot: 'bg-current',
  },
  Failed: {
    label: 'Failed',
    className: 'bg-critical-50 text-critical-700',
    dot: 'bg-current',
  },
  Suppressed: {
    label: 'Not sent',
    className: 'border border-line bg-canvas text-ink-600',
    dot: 'bg-ink-400',
  },
  Pending: {
    label: 'Waiting',
    className: 'border border-line bg-canvas text-ink-600',
    dot: 'bg-ink-400',
  },
};

function StatusCell({ entry }: { entry: DeliveryLogEntry }) {
  const pill = STATUS_PILL[entry.status] ?? {
    label: entry.status,
    className: 'border border-line bg-canvas text-ink-600',
    dot: 'bg-ink-400',
  };

  // A failure's reason is critical-coloured; anything else's is quiet. "Not sent — opted out" is
  // information, not an alarm.
  const reasonClass = entry.status === 'Failed' ? 'text-critical-700' : 'text-ink-600';

  return (
    <>
      <span
        className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-xs font-medium ${pill.className}`}
      >
        <span className={`h-1.5 w-1.5 rounded-full ${pill.dot}`} aria-hidden="true" />
        {pill.label}
      </span>
      {entry.lastError && (
        <span className={`mt-1 block text-xs ${reasonClass}`}>{entry.lastError}</span>
      )}
      {/* Only while something is still going to happen. A terminal row has no nextAttemptAt —
          the server strips it — so this cannot promise a retry that is never coming. */}
      {entry.status === 'Pending' && entry.attempts > 0 && (
        <span className="mt-1 block text-xs text-ink-600">
          Attempt {entry.attempts} did not go through. Trying again automatically.
        </span>
      )}
    </>
  );
}

function formatWhen(entry: DeliveryLogEntry): string {
  const raw = entry.sentAt ?? entry.createdAt;
  const date = new Date(raw);
  if (Number.isNaN(date.getTime())) return '—';

  const today = new Date();
  const sameDay =
    date.getFullYear() === today.getFullYear() &&
    date.getMonth() === today.getMonth() &&
    date.getDate() === today.getDate();

  // The design shows a bare time, because its rows are all from today. A log that goes back
  // weeks and shows "14:06" on every row is a log you cannot read, so older rows carry a date.
  return sameDay
    ? date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
    : date.toLocaleString(undefined, {
        day: 'numeric',
        month: 'short',
        hour: '2-digit',
        minute: '2-digit',
      });
}

export function DeliveryLogPage() {
  const [status, setStatus] = useState<OutboundMessageStatus | 'all'>('all');
  const [result, setResult] = useState<PagedResult<DeliveryLogEntry> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const query = status === 'all' ? '' : `?status=${status}`;
      setResult(await api<PagedResult<DeliveryLogEntry>>(`/delivery${query}`));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load the delivery log.');
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => {
    load();
  }, [load]);

  const entries = result?.items ?? [];

  return (
    <>
      <header className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Delivery log</h1>
          <p className="mt-1 max-w-[74ch] text-sm text-ink-600">
            Everything the product has told a candidate, and whether it arrived. When a message
            does not get through, the recruiter needs to know the candidate was not told — silence
            is the failure mode that costs a hire.
          </p>
        </div>

        <button
          type="button"
          onClick={load}
          disabled={loading}
          className="h-9 shrink-0 rounded-md border border-line bg-white px-3.5 text-base font-medium
            transition-colors hover:border-line-strong disabled:text-ink-400"
        >
          {loading ? 'Refreshing…' : 'Refresh'}
        </button>
      </header>

      {/* The kit's detented filter group. Every position is a labelled state that snaps — no
          free text, because "which of these went wrong" has exactly five answers. */}
      <div
        className="mb-4 flex w-fit items-center rounded-md border border-line p-0.5"
        role="group"
        aria-label="Filter by result"
      >
        {STATUS_FILTERS.map((filter) => (
          <button
            key={filter.value}
            type="button"
            aria-pressed={status === filter.value}
            onClick={() => setStatus(filter.value)}
            className={`h-7 rounded px-2.5 text-sm transition-colors ${
              status === filter.value
                ? 'bg-ink-900 font-medium text-white'
                : 'text-ink-600 hover:text-ink-900'
            }`}
          >
            {filter.label}
          </button>
        ))}
      </div>

      {error && (
        <div role="alert" className="mb-4 rounded-md border border-critical-100 bg-critical-50 p-3 text-sm text-critical-700">
          {error}
        </div>
      )}

      {loading && !result && (
        <div className="space-y-2" data-testid="delivery-log-skeleton">
          <span className="skeleton block h-10 w-full" />
          <span className="skeleton block h-10 w-full" />
          <span className="skeleton block h-10 w-full" />
        </div>
      )}

      {result && entries.length === 0 && !error && (
        <div className="rounded-lg border border-line bg-white p-8 text-center">
          <p className="text-base font-medium">
            {status === 'all' ? 'Nothing has been sent yet' : 'Nothing here'}
          </p>
          <p className="mx-auto mt-1 max-w-[52ch] text-sm text-ink-600">
            {status === 'all'
              ? 'Messages appear here as soon as the product has something to tell a candidate — the first one is the invitation sent when you schedule an interview.'
              : 'No message has that result. Try “All”.'}
          </p>
        </div>
      )}

      {entries.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-line">
          <table className="w-full min-w-[760px] bg-white text-left text-base">
            <thead>
              <tr className="border-b border-line bg-canvas text-left text-sm">
                <th className="px-5 py-2.5 font-medium text-ink-600">When</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Candidate</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Message</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Channel</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Result</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={entry.id} className="border-b border-line last:border-b-0">
                  <td className="px-5 py-3 tnum text-ink-600">{formatWhen(entry)}</td>
                  <td className="px-5 py-3">
                    {/* `.mm` because a candidate's name is very often Burmese, and Burmese
                        diacritics clip at the 20px line box the rest of the table uses. */}
                    <span className="mm block">{entry.candidateName ?? '—'}</span>
                    <span className="block text-xs text-ink-500">{entry.recipient}</span>
                  </td>
                  <td className="px-5 py-3 text-ink-700">{entry.kindLabel}</td>
                  <td className="px-5 py-3 text-ink-600">{entry.channel}</td>
                  <td className="px-5 py-3">
                    <StatusCell entry={entry} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {result && result.totalCount > entries.length && (
        <p className="mt-3 text-sm text-ink-600">
          Showing the {entries.length} most recent of{' '}
          <span className="tnum">{result.totalCount}</span>.
        </p>
      )}
    </>
  );
}
