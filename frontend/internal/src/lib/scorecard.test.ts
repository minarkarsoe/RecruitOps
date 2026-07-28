import { describe, expect, it } from 'vitest';
import type { ScorecardCriterion } from '@recruitops/types';
import { draftsFrom, emptyDraft, isSendable, missingRequired, toAnswers } from './scorecard';
import { criteria, scorecard } from '../test/fixtures';

const rating = criteria[0];
const yesNo = criteria[1];
const text = criteria[2];

describe('isSendable', () => {
  it('holds back a rating with no score', () => {
    // The API rejects `rating: null` on a Rating outright, on a draft save as well as a
    // submit — so sending it would fail the whole payload and lose the other answers too.
    expect(isSendable(rating, emptyDraft())).toBe(false);
    expect(isSendable(rating, { ...emptyDraft(), comment: 'thoughtful' })).toBe(false);
    expect(isSendable(rating, { ...emptyDraft(), rating: 1 })).toBe(true);
  });

  it('treats a No as an answer, not as an empty one', () => {
    // `false` is a real answer. A truthiness check here would silently drop every No.
    expect(isSendable(yesNo, { ...emptyDraft(), yesNo: false })).toBe(true);
    expect(isSendable(yesNo, { ...emptyDraft(), yesNo: true })).toBe(true);
    expect(isSendable(yesNo, emptyDraft())).toBe(false);
  });

  it('treats a rating of 0 as an answer', () => {
    // Ratings are 1–5 so 0 should never arrive, but a `!draft.rating` check would drop it
    // if it ever did — and that is the same bug as the No above, wearing a different hat.
    expect(isSendable(rating, { ...emptyDraft(), rating: 0 })).toBe(true);
  });

  it('does not count whitespace as a text answer', () => {
    expect(isSendable(text, { ...emptyDraft(), comment: '   \n ' })).toBe(false);
    expect(isSendable(text, { ...emptyDraft(), comment: 'ကောင်းတယ်' })).toBe(true);
  });
});

describe('toAnswers', () => {
  it('omits untouched criteria rather than sending nulls', () => {
    const answers = toAnswers(criteria, { 'c-rating': { rating: 3, yesNo: null, comment: '' } });

    expect(answers).toHaveLength(1);
    expect(answers[0].scorecardCriterionId).toBe('c-rating');
  });

  it('sends only the field the criterion type gives meaning to', () => {
    const answers = toAnswers(criteria, {
      'c-rating': { rating: 5, yesNo: true, comment: 'depth' },
      'c-yesno': { rating: 2, yesNo: false, comment: '' },
    });

    expect(answers).toEqual([
      { scorecardCriterionId: 'c-rating', rating: 5, yesNo: null, comment: 'depth' },
      { scorecardCriterionId: 'c-yesno', rating: null, yesNo: false, comment: null },
    ]);
  });

  it('drops a comment written against an answer that was never given', () => {
    // Documented in the form's own helper text. It is a real loss, and the alternative is
    // a 400 that loses the entire draft — but it must be a *deliberate* loss, so it is
    // pinned here rather than left to be rediscovered.
    expect(toAnswers([rating], { 'c-rating': { rating: null, yesNo: null, comment: 'lots' } }))
      .toEqual([]);
  });

  it('trims a comment and sends an all-whitespace one as null', () => {
    const answers = toAnswers([rating], {
      'c-rating': { rating: 4, yesNo: null, comment: '  fine  ' },
    });
    expect(answers[0].comment).toBe('fine');

    const blank = toAnswers([rating], { 'c-rating': { rating: 4, yesNo: null, comment: ' ' } });
    expect(blank[0].comment).toBeNull();
  });

  it('ignores a draft whose criterion is not on the template', () => {
    // A stale tab holding a removed criterion. The server drops unknown ids anyway; not
    // sending them means a stale form does not quietly depend on that defence.
    const answers = toAnswers([rating], {
      'c-rating': { rating: 4, yesNo: null, comment: '' },
      'c-gone': { rating: 5, yesNo: null, comment: 'from an older template' },
    });

    expect(answers.map((a) => a.scorecardCriterionId)).toEqual(['c-rating']);
  });
});

describe('missingRequired', () => {
  it('blocks submit on required criteria only', () => {
    const optionalOnly = missingRequired(criteria, { 'c-rating': { rating: 3, yesNo: null, comment: '' } });
    expect(optionalOnly).toEqual([]);

    const nothing = missingRequired(criteria, {});
    expect(nothing.map((c) => c.id)).toEqual(['c-rating']);
  });

  it('agrees with what toAnswers sends', () => {
    // The completeness check and the payload filter are the same question asked twice.
    // They agree only by construction, so this is the assertion that keeps them agreeing:
    // anything missingRequired complains about must be absent from the payload.
    const drafts = { 'c-rating': { rating: null, yesNo: null, comment: 'evidence' } };
    const sent = toAnswers(criteria, drafts).map((a) => a.scorecardCriterionId);

    for (const c of missingRequired(criteria, drafts)) {
      expect(sent).not.toContain(c.id);
    }
  });
});

describe('draftsFrom', () => {
  it('seeds the form from a saved scorecard so a reload does not lose it', () => {
    expect(draftsFrom(scorecard())).toEqual({
      'c-rating': { rating: 4, yesNo: null, comment: '' },
    });
  });

  it('is empty when nothing has been saved yet', () => {
    expect(draftsFrom(null)).toEqual({});
  });

  it('round-trips: a seeded draft sends back what was saved', () => {
    const saved = scorecard();
    const seeded = draftsFrom(saved);
    const onlyRating: ScorecardCriterion[] = [rating];

    expect(toAnswers(onlyRating, seeded)).toEqual([
      { scorecardCriterionId: 'c-rating', rating: 4, yesNo: null, comment: null },
    ]);
  });
});
