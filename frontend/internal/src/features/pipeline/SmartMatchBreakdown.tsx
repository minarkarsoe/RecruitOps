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

export function getMatchBadgeConfig(
  recommendation?: CandidateMatchAnalysis['recommendation'],
  overallScore?: number
): { variant: 'success' | 'primary' | 'warning' | 'danger'; label: string } {
  const score = overallScore ?? 0;
  if (recommendation) {
    switch (recommendation) {
      case 'StrongMatch':
        return { variant: 'success', label: 'Strong Match' };
      case 'GoodMatch':
        return { variant: 'primary', label: 'Good Match' };
      case 'PossibleMatch':
        return { variant: 'warning', label: 'Possible Match' };
      case 'LowMatch':
      default:
        return { variant: 'danger', label: 'Low Match' };
    }
  }

  if (score >= 80) {
    return { variant: 'success', label: `${score}% Match` };
  }
  if (score >= 60) {
    return { variant: 'primary', label: `${score}% Match` };
  }
  if (score >= 40) {
    return { variant: 'warning', label: `${score}% Match` };
  }
  return { variant: 'danger', label: `${score}% Match` };
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

  const badgeConfig = analysis ? getMatchBadgeConfig(analysis.recommendation, analysis.overallScore) : null;

  return (
    <div className={`rounded-lg border border-line-200 bg-surface-0 p-5 space-y-5 ${className}`}>
      {/* Header & Controls */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line-200 pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-base font-semibold text-ink-900">AI Smart Match Analysis</h3>
            {badgeConfig && (
              <Badge variant={badgeConfig.variant} className="font-bold text-xs">
                {analysis?.overallScore}% Match ({badgeConfig.label})
              </Badge>
            )}
          </div>
          <p className="mt-0.5 text-xs text-ink-500">
            Powered by Claude AI candidate compatibility scoring & criteria evaluation
          </p>
        </div>

        <Button
          onClick={runMatchAnalysis}
          disabled={loading || isApiKeyMissing || !jobPostingId}
          variant="secondary"
          className="h-8 px-3 text-xs flex items-center gap-1.5"
        >
          <svg className="h-3.5 w-3.5 text-primary-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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
          className="rounded-md border border-amber-300 bg-amber-50 p-4 text-amber-900"
        >
          <div className="flex items-start gap-3">
            <svg className="h-5 w-5 text-amber-600 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            <div>
              <h4 className="font-semibold text-sm text-amber-900">AI Features Unconfigured: API key required</h4>
              <p className="mt-1 text-xs text-amber-800 leading-relaxed">
                An AI Provider API Key (Claude) has not been configured for this installation. Candidate Smart Match
                features are currently disabled. Manual candidate profiling and evaluation remain fully operational.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* General Non-402 Error State */}
      {error && !isApiKeyMissing && (
        <div className="rounded-md border border-danger-200 bg-danger-50 p-4 text-xs text-danger-800 flex items-center justify-between">
          <span>{error.message || 'An error occurred while running candidate match analysis.'}</span>
          <Button onClick={runMatchAnalysis} variant="secondary" className="h-7 px-2.5 text-xs text-danger-700">
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
          {/* Summary Banner */}
          <div className="rounded-md bg-surface-50 p-4 border border-line-200">
            <div className="flex items-center justify-between gap-2 mb-2">
              <span className="text-xs font-bold uppercase tracking-wider text-ink-500">Overall Match Summary</span>
              <span className="text-sm font-bold text-ink-900">{analysis.overallScore}% Overall Score</span>
            </div>
            <p className="text-sm text-ink-800 leading-relaxed">{analysis.summary}</p>
          </div>

          {/* Strengths & Gaps */}
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {/* Strengths */}
            <div className="rounded-md border border-success-200 bg-success-50/40 p-4">
              <h4 className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-success-800 mb-2">
                <svg className="h-4 w-4 text-success-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
                Key Strengths ({analysis.strengths?.length || 0})
              </h4>
              {analysis.strengths && analysis.strengths.length > 0 ? (
                <ul className="space-y-1.5 text-xs text-ink-800">
                  {analysis.strengths.map((str, idx) => (
                    <li key={idx} className="flex items-start gap-2">
                      <span className="text-success-600 font-bold">•</span>
                      <span>{str}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-xs text-ink-500 italic">No specific strengths identified.</p>
              )}
            </div>

            {/* Gaps */}
            <div className="rounded-md border border-warning-200 bg-warning-50/40 p-4">
              <h4 className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-warning-800 mb-2">
                <svg className="h-4 w-4 text-warning-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                  />
                </svg>
                Identified Gaps ({analysis.gaps?.length || 0})
              </h4>
              {analysis.gaps && analysis.gaps.length > 0 ? (
                <ul className="space-y-1.5 text-xs text-ink-800">
                  {analysis.gaps.map((gap, idx) => (
                    <li key={idx} className="flex items-start gap-2">
                      <span className="text-warning-600 font-bold">•</span>
                      <span>{gap}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-xs text-ink-500 italic">No critical gaps identified.</p>
              )}
            </div>
          </div>

          {/* Criteria Breakdown Table */}
          {analysis.criteria && analysis.criteria.length > 0 && (
            <div>
              <h4 className="text-xs font-bold uppercase tracking-wider text-ink-500 mb-3">
                Criteria Compatibility Breakdown
              </h4>
              <div className="space-y-2.5">
                {analysis.criteria.map((c, idx) => (
                  <div key={idx} className="rounded-md border border-line-200 bg-surface-50 p-3 text-xs">
                    <div className="flex items-center justify-between gap-2 mb-1">
                      <span className="font-semibold text-ink-900">{c.criterion}</span>
                      <span
                        className={`font-bold text-xs px-2 py-0.5 rounded ${
                          c.score >= 80
                            ? 'bg-success-100 text-success-800'
                            : c.score >= 60
                            ? 'bg-primary-100 text-primary-800'
                            : c.score >= 40
                            ? 'bg-warning-100 text-warning-800'
                            : 'bg-danger-100 text-danger-800'
                        }`}
                      >
                        {c.score}% Match
                      </span>
                    </div>
                    <p className="text-ink-600 leading-normal">{c.rationale}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Suggested Interview Questions */}
          {analysis.suggestedInterviewQuestions && analysis.suggestedInterviewQuestions.length > 0 && (
            <div className="rounded-md border border-line-200 bg-surface-50 p-4">
              <h4 className="text-xs font-bold uppercase tracking-wider text-ink-500 mb-2.5">
                Suggested Interview Questions
              </h4>
              <ol className="list-decimal list-inside space-y-2 text-xs text-ink-800">
                {analysis.suggestedInterviewQuestions.map((q, idx) => (
                  <li key={idx} className="leading-relaxed pl-1">
                    <span className="font-medium">{q}</span>
                  </li>
                ))}
              </ol>
            </div>
          )}
        </div>
      )}

      {!loading && !analysis && !error && !isApiKeyMissing && (
        <div className="py-8 text-center text-xs text-ink-500">
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
