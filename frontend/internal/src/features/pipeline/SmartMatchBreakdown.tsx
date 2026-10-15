import { useState, useEffect, useCallback } from 'react';
import { Badge, Button, SkeletonCard, SkeletonRow, SkeletonText } from '@recruitops/ui';
import type { CandidateMatchAnalysis } from '@recruitops/types';
import { aiApi, ApiError } from '../../lib/api';

export interface SmartMatchBreakdownProps {
  candidateId: string;
  jobPostingId?: string;
  initialAnalysis?: CandidateMatchAnalysis | null;
  onAnalysisUpdated?: (analysis: CandidateMatchAnalysis) => void;
  className?: string;
}

/**
 * Colour comes from the score; the label is the model's own verdict when it sent one.
 *
 * This used to `switch` on the verdict against four enum members that the API has never
 * returned. Because the real value is a free-text sentence, every branch missed and `default:`
 * painted an 88-point "Strong Fit" critical-red as "Low Match". Free text may be *displayed*,
 * never *decided on* — the number is the only thing here with a defined range.
 */
export function getMatchBadgeConfig(
  overallVerdict?: string,
  matchScore?: number
): { variant: 'success' | 'primary' | 'warning' | 'danger'; label: string } {
  const score = matchScore ?? 0;
  const variant =
    score >= 80 ? 'success' : score >= 60 ? 'primary' : score >= 40 ? 'warning' : 'danger';
  const label = overallVerdict?.trim() ? overallVerdict.trim() : `${score}% Match`;
  return { variant, label };
}

