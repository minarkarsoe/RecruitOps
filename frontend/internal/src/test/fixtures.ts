import type {
  Interview, InterviewScorecards, MyScorecard, Note, Scorecard, ScorecardCriterion,
} from '@recruitops/types';

/*
 * Shapes mirroring what the API serialises. They are hand-written rather than generated,
 * which is exactly the drift these tests cannot catch — `packages/types` is the contract,
 * and only running the stack proves the backend agrees with it. What they *can* catch is
 * this app disagreeing with itself.
 */

export const criteria: ScorecardCriterion[] = [
  { id: 'c-rating', sequence: 1, label: 'Technical depth', guidance: null, type: 'Rating', isRequired: true },
  { id: 'c-yesno', sequence: 2, label: 'Would you work with them', guidance: null, type: 'YesNo', isRequired: false },
  { id: 'c-text', sequence: 3, label: 'Evidence', guidance: null, type: 'Text', isRequired: false },
];

export function interview(overrides: Partial<Interview> = {}): Interview {
  return {
    id: 'iv-1',
    jobApplicationId: 'app-1',
    round: 1,
    scheduledStart: '2026-08-01T09:00:00Z',
    durationMinutes: 45,
    mode: 'OnSite',
    location: 'Room 2',
    status: 'Scheduled',
    agenda: null,
    cancellationReason: null,
    scorecardTemplateId: 'tpl-1',
    scorecardTemplateName: 'Engineering',
    participants: [
      { userId: 'u-me', displayName: 'Aye Aye', email: null, isLead: true, hasSubmittedScorecard: false },
      { userId: 'u-other', displayName: 'Bo Bo', email: null, isLead: false, hasSubmittedScorecard: true },
    ],
    ...overrides,
  };
}

export function scorecard(overrides: Partial<Scorecard> = {}): Scorecard {
  return {
    id: 'sc-1',
    interviewId: 'iv-1',
    interviewerUserId: 'u-other',
    interviewerName: 'Bo Bo',
    status: 'Submitted',
    submittedAt: '2026-08-01T10:00:00Z',
    recommendation: 'Yes',
    summaryComment: 'Solid.',
    responses: [
      {
        scorecardCriterionId: 'c-rating',
        criterionLabel: 'Technical depth',
        criterionType: 'Rating',
        rating: 4,
        yesNo: null,
        comment: null,
      },
    ],
    ...overrides,
  };
}

export function myScorecard(overrides: Partial<MyScorecard> = {}): MyScorecard {
  return {
    interviewId: 'iv-1',
    scorecardTemplateId: 'tpl-1',
    scorecardTemplateName: 'Engineering',
    criteria,
    scorecard: null,
    ...overrides,
  };
}

export function panel(overrides: Partial<InterviewScorecards> = {}): InterviewScorecards {
  return {
    interviewId: 'iv-1',
    visible: [],
    hiddenCount: 0,
    blindedUntilYouSubmit: false,
    ...overrides,
  };
}

export function note(overrides: Partial<Note> = {}): Note {
  return {
    id: 'n-1',
    jobApplicationId: 'app-1',
    interviewId: null,
    authorUserId: 'u-other',
    authorName: 'Bo Bo',
    body: 'Strong hire.',
    bodyHtml: 'Strong hire.',
    createdAt: '2026-08-01T11:00:00Z',
    mentions: [],
    ...overrides,
  };
}
