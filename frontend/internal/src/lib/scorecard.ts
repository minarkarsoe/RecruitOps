import type {
  Scorecard, ScorecardAnswerInput, ScorecardCriterion,
} from '@recruitops/types';

/*
 * ---------------------------------------------------------------------------
 * The scorecard form's payload rules, lifted out of `InterviewDetailPage` so they can be
 * asserted directly.
 *
 * They are the quiet kind of logic: getting `isSendable` wrong does not throw, it just
 * stops drafts saving, and the screen still looks right while it happens. Testing them
 * through the rendered page would work, but it would test them once — as a module they can
 * be pinned case by case.
 * ---------------------------------------------------------------------------
 */

/** Local edit state for one criterion. Deliberately partial, unlike what we send. */
export interface Draft {
  rating: number | null;
  yesNo: boolean | null;
  comment: string;
}

export function emptyDraft(): Draft {
  return { rating: null, yesNo: null, comment: '' };
}

/**
 * True when this answer is complete enough to send.
 *
 * The API validates **every** answer in the payload, on a draft save as well as a submit —
 * a Rating with no rating is rejected outright rather than stored as half an answer. So an
 * untouched criterion must be omitted from `answers` entirely, not sent as nulls. Omitting
 * is also what "unanswered" means to the completeness check at submit, so the two agree.
 */
export function isSendable(criterion: ScorecardCriterion, draft: Draft): boolean {
  if (criterion.type === 'Rating') return draft.rating !== null;
  if (criterion.type === 'YesNo') return draft.yesNo !== null;
  return draft.comment.trim().length > 0;
}

/**
 * Build the `answers` array: only sendable criteria, and each one carrying only the field
 * its own type gives meaning to. A comment typed against a rating that was never given is
 * dropped with it — the alternative is a payload the API rejects wholesale, which would
 * lose the rest of the draft too.
 */
export function toAnswers(
  criteria: ScorecardCriterion[], drafts: Record<string, Draft>,
): ScorecardAnswerInput[] {
  return criteria
    .filter((c) => isSendable(c, drafts[c.id] ?? emptyDraft()))
    .map((c) => {
      const d = drafts[c.id];
      return {
        scorecardCriterionId: c.id,
        rating: c.type === 'Rating' ? d.rating : null,
        yesNo: c.type === 'YesNo' ? d.yesNo : null,
        comment: d.comment.trim() || null,
      };
    });
}

/** Seed the form from whatever was saved before, so a reload does not lose a draft. */
export function draftsFrom(scorecard: Scorecard | null): Record<string, Draft> {
  const drafts: Record<string, Draft> = {};
  for (const r of scorecard?.responses ?? []) {
    drafts[r.scorecardCriterionId] = {
      rating: r.rating,
      yesNo: r.yesNo,
      comment: r.comment ?? '',
    };
  }
  return drafts;
}

/** Required criteria still unanswered — what blocks submit, and what the form lists. */
export function missingRequired(
  criteria: ScorecardCriterion[], drafts: Record<string, Draft>,
): ScorecardCriterion[] {
  return criteria.filter((c) => c.isRequired && !isSendable(c, drafts[c.id] ?? emptyDraft()));
}
