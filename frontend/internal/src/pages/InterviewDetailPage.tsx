import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type {
  HireRecommendation, Interview, InterviewScorecards, MyScorecard, Scorecard,
  ScorecardCriterion, SaveScorecardRequest,
} from '@recruitops/types';
import { api, ApiError } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';
import {
  type Draft, draftsFrom, emptyDraft, missingRequired as unansweredRequired, toAnswers,
} from '../lib/scorecard';
import { ApplicationNotes } from '../components/ApplicationNotes';

/**
 * One interview round: the caller's own scorecard, and the panel's — filtered by the blind
 * rule (ADR-0017 §3).
 *
 * Three audiences share this page and see different things:
 *  - a panel member who has not submitted: their own form, and a count of what is withheld
 *  - a panel member who has submitted: everything, including the evaluation that disagrees
 *  - a recruiter who was not in the room: the submitted evaluations, and no form at all
 */

const RECOMMENDATIONS: { value: HireRecommendation; label: string }[] = [
  { value: 'StrongNo', label: 'Strong no' },
  { value: 'No', label: 'No' },
  { value: 'Yes', label: 'Yes' },
  { value: 'StrongYes', label: 'Strong yes' },
];

function recommendationLabel(value: HireRecommendation | null): string {
  return RECOMMENDATIONS.find((r) => r.value === value)?.label ?? '—';
}

// The payload rules — `isSendable`, `toAnswers`, `draftsFrom` — live in `lib/scorecard.ts`
// so they can be asserted directly rather than only through this page.

const field =
  'h-10 w-full rounded-md border border-line px-3 text-md focus:outline-none focus:ring-2 focus:ring-brand-700';

// ---------------------------------------------------------------------------

function RatingInput({ value, onChange }: { value: number | null; onChange: (n: number) => void }) {
  return (
    <div className="flex gap-1.5" role="group" aria-label="Rating out of 5">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          aria-pressed={value === n}
          onClick={() => onChange(n)}
          className={`h-9 w-9 rounded-md border text-md font-semibold ${
            value === n
              ? 'border-brand-700 bg-brand-50 text-brand-700'
              : 'border-line text-ink-600 hover:bg-canvas'
          }`}
        >
          {n}
        </button>
      ))}
    </div>
  );
}