export function SmartMatchBreakdown({
  candidateId,
  jobPostingId,
  initialAnalysis = null,
  onAnalysisUpdated,
  className = '',
}: SmartMatchBreakdownProps) {
  const [analysis, setAnalysis] = useState<CandidateMatchAnalysis | null>(initialAnalysis);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<ApiError | Error | null>(null);
  const [isApiKeyMissing, setIsApiKeyMissing] = useState(false);

  const runMatchAnalysis = useCallback(async () => {
    if (!candidateId || !jobPostingId) return;
    setLoading(true);
    setError(null);
    setIsApiKeyMissing(false);

    try {
      const result = await aiApi.matchCandidate({ candidateId, jobPostingId });
      setAnalysis(result);
      if (onAnalysisUpdated) {
        onAnalysisUpdated(result);
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setIsApiKeyMissing(true);
        setError(err);
      } else if (err instanceof ApiError) {
        setError(err);
      } else {
        setError(err instanceof Error ? err : new Error('Failed to run Smart Match analysis.'));
      }
    } finally {
      setLoading(false);
    }
  }, [candidateId, jobPostingId, onAnalysisUpdated]);

  useEffect(() => {
    if (initialAnalysis) {
      setAnalysis(initialAnalysis);
    } else if (jobPostingId && candidateId && !analysis && !loading && !error && !isApiKeyMissing) {
      runMatchAnalysis();
    }
  }, [candidateId, jobPostingId, initialAnalysis]);

  const badgeConfig = analysis ? getMatchBadgeConfig(analysis.overallVerdict, analysis.matchScore) : null;

  return (
    <div className={`space-y-5 rounded-lg border border-line bg-white p-5 ${className}`}>
      {/* Header & Controls */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-base font-semibold text-ink-900">AI Smart Match Analysis</h3>
            {badgeConfig && (
              <Badge variant={badgeConfig.variant}>
                {analysis?.matchScore}% Match ({badgeConfig.label})
              </Badge>
            )}
          </div>
          <p className="mt-0.5 text-sm text-ink-500">
            Powered by Claude AI candidate compatibility scoring & criteria evaluation
          </p>
        </div>

        <Button
          onClick={runMatchAnalysis}
          disabled={loading || isApiKeyMissing || !jobPostingId}
          variant="secondary"
          className="gap-1.5"
        >
          <svg className="h-3.5 w-3.5 text-brand-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
            />
          </svg>
          {loading ? 'Analyzing...' : analysis ? 'Re-analyze Fit' : 'Analyze Fit'}
        </Button>
      </div>

      {/* 402 API Key Gating Graceful Alert Banner */}
      {isApiKeyMissing && (
        <div
          data-testid="smart-match-402-banner"
          /* warn-50/warn-700, not raw Tailwind amber. Same hue family, but amber-500 as text on
             amber-50 measures 2.07:1 and the token step is the whole point of having tokens. */
          className="rounded-md border border-warn-100 bg-warn-50 p-4"
        >
          <div className="flex items-start gap-3">
            <svg className="mt-0.5 h-5 w-5 shrink-0 text-warn-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            <div>
              <h4 className="text-base font-semibold text-warn-700">AI Features Unconfigured: API key required</h4>
              <p className="mt-1 text-sm leading-5 text-ink-700">
                An AI Provider API Key (Claude) has not been configured for this installation. Candidate Smart Match
                features are currently disabled. Manual candidate profiling and evaluation remain fully operational.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* General Non-402 Error State */}
      {error && !isApiKeyMissing && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-critical-100 bg-critical-50 p-4 text-sm text-critical-700">
          <span>{error.message || 'An error occurred while running candidate match analysis.'}</span>
          <Button onClick={runMatchAnalysis} variant="secondary">
            Retry
          </Button>
        </div>
      )}

      {/* Loading Skeleton State */}
      {loading && (
        <div data-testid="smart-match-skeleton" className="space-y-4 py-2">
          <SkeletonCard className="h-20" />
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <SkeletonText lines={4} />
            <SkeletonText lines={4} />
          </div>
          <div className="space-y-2">
            <SkeletonRow />
            <SkeletonRow />
            <SkeletonRow />
          </div>
        </div>
      )}

      {/* Match Analysis Results */}
      {!loading && analysis && !isApiKeyMissing && (
        <div className="space-y-5">
          {/* Recommendation Banner — the API's `recommendation` is the next-step sentence, and
              was previously never rendered anywhere. `summary` did not exist. */}
          <div className="rounded-md border border-line bg-canvas p-4">
            <div className="mb-2 flex items-center justify-between gap-2">
              <span className="text-2xs font-medium uppercase tracking-wider text-ink-500">Recommendation</span>
              <span className="font-mono text-sm tnum text-ink-900">{analysis.matchScore}% Overall Score</span>
            </div>
            <p className="text-base leading-6 text-ink-800">{analysis.recommendation}</p>
          </div>

          {/* Strengths & Concerns */}
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {/* Strengths */}
            <div className="rounded-md border border-positive-100 bg-positive-50 p-4">
              <h4 className="mb-2 flex items-center gap-1.5 text-2xs font-medium uppercase tracking-wider text-positive-700">
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
                Key Strengths ({analysis.strengths?.length || 0})
              </h4>
              {analysis.strengths && analysis.strengths.length > 0 ? (
                <ul className="space-y-1.5 text-sm text-ink-800">
                  {analysis.strengths.map((str, idx) => (
                    <li key={idx} className="flex items-start gap-2">
                      <span className="text-positive-700">•</span>
                      <span>{str}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-ink-500">No specific strengths identified.</p>
              )}
            </div>

            {/* Gaps */}
            <div className="rounded-md border border-warn-100 bg-warn-50 p-4">
              <h4 className="mb-2 flex items-center gap-1.5 text-2xs font-medium uppercase tracking-wider text-warn-700">
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                  />
                </svg>
                Identified Concerns ({analysis.concerns?.length || 0})
              </h4>
              {analysis.concerns && analysis.concerns.length > 0 ? (
                <ul className="space-y-1.5 text-sm text-ink-800">
                  {analysis.concerns.map((concern, idx) => (
                    <li key={idx} className="flex items-start gap-2">
                      <span className="text-warn-700">•</span>
                      <span>{concern}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-ink-500">No critical concerns identified.</p>
              )}
            </div>
          </div>

          {/* Skill coverage. This replaces a per-criterion score table and a suggested-questions
              list, neither of which the API has ever returned — while `matchedSkills` and
              `missingSkills`, which it does return on every call, were dropped on the floor.
              Interview questions live in prepareDocument's InterviewKit, not here. */}
          {(analysis.matchedSkills?.length > 0 || analysis.missingSkills?.length > 0) && (
            <div>
              <h4 className="mb-3 text-2xs font-medium uppercase tracking-wider text-ink-500">
                Skill Coverage
              </h4>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                  <p className="mb-2 text-2xs font-medium uppercase tracking-wider text-positive-700">
                    Matched ({analysis.matchedSkills?.length || 0})
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {analysis.matchedSkills?.length > 0 ? (
                      analysis.matchedSkills.map((skill, idx) => (
                        <span
                          key={idx}
                          className="inline-flex h-5 items-center rounded-full bg-positive-50 px-2 text-2xs font-medium text-positive-700"
                        >
                          {skill}
                        </span>
                      ))
                    ) : (
                      <span className="text-sm text-ink-500">None matched.</span>
                    )}
                  </div>
                </div>
                <div>
                  <p className="mb-2 text-2xs font-medium uppercase tracking-wider text-critical-700">
                    Missing ({analysis.missingSkills?.length || 0})
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {analysis.missingSkills?.length > 0 ? (
                      analysis.missingSkills.map((skill, idx) => (
                        <span
                          key={idx}
                          className="inline-flex h-5 items-center rounded-full bg-critical-50 px-2 text-2xs font-medium text-critical-700"
                        >
                          {skill}
                        </span>
                      ))
                    ) : (
                      <span className="text-sm text-ink-500">No gaps against the posting.</span>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {!loading && !analysis && !error && !isApiKeyMissing && (
        <div className="py-8 text-center text-sm text-ink-600">
          {!jobPostingId ? (
            <p>No job posting selected for match analysis.</p>
          ) : (
            <p>Click &quot;Analyze Fit&quot; to evaluate candidate compatibility against job requirements.</p>
          )}
        </div>
      )}
    </div>
  );
}
