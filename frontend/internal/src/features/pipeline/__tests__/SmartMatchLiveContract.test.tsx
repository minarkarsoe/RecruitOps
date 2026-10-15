/**
 * Smart Match against the response the API *actually* sends.
 *
 * Every other Smart Match test builds its fixture from `CandidateMatchAnalysis`, so all of them
 * pass while proving nothing: the interface and the fixtures agree with each other and neither
 * had ever been compared to the service. This file is the comparison. `LIVE_RESPONSE` below is
 * a verbatim capture from the running API on 2026-08-28:
 *
 *   POST http://localhost:5080/api/Ai/claude/match-candidate  ->  200
 *
 * If `CandidateMatchAnalysis` drifts from `CandidateMatchAnalysisDto` again, this file stops
 * compiling — the object is typed, not cast.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { CandidateMatchAnalysis } from '@recruitops/types';
import { SmartMatchBreakdown, getMatchBadgeConfig } from '../SmartMatchBreakdown';

vi.mock('../../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api')>('../../../lib/api');
  return { ...actual, aiApi: { matchCandidate: vi.fn() } };
});

/** Verbatim from the running service. Do not "tidy" — that is the point of the file. */
const LIVE_RESPONSE: CandidateMatchAnalysis = {
  matchScore: 88,
  overallVerdict: 'Strong Fit',
  matchedSkills: ['C#', 'ASP.NET Core', 'PostgreSQL', 'Clean Architecture', 'TypeScript'],
  missingSkills: ['GraphQL', 'Kubernetes'],
  strengths: [
    'Extensive 7+ years hands-on experience in backend API development',
    'Proven track record of architecting scalable enterprise SaaS platforms',
    'Strong background in dynamic RBAC and multi-tenant security design',
  ],
  concerns: ['Limited experience with Kubernetes orchestration in production environments'],
  recommendation:
    'Proceed to Technical Deep Dive Interview. Candidate exceeds core senior requirements.',
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe('SmartMatchBreakdown — the API response, not the fixture', () => {
  it('shows the score the API returned', () => {
    render(<SmartMatchBreakdown candidateId="c1" jobPostingId="j1" initialAnalysis={LIVE_RESPONSE} />);
    // Was `undefined% Match` — `overallScore` never existed on the response.
    expect(screen.getAllByText(/88%/).length).toBeGreaterThan(0);
    expect(screen.queryByText(/undefined/i)).toBeNull();
  });

  it('does not label an 88-point Strong Fit as a low match', () => {
    render(<SmartMatchBreakdown candidateId="c1" jobPostingId="j1" initialAnalysis={LIVE_RESPONSE} />);
    // `recommendation` is a free-text sentence, not one of four enum members. The old switch
    // fell through every case to `default:` and painted the best candidate critical-red.
    expect(screen.queryByText(/Low Match/i)).toBeNull();
    expect(screen.getByText(/Strong Fit/)).toBeInTheDocument();
  });

  it('renders the concerns instead of claiming there are none', () => {
    render(<SmartMatchBreakdown candidateId="c1" jobPostingId="j1" initialAnalysis={LIVE_RESPONSE} />);
    // The worst of the four. `gaps` was undefined, so the empty branch ran and told the
    // recruiter "No critical gaps identified." over a concern the model had actually raised.
    expect(screen.getByText(/Kubernetes orchestration/)).toBeInTheDocument();
    expect(screen.queryByText(/No critical gaps identified/i)).toBeNull();
  });

  it('renders the recommendation sentence and the skill lists it used to discard', () => {
    render(<SmartMatchBreakdown candidateId="c1" jobPostingId="j1" initialAnalysis={LIVE_RESPONSE} />);
    expect(screen.getByText(/Technical Deep Dive Interview/)).toBeInTheDocument();
    // `matchedSkills` / `missingSkills` reached the browser and were dropped on the floor.
    expect(screen.getByText('ASP.NET Core')).toBeInTheDocument();
    expect(screen.getByText('GraphQL')).toBeInTheDocument();
  });

  it('bands the badge on the score, since the verdict is free text', () => {
    expect(getMatchBadgeConfig('Strong Fit', 88).variant).toBe('success');
    expect(getMatchBadgeConfig('Gap Identified', 35).variant).toBe('danger');
    // An unrecognised verdict must not decide the colour — only the number may.
    expect(getMatchBadgeConfig('Wildly Enthusiastic Fit', 88).variant).toBe('success');
  });
});
