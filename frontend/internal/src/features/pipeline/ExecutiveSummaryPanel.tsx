import { useState, useCallback } from 'react';
import { Badge, Button, SkeletonCard, SkeletonText } from '@recruitops/ui';
import type { ExecutiveSummaryResult } from '@recruitops/types';
import { aiApi, ApiError } from '../../lib/api';

export interface ExecutiveSummaryPanelProps {
  candidateId: string;
  jobPostingId?: string;
  candidateName?: string;
  initialSummary?: ExecutiveSummaryResult | null;
  className?: string;
}

export function ExecutiveSummaryPanel({
  candidateId,
  jobPostingId,
  candidateName = 'Candidate',
  initialSummary = null,
  className = '',
}: ExecutiveSummaryPanelProps) {
  const [summaryResult, setSummaryResult] = useState<ExecutiveSummaryResult | null>(initialSummary);
  const [language, setLanguage] = useState<'en' | 'my' | 'bilingual'>('en');
  const [audience, setAudience] = useState<'internal' | 'client'>('internal');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<ApiError | Error | null>(null);
  const [isApiKeyMissing, setIsApiKeyMissing] = useState(false);
  const [copied, setCopied] = useState(false);

  const generateSummary = useCallback(async () => {
    if (!candidateId) return;
    setLoading(true);
    setError(null);
    setIsApiKeyMissing(false);

    try {
      const result = await aiApi.generateExecutiveSummary({
        candidateId,
        jobPostingId,
        audience,
        language,
      });
      setSummaryResult(result);
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setIsApiKeyMissing(true);
        setError(err);
      } else if (err instanceof ApiError) {
        setError(err);
      } else {
        setError(err instanceof Error ? err : new Error('Failed to generate AI executive summary.'));
      }
    } finally {
      setLoading(false);
    }
  }, [candidateId, jobPostingId, audience, language]);

  const handleCopy = () => {
    if (!summaryResult) return;
    const textToCopy = [
      `# Executive Summary: ${candidateName}`,
      `Headline: ${summaryResult.headline}`,
      `\n${summaryResult.summary}`,
      summaryResult.keyStrengths?.length
        ? `\nKey Strengths:\n${summaryResult.keyStrengths.map((s) => `- ${s}`).join('\n')}`
        : '',
      summaryResult.suggestedInterviewQuestions?.length
        ? `\nSuggested Interview Questions:\n${summaryResult.suggestedInterviewQuestions.map((q) => `- ${q}`).join('\n')}`
        : '',
    ]
      .filter(Boolean)
      .join('\n');

    navigator.clipboard.writeText(textToCopy);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleExport = () => {
    if (!summaryResult) return;
    const textContent = [
      `# Executive Candidate Summary — ${candidateName}`,
      `Generated on: ${new Date().toLocaleDateString()}`,
      `Audience: ${audience.toUpperCase()} | Language: ${language.toUpperCase()}`,
      `--------------------------------------------------`,
      `Headline: ${summaryResult.headline}`,
      `\nSummary:\n${summaryResult.summary}`,
      summaryResult.keyStrengths?.length
        ? `\nKey Strengths:\n${summaryResult.keyStrengths.map((s) => `* ${s}`).join('\n')}`
        : '',
      summaryResult.suggestedInterviewQuestions?.length
        ? `\nSuggested Interview Questions:\n${summaryResult.suggestedInterviewQuestions.map((q) => `* ${q}`).join('\n')}`
        : '',
    ]
      .filter(Boolean)
      .join('\n');

    const blob = new Blob([textContent], { type: 'text/markdown;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `Executive_Summary_${candidateName.replace(/\s+/g, '_')}.md`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <div className={`rounded-lg border border-line-200 bg-surface-0 p-5 space-y-5 ${className}`}>
      {/* Panel Header */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line-200 pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-base font-semibold text-ink-900">Executive Candidate Summary</h3>
            {summaryResult?.isBilingual && <Badge variant="cyan">Burmese Enabled</Badge>}
          </div>
          <p className="mt-0.5 text-xs text-ink-500">
            Powered by Gemini AI candidate profiling and localization
          </p>
        </div>

        {/* Action button */}
        <Button
          onClick={generateSummary}
          disabled={loading || isApiKeyMissing}
          className="bg-primary-600 hover:bg-primary-700 text-white h-8 px-3 text-xs flex items-center gap-1.5"
        >
          <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          {loading ? 'Generating...' : 'Generate AI Summary'}
        </Button>
      </div>

      {/* Controls toolbar: Language toggle group & Audience selector */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-md bg-surface-50 p-3 border border-line-200">
        {/* Language Toggle Button Group */}
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold text-ink-700">Language:</span>
          <div className="inline-flex rounded-md shadow-xs" role="group">
            <button
              type="button"
              onClick={() => setLanguage('en')}
              className={`px-3 py-1 text-xs font-medium rounded-l-md border ${
                language === 'en'
                  ? 'bg-primary-600 text-white border-primary-600'
                  : 'bg-surface-0 text-ink-700 border-line-300 hover:bg-surface-100'
              }`}
            >
              EN (English)
            </button>
            <button
              type="button"
              onClick={() => setLanguage('my')}
              className={`px-3 py-1 text-xs font-medium border-t border-b ${
                language === 'my'
                  ? 'bg-primary-600 text-white border-primary-600'
                  : 'bg-surface-0 text-ink-700 border-line-300 hover:bg-surface-100'
              }`}
            >
              MY (Burmese)
            </button>
            <button
              type="button"
              onClick={() => setLanguage('bilingual')}
              className={`px-3 py-1 text-xs font-medium rounded-r-md border ${
                language === 'bilingual'
                  ? 'bg-primary-600 text-white border-primary-600'
                  : 'bg-surface-0 text-ink-700 border-line-300 hover:bg-surface-100'
              }`}
            >
              Bilingual
            </button>
          </div>
        </div>

        {/* Audience Toggle */}
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold text-ink-700">Audience:</span>
          <div className="inline-flex rounded-md shadow-xs" role="group">
            <button
              type="button"
              onClick={() => setAudience('internal')}
              className={`px-2.5 py-1 text-xs font-medium rounded-l-md border ${
                audience === 'internal'
                  ? 'bg-ink-800 text-white border-ink-800'
                  : 'bg-surface-0 text-ink-700 border-line-300 hover:bg-surface-100'
              }`}
            >
              Internal Recruiter
            </button>
            <button
              type="button"
              onClick={() => setAudience('client')}
              className={`px-2.5 py-1 text-xs font-medium rounded-r-md border ${
                audience === 'client'
                  ? 'bg-ink-800 text-white border-ink-800'
                  : 'bg-surface-0 text-ink-700 border-line-300 hover:bg-surface-100'
              }`}
            >
              Client Portal
            </button>
          </div>
        </div>
      </div>

      {/* 402 API Key Gating Graceful Alert Banner */}
      {isApiKeyMissing && (
        <div
          data-testid="executive-summary-402-banner"
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
                An AI Provider API Key (Gemini) has not been configured for this installation. Executive Summary
                generation is currently disabled.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Non-402 Error Banner */}
      {error && !isApiKeyMissing && (
        <div className="rounded-md border border-danger-200 bg-danger-50 p-4 text-xs text-danger-800 flex items-center justify-between">
          <span>{error.message || 'Failed to generate AI executive summary.'}</span>
          <Button onClick={generateSummary} variant="secondary" className="h-7 px-2.5 text-xs text-danger-700">
            Retry
          </Button>
        </div>
      )}

      {/* Loading Skeleton State */}
      {loading && (
        <div data-testid="executive-summary-skeleton" className="space-y-4 py-2">
          <SkeletonCard className="h-16" />
          <SkeletonText lines={5} />
          <SkeletonText lines={3} />
        </div>
      )}

      {/* Generated Summary Results */}
      {!loading && summaryResult && !isApiKeyMissing && (
        <div className="space-y-5">
          {/* Quick Actions toolbar (Copy & Export) */}
          <div className="flex items-center justify-end gap-2 pt-1">
            <Button onClick={handleCopy} variant="secondary" className="h-7 px-2.5 text-xs flex items-center gap-1">
              <svg className="h-3.5 w-3.5 text-ink-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"
                />
              </svg>
              {copied ? 'Copied!' : 'Copy Summary'}
            </Button>

            <Button onClick={handleExport} variant="secondary" className="h-7 px-2.5 text-xs flex items-center gap-1">
              <svg className="h-3.5 w-3.5 text-ink-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"
                />
              </svg>
              Export (.md)
            </Button>
          </div>

          {/* Headline Banner */}
          <div className="rounded-md border-l-4 border-primary-600 bg-primary-50/50 p-4">
            <h4 className="text-sm font-bold text-ink-900">{summaryResult.headline}</h4>
          </div>

          {/* Summary Text */}
          <div className="rounded-md border border-line-200 bg-surface-50 p-4 text-xs text-ink-800 leading-relaxed whitespace-pre-wrap">
            {summaryResult.summary}
          </div>

          {/* Key Strengths */}
          {summaryResult.keyStrengths && summaryResult.keyStrengths.length > 0 && (
            <div className="rounded-md border border-line-200 bg-surface-50 p-4 space-y-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-ink-500">Key Qualifications & Strengths</h4>
              <ul className="space-y-1.5 text-xs text-ink-800">
                {summaryResult.keyStrengths.map((st, idx) => (
                  <li key={idx} className="flex items-start gap-2">
                    <span className="text-primary-600 font-bold">•</span>
                    <span>{st}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Suggested Questions */}
          {summaryResult.suggestedInterviewQuestions && summaryResult.suggestedInterviewQuestions.length > 0 && (
            <div className="rounded-md border border-line-200 bg-surface-50 p-4 space-y-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-ink-500">Suggested Interview Questions</h4>
              <ol className="list-decimal list-inside space-y-1.5 text-xs text-ink-800">
                {summaryResult.suggestedInterviewQuestions.map((q, idx) => (
                  <li key={idx} className="leading-relaxed pl-1">
                    <span>{q}</span>
                  </li>
                ))}
              </ol>
            </div>
          )}
        </div>
      )}

      {!loading && !summaryResult && !error && !isApiKeyMissing && (
        <div className="py-8 text-center text-xs text-ink-500">
          Click &quot;Generate AI Summary&quot; to synthesize candidate qualifications using Gemini AI.
        </div>
      )}
    </div>
  );
}
