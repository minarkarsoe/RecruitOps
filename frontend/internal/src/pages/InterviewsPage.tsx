import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import type { InterviewListItem, InterviewStatus } from '@recruitops/types';
import { api } from '../lib/api';

// Built against `design/internal/interviews.html` (ADR-0025).
//
// The screen exists because the kit's rail has carried an "Interviews" item since the design was
// drawn and there was nothing behind it: `/interviews/:id` existed, a list did not, and no rail
// entry pointed anywhere. A round could only be reached by opening a posting, expanding a
// candidate row and clicking through — so "which interviews am I sitting on this week, and whose
// scorecard is late" had no answer anywhere in the product.
//
// Two rules from the design carry the weight:
//
//   1. Panel PROGRESS is public, panel OPINIONS are not. "1 of 3 in" is here on purpose — the
//      panel is meant to know who is holding up the debrief. Nothing on this screen shows a
//      rating or a recommendation, and the API deliberately does not send one: reading an
//      evaluation goes through the detail screen, where the blind rule (ADR-0017 §3) is applied.
//      A "recommendation" column here would route around it.
//
//   2. Concluded rounds are kept and hidden. Cancelled and Completed both stay in the record —
//      a cancelled round is the reason a candidate was asked to move twice — and both are out of
//      the default view, where they would bury the rounds that still need something. Each is one
//      click away on its own tab.
//
//      ⚠️ Until 2026-08-29 the default excluded only Cancelled, so this tab — labelled
//      "Upcoming" — returned exactly the same rows as "All", completed rounds included. The
//      behaviour was deliberate and pinned by a test on both sides; the LABEL is what nobody had
//      compared it to, and it took thirty seconds of using the screen to see. The cut is
//      "concluded" rather than a date on purpose: a Scheduled round whose time has passed is not
//      upcoming in the calendar sense, but it is the one someone has to chase, and a date filter
//      would hide precisely the work that needs doing.

/** In the order the design draws them. `all` is last because it is the escape hatch, not a view. */
const STATUS_FILTERS: { value: InterviewStatus | 'default' | 'all'; label: string }[] = [
  { value: 'default', label: 'Upcoming' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'all', label: 'All' },
];

const STATUS_PILL: Record<InterviewStatus, { className: string; dot: string }> = {
  Scheduled: { className: 'bg-brand-50 text-brand-800', dot: 'bg-current' },
  Completed: { className: 'border border-line bg-canvas text-ink-600', dot: 'bg-ink-400' },
  // Neutral, not red. A cancelled round is usually the process working — a candidate withdrew,
  // a slot moved — and colouring a correct outcome as an error is how people learn to ignore
  // the colour that means something. Same decision as `Suppressed` on the delivery log.
  Cancelled: { className: 'border border-line bg-canvas text-ink-500', dot: 'bg-ink-400' },
  NoShow: { className: 'bg-warn-50 text-warn-700', dot: 'bg-current' },
};

const MODE_LABEL: Record<string, string> = { OnSite: 'Onsite', Video: 'Online', Phone: 'Phone' };

/** Panel progress. Amber only when something is genuinely outstanding — a full panel is quiet. */
function panelPill(item: InterviewListItem): string {
  if (item.panelSize > 0 && item.submittedCount === item.panelSize) {
    return 'bg-positive-50 text-positive-700';
  }
  return item.submittedCount > 0
    ? 'bg-warn-50 text-warn-700'
    : 'border border-line bg-canvas text-ink-600';
}

function whenLabel(iso: string): { day: string; time: string } {
  const date = new Date(iso);
  const now = new Date();
  const sameDay =
    date.getFullYear() === now.getFullYear() &&
    date.getMonth() === now.getMonth() &&
    date.getDate() === now.getDate();

  return {
    day: sameDay
      ? 'Today'
      : date.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }),
    time: date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }),
  };
}

