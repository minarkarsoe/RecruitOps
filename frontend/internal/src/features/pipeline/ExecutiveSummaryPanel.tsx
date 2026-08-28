import { useState, useCallback } from 'react';
// `Badge` went with the "Burmese Enabled" pill, which read `isBilingual` — a field the API
// has never returned.
import { Button, SkeletonCard, SkeletonText } from '@recruitops/ui';
import type { ExecutiveSummaryResult } from '@recruitops/types';
import { aiApi, ApiError } from '../../lib/api';

/**
 * The kit's detented filter group (`design/internal/board.html` toolbar): one bordered track,
 * the active segment filled `bg-ink-900`, the rest quiet text.
 *
 * It replaces two hand-rolled groups of butt-joined bordered buttons. Those had the active
 * segment in brand and ink respectively — two different meanings for "this one is selected" on
 * one row — and the shared borders doubled to 2px between segments, so the track was unevenly
 * ruled. The kit uses ink for selection precisely because brand is the *action* colour: a
 * filter is not an action, and painting it brand made "Bilingual" look like a button that
 * would go and do something.
 *
 * `aria-pressed` is what makes the group readable to a screen reader — without it the selected
 * segment is a colour and nothing else.
 */
function Segmented<T extends string>({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: T;
  onChange: (value: T) => void;
  options: { value: T; label: string }[];
}) {
  return (
    <div className="flex shrink-0 items-center rounded-md border border-line p-0.5" role="group" aria-label={label}>
      {options.map((option) => {
        const isActive = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={isActive}
            onClick={() => onChange(option.value)}
            className={`h-7 rounded px-2.5 text-sm transition-colors ${
              isActive ? 'bg-ink-900 font-medium text-white' : 'text-ink-600 hover:text-ink-900'
            }`}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}

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
      // `language` is deliberately NOT sent. The API's request record is
      // (CandidateId, JobPostingId, Tone) — it has never had a language field, so sending one
      // only put a value on the wire for model binding to discard. Sending nothing is the
      // honest version of the same behaviour, and it makes the gap visible in the type rather
      // than hidden in a dropped property. See the note on the Language control below.
      const result = await aiApi.generateExecutiveSummary({
        candidateId,
        jobPostingId,
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
  }, [candidateId, jobPostingId, language]);

  const handleCopy = () => {
    if (!summaryResult) return;
    const textToCopy = [
      `# Executive Summary: ${candidateName}`,
      `Headline: ${summaryResult.headline}`,
      `\n${summaryResult.executiveSummary}`,
      summaryResult.keyHighlights?.length
        ? `\nKey Strengths:\n${summaryResult.keyHighlights.map((s) => `- ${s}`).join('\n')}`
        : '',
      summaryResult.recommendedInterviewQuestions?.length
        ? `\nSuggested Interview Questions:\n${summaryResult.recommendedInterviewQuestions.map((q) => `- ${q}`).join('\n')}`
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
      `Language: ${language.toUpperCase()}`,
      `--------------------------------------------------`,
      `Headline: ${summaryResult.headline}`,
      `\nSummary:\n${summaryResult.executiveSummary}`,
      summaryResult.keyHighlights?.length
        ? `\nKey Strengths:\n${summaryResult.keyHighlights.map((s) => `* ${s}`).join('\n')}`
        : '',
      summaryResult.recommendedInterviewQuestions?.length
        ? `\nSuggested Interview Questions:\n${summaryResult.recommendedInterviewQuestions.map((q) => `* ${q}`).join('\n')}`
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
    <div className={`space-y-5 rounded-lg border border-line bg-white p-5 ${className}`}>
      {/* Panel Header */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-base font-semibold text-ink-900">Executive Candidate Summary</h3>
          </div>
          <p className="mt-0.5 text-sm text-ink-500">
            Powered by Gemini AI candidate profiling and localization
          </p>
        </div>

        {/* Action button */}
        <Button onClick={generateSummary} disabled={loading || isApiKeyMissing} className="gap-1.5">
          <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          {loading ? 'Generating...' : 'Generate AI Summary'}
        </Button>
      </div>

      {/* The Audience selector that used to sit to the right of this is gone (2026-08-28). It
          offered "Internal Recruiter" and "Client Portal", and clients were deleted by ADR-0001
          on 2026-07-27 — there is no client portal for a summary to be made safe for. The API
          never accepted the field either, so switching it had never changed anything.

          ⚠️ `language` is still not read by the API. `GenerateExecutiveSummaryRequest` accepts
          `candidateId`, `jobPostingId` and `tone` only, so this control is currently decorative.
          It is kept rather than removed because bilingual output is a real product requirement
          (ADR-0009) and wiring it needs a backend change; the alternative was deleting the only
          Burmese affordance on the panel. Tracked in NEXT-SESSION. */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <span className="text-sm text-ink-500">Language</span>
          <Segmented
            label="Summary language"
            value={language}
            onChange={setLanguage}
            options={[
              { value: 'en', label: 'EN (English)' },
              { value: 'my', label: 'MY (Burmese)' },
              { value: 'bilingual', label: 'Bilingual' },
            ]}
          />
        </div>
      </div>

      {/* 402 API Key Gating Graceful Alert Banner */}
      {isApiKeyMissing && (
        <div
          data-testid="executive-summary-402-banner"
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
                An AI Provider API Key (Gemini) has not been configured for this installation. Executive Summary
                generation is currently disabled.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Non-402 Error Banner */}
      {error && !isApiKeyMissing && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-critical-100 bg-critical-50 p-4 text-sm text-critical-700">
          <span>{error.message || 'Failed to generate AI executive summary.'}</span>
          <Button onClick={generateSummary} variant="secondary">
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
            <Button onClick={handleCopy} variant="secondary" className="gap-1.5">
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

            <Button onClick={handleExport} variant="secondary" className="gap-1.5">
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
          <div className="rounded-md border-l-2 border-brand-700 bg-brand-50 p-4">
            <h4 className="text-base font-semibold text-ink-900">{summaryResult.headline}</h4>
          </div>

          {/* Summary Text. `.mm` because this panel renders Burmese whenever the language
              toggle is on MY or Bilingual, and Burmese diacritics clip at the 20px line box. */}
          <div
            className={`whitespace-pre-wrap rounded-md border border-line bg-canvas p-4 text-base text-ink-800 ${
              language === 'en' ? 'leading-6' : 'mm'
            }`}
          >
            {summaryResult.executiveSummary}
          </div>

          {/* Key Strengths */}
          {summaryResult.keyHighlights && summaryResult.keyHighlights.length > 0 && (
            <div className="space-y-2 rounded-md border border-line bg-canvas p-4">
              <h4 className="text-2xs font-medium uppercase tracking-wider text-ink-500">Key Qualifications &amp; Strengths</h4>
              <ul className="space-y-1.5 text-base text-ink-800">
                {summaryResult.keyHighlights.map((st, idx) => (
                  <li key={idx} className="flex items-start gap-2">
                    <span className="text-brand-700">•</span>
                    <span>{st}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Suggested Questions */}
          {summaryResult.recommendedInterviewQuestions && summaryResult.recommendedInterviewQuestions.length > 0 && (
            <div className="space-y-2 rounded-md border border-line bg-canvas p-4">
              <h4 className="text-2xs font-medium uppercase tracking-wider text-ink-500">Suggested Interview Questions</h4>
              <ol className="list-inside list-decimal space-y-1.5 text-base text-ink-800">
                {summaryResult.recommendedInterviewQuestions.map((q, idx) => (
                  <li key={idx} className="pl-1 leading-5">
                    {q}
                  </li>
                ))}
              </ol>
            </div>
          )}
        </div>
      )}

      {!loading && !summaryResult && !error && !isApiKeyMissing && (
        <div className="py-8 text-center text-sm text-ink-600">
          Click &quot;Generate AI Summary&quot; to synthesize candidate qualifications using Gemini AI.
        </div>
      )}
    </div>
  );
}