function CriterionField({
  criterion, draft, onChange,
}: {
  criterion: ScorecardCriterion;
  draft: Draft;
  onChange: (next: Draft) => void;
}) {
  return (
    <div className="border-t border-line pt-4">
      <p className="text-md font-semibold">
        {criterion.label}
        {!criterion.isRequired && <span className="ml-2 font-normal text-ink-400">optional</span>}
      </p>
      {criterion.guidance && (
        <p className="mt-0.5 max-w-[60ch] text-sm text-ink-600">{criterion.guidance}</p>
      )}

      <div className="mt-2 space-y-2">
        {criterion.type === 'Rating' && (
          <RatingInput value={draft.rating} onChange={(rating) => onChange({ ...draft, rating })} />
        )}

        {criterion.type === 'YesNo' && (
          <div className="flex gap-2">
            {[true, false].map((v) => (
              <button
                key={String(v)}
                type="button"
                aria-pressed={draft.yesNo === v}
                onClick={() => onChange({ ...draft, yesNo: v })}
                className={`h-9 rounded-md border px-4 text-md font-semibold ${
                  draft.yesNo === v
                    ? 'border-brand-700 bg-brand-50 text-brand-700'
                    : 'border-line text-ink-600 hover:bg-canvas'
                }`}
              >
                {v ? 'Yes' : 'No'}
              </button>
            ))}
          </div>
        )}

        <textarea
          rows={criterion.type === 'Text' ? 4 : 2}
          placeholder={criterion.type === 'Text' ? 'Your answer' : 'Evidence (optional)'}
          className="w-full rounded-md border border-line p-3 text-md focus:outline-none focus:ring-2 focus:ring-brand-700"
          value={draft.comment}
          onChange={(e) => onChange({ ...draft, comment: e.target.value })}
        />
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------

function ScorecardView({ scorecard }: { scorecard: Scorecard }) {
  return (
    <div className="rounded-md border border-line p-4">
      <div className="flex items-baseline justify-between gap-4">
        <p className="font-semibold">{scorecard.interviewerName}</p>
        <p className="text-sm text-ink-600">
          {scorecard.status === 'Submitted'
            ? `${recommendationLabel(scorecard.recommendation)} · submitted ${
                scorecard.submittedAt ? new Date(scorecard.submittedAt).toLocaleDateString() : ''
              }`
            : 'Your draft — nobody else can see this'}
        </p>
      </div>

      {scorecard.summaryComment && (
        <p className="mt-2 max-w-[60ch] whitespace-pre-wrap text-md">{scorecard.summaryComment}</p>
      )}

      <dl className="mt-3 space-y-2">
        {scorecard.responses.map((r) => (
          <div key={r.scorecardCriterionId}>
            {/* The label is the one snapshotted when this was written, not the template's
                current wording — a criterion renamed since must not rewrite what this
                person was actually asked. */}
            <dt className="text-sm font-semibold text-ink-600">{r.criterionLabel}</dt>
            <dd className="text-md">
              {r.criterionType === 'Rating' && <span className="font-mono">{r.rating ?? '—'}/5</span>}
              {r.criterionType === 'YesNo' && <span>{r.yesNo === null ? '—' : r.yesNo ? 'Yes' : 'No'}</span>}
              {r.comment && (
                <span className="block max-w-[60ch] whitespace-pre-wrap text-ink-600">{r.comment}</span>
              )}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

// ---------------------------------------------------------------------------

export function InterviewDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const session = auth.get();

  const [interview, setInterview] = useState<Interview | null>(null);
  const [mine, setMine] = useState<MyScorecard | null>(null);
  const [panel, setPanel] = useState<InterviewScorecards | null>(null);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [recommendation, setRecommendation] = useState<HireRecommendation | ''>('');
  const [summary, setSummary] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;

    const [iv, scorecards] = await Promise.all([
      api<Interview>(`/interviews/${id}`),
      api<InterviewScorecards>(`/interviews/${id}/scorecards`),
    ]);
    setInterview(iv);
    setPanel(scorecards);

    // 404 here means "you were not on this panel", which is an ordinary state for a
    // recruiter reading a debrief — not an error to show them. Anything else still is.
    try {
      const my = await api<MyScorecard>(`/interviews/${id}/scorecard`);
      setMine(my);
      setDrafts(draftsFrom(my.scorecard));
      setRecommendation(my.scorecard?.recommendation ?? '');
      setSummary(my.scorecard?.summaryComment ?? '');
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) setMine(null);
      else throw e;
    }
  }, [id]);

  useEffect(() => {
    load().catch((e) =>
      setError(e instanceof Error ? e.message : 'Could not load this interview.'));
  }, [load]);

  async function write(submit: boolean) {
    if (!id || !mine) return;

    const body: SaveScorecardRequest = {
      recommendation: recommendation || null,
      summaryComment: summary.trim() || null,
      answers: toAnswers(mine.criteria, drafts),
    };

    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      await api(`/interviews/${id}/scorecard${submit ? '/submit' : ''}`, {
        method: submit ? 'POST' : 'PUT',
        body: JSON.stringify(body),
      });
      await load();
      setSaved(submit ? 'Submitted.' : 'Draft saved.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  if (error && !interview) return <p role="alert" className="text-critical-700">{error}</p>;
  if (!interview || !panel) return <p className="text-ink-600">Loading…</p>;

  const alreadySubmitted = mine?.scorecard?.status === 'Submitted';
  const canEdit = mine !== null && !alreadySubmitted && interview.status !== 'Cancelled';
  const missingRequired = unansweredRequired(mine?.criteria ?? [], drafts);
  const canSubmit = canEdit && missingRequired.length === 0 && recommendation !== '';

  return (
    <>
      <header className="mb-6">
        <button
          type="button"
          onClick={() => navigate(-1)}
          className="mb-2 inline-block text-sm text-brand-700 hover:underline"
        >
          ← Back
        </button>
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold tracking-tight">Round {interview.round}</h1>
          <StatusPill status={interview.status} />
        </div>
        <p className="mt-1 text-sm text-ink-600">
          {new Date(interview.scheduledStart).toLocaleString()} · {interview.durationMinutes} min ·{' '}
          {interview.mode}
          {interview.location ? ` · ${interview.location}` : ''}
          {interview.scorecardTemplateName ? ` · ${interview.scorecardTemplateName}` : ''}
        </p>
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}
      {saved && <p className="mb-4 text-md text-positive-700">{saved}</p>}

      <div className="space-y-6">
        {/* ── Panel roster ── */}
        <Card>
          <h2 className="mb-3 text-base font-semibold">
            Panel
          </h2>
          <ul className="space-y-1.5">
            {interview.participants.map((p) => (
              <li key={p.userId} className="flex items-center justify-between text-md">
                <span>{p.displayName}{p.isLead && <span className="text-ink-400"> · lead</span>}</span>
                {/* Who has finished is visible to the whole panel on purpose: it says
                    nothing about what they wrote, and it is what lets a lead chase the
                    outstanding one. */}
                <span className="text-sm text-ink-600">
                  {p.hasSubmittedScorecard ? 'Scorecard in' : 'Not yet'}
                </span>
              </li>
            ))}
          </ul>
        </Card>

        {/* ── My scorecard ── */}
        {mine && (
          <Card>
            <h2 className="mb-1 text-base font-semibold">
              Your evaluation
            </h2>

            {mine.criteria.length === 0 ? (
              <p className="text-md text-ink-600">
                No scorecard template applies to this posting yet, so there are no criteria to
                fill in. Recruitment staff can set one up under Scorecard templates.
              </p>
            ) : alreadySubmitted && mine.scorecard ? (
              <>
                <p className="mb-3 text-md text-ink-600">
                  Submitted — evaluations cannot be changed afterwards, which is what makes the
                  blind rule mean anything.
                </p>
                <ScorecardView scorecard={mine.scorecard} />
              </>
            ) : interview.status === 'Cancelled' ? (
              <p className="text-md text-ink-600">
                This round was cancelled; there is nothing to evaluate.
              </p>
            ) : (
              <form
                className="space-y-4"
                onSubmit={(e) => { e.preventDefault(); void write(false); }}
              >
                {mine.criteria.map((c) => (
                  <CriterionField
                    key={c.id}
                    criterion={c}
                    draft={drafts[c.id] ?? emptyDraft()}
                    onChange={(next) => setDrafts({ ...drafts, [c.id]: next })}
                  />
                ))}

                <div className="border-t border-line pt-4">
                  <label htmlFor="rec" className="mb-1 block text-sm font-semibold">
                    Overall recommendation
                  </label>
                  <select
                    id="rec"
                    className={`${field} max-w-xs`}
                    value={recommendation}
                    onChange={(e) => setRecommendation(e.target.value as HireRecommendation | '')}
                  >
                    <option value="">—</option>
                    {RECOMMENDATIONS.map((r) => (
                      <option key={r.value} value={r.value}>{r.label}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label htmlFor="summary" className="mb-1 block text-sm font-semibold">
                    Summary
                  </label>
                  <textarea
                    id="summary"
                    rows={4}
                    className="w-full rounded-md border border-line p-3 text-md focus:outline-none focus:ring-2 focus:ring-brand-700"
                    value={summary}
                    onChange={(e) => setSummary(e.target.value)}
                  />
                </div>

                <p className="text-sm text-ink-400">
                  A draft saves whatever is complete: a rating with no score, or a yes/no with
                  no answer, is not stored yet — so a comment written against one is kept only
                  once its answer is given.
                </p>

                {missingRequired.length > 0 && (
                  <p className="text-sm text-ink-600">
                    Still needed to submit: {missingRequired.map((c) => c.label).join(', ')}.
                  </p>
                )}

                <div className="flex items-center gap-3">
                  {hasPermission(session, 'permission:scorecards:scorecards:submit') && (
                    <>
                      <Button variant="secondary" type="submit" disabled={busy}>
                        {busy ? 'Saving…' : 'Save draft'}
                      </Button>
                      <Button
                        type="button"
                        disabled={busy || !canSubmit}
                        onClick={() => {
                          if (window.confirm(
                            'Submit this evaluation? It cannot be changed afterwards, and it '
                            + 'unlocks the rest of the panel’s scores for you.',
                          )) void write(true);
                        }}
                      >
                        Submit evaluation
                      </Button>
                    </>
                  )}
                </div>
              </form>
            )}
          </Card>
        )}

        {/* ── The panel's evaluations ── */}
        <Card>
          <h2 className="mb-3 text-base font-semibold">
            Panel evaluations
          </h2>

          {/* The blind rule rendered as a state, not an error. A bare "forbidden" would read
              as a bug; naming what is waiting and why makes it a step in the process. */}
          {panel.blindedUntilYouSubmit && panel.hiddenCount > 0 && (
            <p className="mb-4 rounded-md bg-warn-50 p-3 text-md text-warn-700">
              {panel.hiddenCount} {panel.hiddenCount === 1 ? 'evaluation is' : 'evaluations are'}{' '}
              waiting for yours. They unlock as soon as you submit — so that what you write is
              yours rather than an echo of theirs.
            </p>
          )}
          {panel.blindedUntilYouSubmit && panel.hiddenCount === 0 && (
            <p className="mb-4 text-md text-ink-600">
              Nobody else has submitted yet. Their evaluations will appear here once you have
              submitted yours.
            </p>
          )}

          {panel.visible.length === 0 ? (
            !panel.blindedUntilYouSubmit && (
              <p className="text-md text-ink-600">No evaluations submitted yet.</p>
            )
          ) : (
            <div className="space-y-3">
              {panel.visible.map((s) => <ScorecardView key={s.id} scorecard={s} />)}
            </div>
          )}
        </Card>

        {/* ── Debrief, pinned to this round ── */}
        <Card>
          <h2 className="mb-1 text-base font-semibold">
            Debrief
          </h2>
          <p className="mb-2 text-sm text-ink-400">
            Notes about this round. The candidate&apos;s full thread is on the pipeline board.
          </p>
          {/* Notes are not blinded — they are a conversation, not an independent judgement,
              and the blind rule (ADR-0017 §3) is about scorecards. Someone who wants to keep
              their read to themselves until they submit simply does not post yet. */}
          <ApplicationNotes
            applicationId={interview.jobApplicationId}
            pinnedTo={interview.id}
          />
        </Card>
      </div>
    </>
  );
}
