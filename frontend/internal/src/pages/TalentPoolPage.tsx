import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Button, Dialog } from '@recruitops/ui';
import type {
  DuplicateCandidate, DuplicatePair, MergeCandidatesResult,
} from '@recruitops/types';
import { api } from '../lib/api';
import { auth, isRecruitmentStaff } from '../lib/auth';

/**
 * The Talent pool — Module 2.7's possible duplicates, built against
 * `design/internal/talent-pool.html` (the "Possible duplicate" row and "Review" → "Two records,
 * one person?").
 *
 * Only the duplicates part of that screen exists so far: the candidate search, ranking and CV
 * upload drawn above it have no API yet (`GET /api/candidates` is a stub).
 *
 * Recruitment staff only, as on the server (`Policies.RecruitmentStaff`): those roles are
 * company-wide, which is what lets two records of one person from different departments' postings
 * sit side by side here.
 */
export function TalentPoolPage() {
  const session = auth.get();
  const allowed = session !== null && isRecruitmentStaff(session.role);

  const [pairs, setPairs] = useState<DuplicatePair[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [reviewing, setReviewing] = useState<DuplicatePair | null>(null);

  const load = useCallback(async () => {
    setPairs(await api<DuplicatePair[]>('/candidates/duplicates'));
  }, []);

  useEffect(() => {
    if (!allowed) return;
    load().catch((e) => setError(e instanceof Error ? e.message : 'Could not load possible duplicates.'));
  }, [allowed, load]);

  if (!allowed) {
    return (
      <>
        <h1 className="mb-6 text-xl font-semibold tracking-tight">Talent pool</h1>
        <p className="text-md text-ink-600">The Talent pool is open to recruitment staff.</p>
      </>
    );
  }

  async function reload() {
    try {
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not reload possible duplicates.');
    }
  }

  async function decided(message: string) {
    setReviewing(null);
    setNotice(message);
    await reload();
  }

  // Reloaded on "Decide later" too: a refusal in the dialog is most often someone else having
  // decided the pair a moment ago, and the list should stop offering it.
  function closeReview() {
    setReviewing(null);
    void reload();
  }

  return (
    <>
      <div className="mb-6 flex items-center gap-4">
        <h1 className="text-xl font-semibold tracking-tight">Talent pool</h1>
      </div>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}
      {notice && <p role="status" className="mb-4 text-md text-ink-700">{notice}</p>}

      <h2 className="mb-1 text-lg font-semibold">Possible duplicates</h2>
      <p className="mb-4 max-w-[74ch] text-md text-ink-600">
        Two records with the same phone number or email. Applying through a job page reuses a
        match automatically; these were created separately, so a person decides.
      </p>

      {pairs === null && !error && <p className="text-ink-600">Loading…</p>}

      {pairs && pairs.length === 0 && (
        <div className="rounded-lg border border-line bg-white p-16 text-center">
          <p className="text-base font-medium">No possible duplicates</p>
          <p className="mx-auto mt-1.5 max-w-[46ch] text-base text-ink-600">
            Two records that share a phone number or email appear here until someone merges them or
            marks them as different people.
          </p>
        </div>
      )}

      {pairs && pairs.length > 0 && (
        // `relative`: the header's sr-only label is absolutely positioned, and without a positioned
        // ancestor it escapes this scroll box and widens the whole page on a phone (measured).
        <div className="relative overflow-x-auto rounded-lg border border-line bg-white">
          <table className="w-full min-w-[760px] text-base">
            <thead>
              <tr className="border-b border-line bg-canvas text-left text-sm">
                <th className="px-5 py-2.5 font-medium text-ink-600">Candidate</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Same as</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Source</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">History</th>
                <th className="px-5 py-2.5 font-medium text-ink-600"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {pairs.map((pair) => (
                <PairRow
                  key={`${pair.first.id}:${pair.second.id}`}
                  pair={pair}
                  onReview={() => { setNotice(null); setReviewing(pair); }}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {reviewing && (
        <ReviewDialog
          // A fresh dialog per pair, so the choice of record to keep never carries over.
          key={`${reviewing.first.id}:${reviewing.second.id}`}
          pair={reviewing}
          onClose={closeReview}
          onDecided={decided}
        />
      )}
    </>
  );
}

/** One flagged pair. The newer record leads — it is the one most likely created by mistake. */
function PairRow({ pair, onReview }: { pair: DuplicatePair; onReview: () => void }) {
  const { first: older, second: newer } = pair;
  return (
    <tr className="border-b border-line transition-colors last:border-b-0 hover:bg-canvas">
      <td className="px-5 py-3">
        <div className="flex flex-wrap items-center gap-2">
          <bdi className="break-words font-medium">{newer.fullName}</bdi>
          <span className="inline-flex h-5 items-center gap-1.5 rounded-full bg-warn-50 px-2 text-2xs font-medium text-warn-700">
            Possible duplicate
          </span>
        </div>
        <span className="tnum block text-sm text-ink-600">{contact(newer)}</span>
      </td>
      <td className="px-5 py-3 text-ink-700">
        <bdi className="break-words">{older.fullName}</bdi>
        <span className="block text-sm text-ink-600">{pair.matchedOn}</span>
      </td>
      <td className="px-5 py-3"><SourceChip source={newer.source} /></td>
      <td className="px-5 py-3 text-sm text-ink-600">{history(newer.applicationCount + older.applicationCount)}</td>
      <td className="px-5 py-3 text-right">
        <button
          type="button"
          onClick={onReview}
          className="text-sm font-medium text-warn-700 hover:text-warn-700"
          aria-label={`Review ${newer.fullName} and ${older.fullName}`}
        >
          Review
        </button>
      </td>
    </tr>
  );
}

interface ReviewDialogProps {
  pair: DuplicatePair;
  onClose: () => void;
  onDecided: (message: string) => Promise<void>;
}

/**
 * "Review" → the merge nobody should do blind. Three buttons, because there are three answers —
 * and "Not the same person" is the frequent one: common names collide and families share a phone.
 */
function ReviewDialog({ pair, onClose, onDecided }: ReviewDialogProps) {
  // The older record is the default to keep (the API orders it first).
  const [keepId, setKeepId] = useState(pair.first.id);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const keep = keepId === pair.first.id ? pair.first : pair.second;
  const other = keepId === pair.first.id ? pair.second : pair.first;

  async function run(action: () => Promise<string>) {
    setBusy(true);
    setError(null);
    try {
      await onDecided(await action());
    } catch (e) {
      // Most often someone else decided this pair a moment ago; the server says which.
      setError(e instanceof Error ? e.message : 'That did not work.');
      setBusy(false);
    }
  }

  const merge = () => run(async () => {
    const result = await api<MergeCandidatesResult>('/candidates/merge', {
      method: 'POST', body: JSON.stringify({ keepCandidateId: keep.id, mergeCandidateId: other.id }),
    });
    return `Merged into ${keep.fullName}: ${applications(result.applicationsMoved)} moved.`;
  });

  const keepSeparate = () => run(async () => {
    await api('/candidates/keep-separate', {
      method: 'POST', body: JSON.stringify({ firstCandidateId: pair.first.id, secondCandidateId: pair.second.id }),
    });
    return `${pair.first.fullName} and ${pair.second.fullName} are marked as different people.`;
  });

  return (
    <Dialog
      isOpen
      onClose={onClose}
      size="xl"
      // Two columns of contact details: the kit draws this at ~1020px, and at the default 672px
      // an email address breaks mid-word.
      className="md:max-w-4xl"
      title="Two records, one person?"
      description={(
        <>Matched on {pair.matchedOn.toLowerCase()} <span className="tnum font-mono">{pair.matchedValue}</span></>
      )}
    >
      <div className="grid grid-cols-1 divide-y divide-line md:grid-cols-2 md:divide-x md:divide-y-0">
        {[pair.first, pair.second].map((c) => (
          <RecordColumn
            key={c.id}
            candidate={c}
            kept={c.id === keep.id}
            onKeep={() => setKeepId(c.id)}
            disabled={busy}
          />
        ))}
      </div>

      <div className="border-t border-line bg-canvas px-5 py-4">
        <p className="text-sm font-medium">After merging</p>
        <ul className="mt-1.5 space-y-1 text-sm text-ink-600">
          {afterMerging(keep, other).map((line) => <li key={line}>· {line}</li>)}
        </ul>
      </div>

      {error && <p role="alert" className="border-t border-line px-5 py-3 text-md text-critical-700">{error}</p>}

      <div className="flex flex-wrap items-center gap-2.5 border-t border-line px-5 py-3.5">
        <Button onClick={merge} disabled={busy}>Merge</Button>
        <Button variant="secondary" onClick={keepSeparate} disabled={busy}>Not the same person</Button>
        <Button variant="secondary" onClick={onClose} disabled={busy}>Decide later</Button>
        <p className="ml-auto text-sm text-ink-600">
          “Not the same person” is remembered, so the pair stops being flagged.
        </p>
      </div>
    </Dialog>
  );
}

function RecordColumn({ candidate: c, kept, onKeep, disabled }: {
  candidate: DuplicateCandidate; kept: boolean; onKeep: () => void; disabled: boolean;
}) {
  const none = <span className="text-ink-500">— none</span>;
  return (
    <div className="px-5 py-4">
      <div className="flex flex-wrap items-center gap-2">
        {/* <bdi>: a name comes from an anonymous form, and a direction override in it must not make
            the record a person is about to merge read as someone else. */}
        <p className="min-w-0 break-words text-base font-semibold"><bdi>{c.fullName}</bdi></p>
        {kept ? (
          <span className="inline-flex h-5 items-center rounded-full bg-brand-50 px-2 text-2xs font-medium text-brand-800">
            Keep this one
          </span>
        ) : (
          <span className="inline-flex h-5 items-center rounded-full border border-line bg-canvas px-2 text-2xs font-medium text-ink-600">
            Merge into the other
          </span>
        )}
      </div>
      <dl className="mt-3 space-y-2 text-base">
        <Row label="Email">{c.email ?? none}</Row>
        <Row label="Phone"><span className="tnum">{c.phone ?? none}</span></Row>
        <Row label="Created"><span className="tnum">{formatDate(c.createdAt)}</span></Row>
        <Row label="Applications"><span className="tnum">{c.applicationCount}</span></Row>
        <Row label="Interviews">
          {c.interviewCount === 0
            ? <span className="text-ink-500">—</span>
            : <span className="tnum">{c.interviewCount}{c.hasSubmittedScorecard ? ' · scorecard submitted' : ''}</span>}
        </Row>
        <Row label="Source">{sourceLabel(c.source)}</Row>
      </dl>
      {!kept && (
        <button
          type="button"
          onClick={onKeep}
          disabled={disabled}
          className="mt-3 text-sm font-medium text-brand-700 hover:text-brand-800 disabled:text-ink-400"
        >
          Keep <bdi>{c.fullName}</bdi> instead
        </button>
      )}
    </div>
  );
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex gap-3">
      <dt className="w-28 shrink-0 text-sm text-ink-500">{label}</dt>
      <dd className="min-w-0 break-words">{children}</dd>
    </div>
  );
}

/** What a merge will do to these two, in the server's rules (CandidateDuplicateService.MergeAsync). */
export function afterMerging(keep: DuplicateCandidate, other: DuplicateCandidate): string[] {
  const total = keep.applicationCount + other.applicationCount;
  const lines = [
    total === 0
      ? 'Neither record has an application yet'
      : `${applications(total)} on one candidate, ${total === 1 ? 'keeping its' : 'each keeping its'} own stage history, interviews and scorecards`,
  ];
  for (const [field, kept, folded] of [
    ['email', keep.email, other.email],
    ['phone', keep.phone, other.phone],
  ] as const) {
    if (kept === null && folded !== null) lines.push(`The kept record has no ${field}, so it takes ${folded}`);
    else if (kept !== null && folded !== null && kept !== folded) lines.push(`The kept record's ${field} stays; ${folded} is not copied`);
  }
  lines.push(
    'A CV stays with its own application — a second CV is a second document, not a replacement',
    'The merged record keeps a pointer to the kept one, so an old link still resolves',
    'This cannot be undone',
  );
  return lines;
}

function contact(c: DuplicateCandidate): string {
  return [c.phone, c.email].filter((v): v is string => v !== null).join(' · ') || '—';
}

function applications(n: number): string {
  return `${n} application${n === 1 ? '' : 's'}`;
}

function history(n: number): string {
  return n === 0 ? 'No applications yet' : `${applications(n)} between them`;
}

const SOURCE_LABELS: Record<string, string> = {
  Direct: 'Career page',
  ExcelImport: 'Excel import',
};

function sourceLabel(source: string): string {
  return SOURCE_LABELS[source] ?? source;
}

function SourceChip({ source }: { source: string }) {
  return (
    <span className="inline-flex h-6 items-center rounded-full border border-line bg-canvas px-2.5 text-xs font-medium text-ink-600">
      {sourceLabel(source)}
    </span>
  );
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
}
