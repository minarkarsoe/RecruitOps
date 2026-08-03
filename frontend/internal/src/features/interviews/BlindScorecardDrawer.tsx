import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Sheet,
  SheetBody,
  SheetHeader,
  SheetTitle,
  StatusPill,
} from '@recruitops/ui';
import type {
  HireRecommendation,
  Interview,
  InterviewScorecards,
  MyScorecard,
  SaveScorecardRequest,
  Scorecard,
  ScorecardCriterion,
} from '@recruitops/types';
import { api, ApiError } from '../../lib/api';
import { auth, hasPermission } from '../../lib/auth';
import { type Draft, draftsFrom, emptyDraft, missingRequired as unansweredRequired, toAnswers } from '../../lib/scorecard';
import { ApplicationNotes } from '../../components/ApplicationNotes';

export interface BlindScorecardDrawerProps {
  interviewId: string | null;
  isOpen: boolean;
  onClose: () => void;
  onScorecardSubmitted?: () => void;
  className?: string;
}

const RECOMMENDATIONS: { value: HireRecommendation; label: string }[] = [
  { value: 'StrongNo', label: 'Strong No' },
  { value: 'No', label: 'No' },
  { value: 'Yes', label: 'Yes' },
  { value: 'StrongYes', label: 'Strong Yes' },
];

function recommendationLabel(value: HireRecommendation | null): string {
  return RECOMMENDATIONS.find((r) => r.value === value)?.label ?? '—';
}

function RatingInput({ value, onChange }: { value: number | null; onChange: (n: number) => void }) {
  return (
    <div className="flex gap-1.5" role="group" aria-label="Rating out of 5">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          aria-pressed={value === n}
          onClick={() => onChange(n)}
          className={`h-9 w-9 rounded-sm border text-sm font-semibold transition-colors ${
            value === n
              ? 'border-primary-600 bg-primary-100 text-primary-700'
              : 'border-line-200 text-ink-600 hover:bg-surface-50'
          }`}
        >
          {n}
        </button>
      ))}
    </div>
  );
}

function CriterionField({
  criterion,
  draft,
  onChange,
}: {
  criterion: ScorecardCriterion;
  draft: Draft;
  onChange: (next: Draft) => void;
}) {
  return (
    <div className="border-t border-line-200 pt-4">
      <p className="text-sm font-semibold text-ink-900">
        {criterion.label}
        {!criterion.isRequired && <span className="ml-2 font-normal text-xs text-ink-400">optional</span>}
      </p>
      {criterion.guidance && (
        <p className="mt-0.5 text-xs text-ink-600">{criterion.guidance}</p>
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
                className={`h-9 rounded-sm border px-4 text-sm font-semibold transition-colors ${
                  draft.yesNo === v
                    ? 'border-primary-600 bg-primary-100 text-primary-700'
                    : 'border-line-200 text-ink-600 hover:bg-surface-50'
                }`}
              >
                {v ? 'Yes' : 'No'}
              </button>
            ))}
          </div>
        )}

        <textarea
          rows={criterion.type === 'Text' ? 3 : 2}
          placeholder={criterion.type === 'Text' ? 'Your answer' : 'Evidence / notes (optional)'}
          className="w-full rounded-sm border border-line-200 p-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-600"
          value={draft.comment}
          onChange={(e) => onChange({ ...draft, comment: e.target.value })}
        />
      </div>
    </div>
  );
}