export function InterviewsPage() {
  const [filter, setFilter] = useState<InterviewStatus | 'default' | 'all'>('default');
  const [onlyMine, setOnlyMine] = useState(false);
  const [items, setItems] = useState<InterviewListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      // `default` sends nothing and lets the API apply its own rule — everything except
      // Cancelled. Restating that list here would mean a status added later shows up in one
      // place and not the other.
      if (filter === 'all') {
        for (const s of ['Scheduled', 'Completed', 'Cancelled', 'NoShow']) params.append('status', s);
      } else if (filter !== 'default') {
        params.append('status', filter);
      }
      if (onlyMine) params.set('onlyMine', 'true');

      const query = params.toString();
      setItems(await api<InterviewListItem[]>(`/interviews${query ? `?${query}` : ''}`));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load interviews.');
    } finally {
      setLoading(false);
    }
  }, [filter, onlyMine]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <>
      <header className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Interviews</h1>
          <p className="mt-1 max-w-[74ch] text-sm text-ink-600">
            Rounds in your departments, and any panel you are sitting on. Interviews are scheduled
            against a candidate, from the board or a posting.
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

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div
          className="flex w-fit items-center rounded-md border border-line p-0.5"
          role="group"
          aria-label="Filter by status"
        >
          {STATUS_FILTERS.map((f) => (
            <button
              key={f.value}
              type="button"
              onClick={() => setFilter(f.value)}
              aria-pressed={filter === f.value}
              className={`h-7 rounded px-2.5 text-sm transition-colors ${
                filter === f.value
                  ? 'bg-ink-900 font-medium text-white'
                  : 'text-ink-600 hover:text-ink-900'
              }`}
            >
              {f.label}
            </button>
          ))}
        </div>

        <label className="flex cursor-pointer items-center gap-2 text-sm text-ink-600">
          <input
            type="checkbox"
            checked={onlyMine}
            onChange={(e) => setOnlyMine(e.target.checked)}
            className="h-4 w-4 rounded border-line text-brand-700 focus:ring-brand-700/20"
          />
          Only mine
        </label>

        <span className="ml-auto text-sm text-ink-600 tnum">
          {loading ? '…' : `${items.length} ${items.length === 1 ? 'interview' : 'interviews'}`}
        </span>
      </div>

      {error && (
        <div className="mb-4 rounded-md border border-critical-500/25 bg-critical-50 px-4 py-3 text-base text-critical-700">
          {error}
        </div>
      )}

      {!loading && !error && items.length === 0 && (
        // Teaches where interviews come from rather than announcing absence: an interviewer with
        // no rounds this week has done nothing wrong and must not be shown something that reads
        // like an error.
        <div className="rounded-lg border border-line bg-white p-16 text-center">
          <p className="text-base font-medium">No interviews here</p>
          <p className="mx-auto mt-1.5 max-w-[46ch] text-base text-ink-600">
            Interviews are scheduled against a candidate, from the board or a posting. Nothing in
            your departments matches this filter.
          </p>
        </div>
      )}

      {items.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-line bg-white">
          <table className="w-full min-w-[900px] text-base">
            <thead>
              <tr className="border-b border-line bg-canvas text-left text-sm">
                <th className="px-5 py-2.5 font-medium text-ink-600">Candidate</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Department</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">When</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Panel</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Status</th>
                <th className="px-5 py-2.5 font-medium text-ink-600" />
              </tr>
            </thead>
            <tbody>
              {items.map((item) => {
                const when = whenLabel(item.scheduledStart);
                const pill = STATUS_PILL[item.status];

                return (
                  <tr key={item.id} className="border-b border-line transition-colors last:border-0 hover:bg-canvas">
                    <td className="px-5 py-3">
                      <span className="font-medium">{item.candidateName}</span>
                      <span className="block text-sm text-ink-600">
                        {item.jobPostingTitle} · <span className="tnum">Round {item.round}</span>
                      </span>
                    </td>
                    <td className="px-5 py-3 text-ink-600">{item.departmentName}</td>
                    <td className="px-5 py-3">
                      <span className="font-mono text-sm tnum">
                        {when.day} · {when.time}
                      </span>
                      <span className="block text-xs text-ink-600 tnum">
                        {item.durationMinutes} min · {MODE_LABEL[item.mode] ?? item.mode}
                      </span>
                    </td>
                    <td className="px-5 py-3">
                      <span
                        className={`inline-flex h-6 items-center rounded-full px-2.5 text-xs font-medium ${panelPill(item)}`}
                      >
                        {item.submittedCount} of {item.panelSize} in
                      </span>
                      {item.myScorecardOutstanding ? (
                        // The one actionable state on this screen, and nothing else in the
                        // product surfaces it.
                        <span className="mt-1 block text-xs font-medium text-warn-700">
                          Yours is not in
                        </span>
                      ) : (
                        <span className="mt-1 block truncate text-xs text-ink-600">
                          {item.panelNames.join(', ')}
                        </span>
                      )}
                    </td>
                    <td className="px-5 py-3">
                      <span
                        className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-xs font-medium ${pill.className}`}
                      >
                        <span className={`h-1.5 w-1.5 rounded-full ${pill.dot}`} />
                        {item.status}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-right">
                      <Link
                        to={`/interviews/${item.id}`}
                        className="text-sm font-medium text-brand-700 hover:text-brand-800"
                      >
                        {item.myScorecardOutstanding ? 'Score' : 'Open'}
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