function ScorecardView({ scorecard }: { scorecard: Scorecard }) {
  return (
    <div className="rounded-md border border-line-200 bg-surface-0 p-4">
      <div className="flex items-baseline justify-between gap-4">
        <p className="font-semibold text-sm text-ink-900">{scorecard.interviewerName}</p>
        <p className="text-xs text-ink-600">
          {scorecard.status === 'Submitted'
            ? `${recommendationLabel(scorecard.recommendation)} · ${
                scorecard.submittedAt ? new Date(scorecard.submittedAt).toLocaleDateString() : ''
              }`
            : 'Your draft (private)'}
        </p>
      </div>

      {scorecard.summaryComment && (
        <p className="mt-2 text-xs leading-relaxed text-ink-800 whitespace-pre-wrap">
          {scorecard.summaryComment}
        </p>
      )}

      <dl className="mt-3 space-y-2">
        {scorecard.responses.map((r) => (
          <div key={r.scorecardCriterionId}>
            <dt className="text-xs font-semibold text-ink-600">{r.criterionLabel}</dt>
            <dd className="text-xs text-ink-900">
              {r.criterionType === 'Rating' && <span className="font-mono">{r.rating ?? '—'}/5</span>}
              {r.criterionType === 'YesNo' && (
                <span>{r.yesNo === null ? '—' : r.yesNo ? 'Yes' : 'No'}</span>
              )}
              {r.comment && <span className="block mt-0.5 text-ink-600 italic">&ldquo;{r.comment}&rdquo;</span>}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

export function BlindScorecardDrawer({
  interviewId,
  isOpen,
  onClose,
  onScorecardSubmitted,
  className = '',
}: BlindScorecardDrawerProps) {
  const session = auth.get();

  const [interview, setInterview] = useState<Interview | null>(null);
  const [mine, setMine] = useState<MyScorecard | null>(null);
  const [panel, setPanel] = useState<InterviewScorecards | null>(null);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [recommendation, setRecommendation] = useState<HireRecommendation | ''>('');
  const [summary, setSummary] = useState('');
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen || !interviewId) return;

    setLoading(true);
    setError(null);
    setSaved(null);

    Promise.all([
      api<Interview>(`/interviews/${interviewId}`),
      api<InterviewScorecards>(`/interviews/${interviewId}/scorecards`),
    ])
      .then(([iv, scorecards]) => {
        setInterview(iv);
        setPanel(scorecards);
        return api<MyScorecard>(`/interviews/${interviewId}/scorecard`)
          .then((my) => {
            setMine(my);
            setDrafts(draftsFrom(my.scorecard));
            setRecommendation(my.scorecard?.recommendation ?? '');
            setSummary(my.scorecard?.summaryComment ?? '');
          })
          .catch((e) => {
            if (e instanceof ApiError && e.status === 404) setMine(null);
            else throw e;
          });
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Could not load interview details'))
      .finally(() => setLoading(false));
  }, [isOpen, interviewId]);

  async function write(submit: boolean) {
    if (!interviewId || !mine) return;

    const body: SaveScorecardRequest = {
      recommendation: recommendation || null,
      summaryComment: summary.trim() || null,
      answers: toAnswers(mine.criteria, drafts),
    };

    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      await api(`/interviews/${interviewId}/scorecard${submit ? '/submit' : ''}`, {
        method: submit ? 'POST' : 'PUT',
        body: JSON.stringify(body),
      });

      // Reload state
      const [iv, scorecards, my] = await Promise.all([
        api<Interview>(`/interviews/${interviewId}`),
        api<InterviewScorecards>(`/interviews/${interviewId}/scorecards`),
        api<MyScorecard>(`/interviews/${interviewId}/scorecard`),
      ]);
      setInterview(iv);
      setPanel(scorecards);
      setMine(my);
      setDrafts(draftsFrom(my.scorecard));

      setSaved(submit ? 'Submitted successfully.' : 'Draft saved.');
      if (submit && onScorecardSubmitted) {
        onScorecardSubmitted();
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Action failed.');
    } finally {
      setBusy(false);
    }
  }

  if (!isOpen) return null;

  const alreadySubmitted = mine?.scorecard?.status === 'Submitted';
  const canEdit = mine !== null && !alreadySubmitted && interview?.status !== 'Cancelled';
  const missingRequired = unansweredRequired(mine?.criteria ?? [], drafts);
  const canSubmit = canEdit && missingRequired.length === 0 && recommendation !== '';

  return (
    <Sheet isOpen={isOpen} onClose={onClose} size="xl" className={className}>
      {loading ? (
        <SheetBody>
          <div className="py-12 text-center text-ink-600">Loading interview scorecard...</div>
        </SheetBody>
      ) : !interview || !panel ? (
        <SheetBody>
          <div className="py-12 text-center text-ink-600">
            {error || 'Interview scorecard details not found.'}
          </div>
        </SheetBody>
      ) : (
        <div className="flex h-full flex-col">
          {/* Header */}
          <SheetHeader>
            <div className="flex items-center gap-3">
              <SheetTitle>Round {interview.round} Scorecard Evaluation</SheetTitle>
              <StatusPill status={interview.status} />
            </div>
            <p className="mt-1 text-xs text-ink-600">
              {new Date(interview.scheduledStart).toLocaleString()} · {interview.durationMinutes} min · {interview.mode}
              {interview.location ? ` · ${interview.location}` : ''}
              {interview.scorecardTemplateName ? ` · ${interview.scorecardTemplateName}` : ''}
            </p>
          </SheetHeader>

          {/* Split-View Body */}
          <SheetBody className="flex-1 overflow-y-auto p-6">
            {error && (
              <div role="alert" className="mb-4 rounded-sm bg-danger-100 p-3 text-sm text-danger-600">
                {error}
              </div>
            )}
            {saved && (
              <div className="mb-4 rounded-sm bg-success-100 p-3 text-sm text-success-600">
                {saved}
              </div>
            )}

            <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
              {/* LEFT SIDE: Candidate Info & Scoring Form */}
              <div className="space-y-6">
                {/* Panel Roster */}
                <div className="rounded-md border border-line-200 bg-surface-50 p-4">
                  <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-500">
                    Panel Roster
                  </h3>
                  <ul className="space-y-1.5 text-xs text-ink-700">
                    {interview.participants.map((p) => (
                      <li key={p.userId} className="flex items-center justify-between">
                        <span className="font-medium text-ink-900">
                          {p.displayName}
                          {p.isLead && <span className="text-ink-400"> (lead)</span>}
                        </span>
                        <Badge variant={p.hasSubmittedScorecard ? 'success' : 'secondary'} size="sm">
                          {p.hasSubmittedScorecard ? 'Scorecard In' : 'Pending'}
                        </Badge>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Scorecard Form */}
                {mine ? (
                  <div className="rounded-md border border-line-200 bg-surface-0 p-4">
                    <h3 className="mb-3 text-xs font-semibold uppercase tracking-wider text-ink-500">
                      Your Evaluation Form
                    </h3>

                    {mine.criteria.length === 0 ? (
                      <p className="text-xs text-ink-600">
                        No scorecard template applies to this posting yet.
                      </p>
                    ) : alreadySubmitted && mine.scorecard ? (
                      <div className="space-y-3">
                        <p className="text-xs text-ink-600">
                          Submitted — evaluations are locked to preserve the blind evaluation process.
                        </p>
                        <ScorecardView scorecard={mine.scorecard} />
                      </div>
                    ) : (
                      <form
                        className="space-y-4"
                        onSubmit={(e) => {
                          e.preventDefault();
                          void write(false);
                        }}
                      >
                        {mine.criteria.map((c) => (
                          <CriterionField
                            key={c.id}
                            criterion={c}
                            draft={drafts[c.id] ?? emptyDraft()}
                            onChange={(next) => setDrafts({ ...drafts, [c.id]: next })}
                          />
                        ))}

                        <div className="border-t border-line-200 pt-4">
                          <label htmlFor="rec" className="mb-1 block text-xs font-semibold text-ink-700">
                            Overall Recommendation
                          </label>
                          <select
                            id="rec"
                            className="h-9 w-full rounded-sm border border-line-200 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-primary-600"
                            value={recommendation}
                            onChange={(e) => setRecommendation(e.target.value as HireRecommendation | '')}
                          >
                            <option value="">Select recommendation...</option>
                            {RECOMMENDATIONS.map((r) => (
                              <option key={r.value} value={r.value}>
                                {r.label}
                              </option>
                            ))}
                          </select>
                        </div>

                        <div>
                          <label htmlFor="summary" className="mb-1 block text-xs font-semibold text-ink-700">
                            Overall Summary
                          </label>
                          <textarea
                            id="summary"
                            rows={3}
                            placeholder="Overall feedback for the debrief team..."
                            className="w-full rounded-sm border border-line-200 p-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-600"
                            value={summary}
                            onChange={(e) => setSummary(e.target.value)}
                          />
                        </div>

                        {missingRequired.length > 0 && (
                          <p className="text-xs text-warning-600">
                            Still needed to submit: {missingRequired.map((c) => c.label).join(', ')}.
                          </p>
                        )}

                        <div className="flex flex-wrap items-center gap-3 pt-2">
                          {hasPermission(session, 'permission:scorecards:scorecards:submit') && (
                            <>
                              <Button variant="secondary" type="submit" disabled={busy}>
                                {busy ? 'Saving...' : 'Save Draft'}
                              </Button>
                              <Button
                                type="button"
                                disabled={busy || !canSubmit}
                                onClick={() => {
                                  if (
                                    window.confirm(
                                      'Submit evaluation? This unlocks the rest of the panel’s scores and cannot be undone.'
                                    )
                                  ) {
                                    void write(true);
                                  }
                                }}
                              >
                                Submit Evaluation
                              </Button>
                            </>
                          )}
                        </div>
                      </form>
                    )}
                  </div>
                ) : (
                  <div className="rounded-md border border-line-200 bg-surface-50 p-4 text-xs text-ink-600">
                    You are viewing as a non-panel reviewer.
                  </div>
                )}
              </div>

              {/* RIGHT SIDE: Blind Panel View & @Mentions Thread */}
              <div className="space-y-6">
                {/* Blind Panel Evaluations */}
                <div className="rounded-md border border-line-200 bg-surface-0 p-4">
                  <h3 className="mb-3 text-xs font-semibold uppercase tracking-wider text-ink-500">
                    Panel Evaluations
                  </h3>

                  {panel.blindedUntilYouSubmit && panel.hiddenCount > 0 && (
                    <div className="mb-4 rounded-md bg-warning-100 p-3 text-xs text-warning-600">
                      {panel.hiddenCount} {panel.hiddenCount === 1 ? 'evaluation is' : 'evaluations are'}{' '}
                      waiting for yours. Submit your evaluation to unlock panel feedback.
                    </div>
                  )}

                  {panel.blindedUntilYouSubmit && panel.hiddenCount === 0 && (
                    <p className="mb-4 text-xs text-ink-600">
                      Nobody else has submitted yet. Their evaluations will appear here once you submit yours.
                    </p>
                  )}

                  {panel.visible.length === 0 ? (
                    !panel.blindedUntilYouSubmit && (
                      <p className="text-xs text-ink-600">No evaluations submitted yet.</p>
                    )
                  ) : (
                    <div className="space-y-3">
                      {panel.visible.map((s) => (
                        <ScorecardView key={s.id} scorecard={s} />
                      ))}
                    </div>
                  )}
                </div>

                {/* Round Debrief Thread with @Mentions */}
                <div className="rounded-md border border-line-200 bg-surface-0 p-4">
                  <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-ink-500">
                    Round Debrief Thread
                  </h3>
                  <p className="mb-3 text-xs text-ink-400">
                    Notes pinned to Round {interview.round}. Use @name to mention colleagues.
                  </p>
                  <ApplicationNotes
                    applicationId={interview.jobApplicationId}
                    pinnedTo={interview.id}
                  />
                </div>
              </div>
            </div>
          </SheetBody>
        </div>
      )}
    </Sheet>
  );
}
