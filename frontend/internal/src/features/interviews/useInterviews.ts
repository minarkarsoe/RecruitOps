import { useCallback, useState } from 'react';
import type {
  HireRecommendation,
  Interview,
  InterviewScorecards,
  MyScorecard,
  SaveScorecardRequest,
} from '@recruitops/types';
import { api, ApiError } from '../../lib/api';
import { type Draft, draftsFrom, toAnswers } from '../../lib/scorecard';

export interface UseInterviewsOptions {
  interviewId?: string;
  autoLoad?: boolean;
}

export function useInterviews(options: UseInterviewsOptions = {}) {
  const { interviewId: initialInterviewId } = options;

  const [interview, setInterview] = useState<Interview | null>(null);
  const [mine, setMine] = useState<MyScorecard | null>(null);
  const [panel, setPanel] = useState<InterviewScorecards | null>(null);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [recommendation, setRecommendation] = useState<HireRecommendation | ''>('');
  const [summary, setSummary] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(false);
  const [busy, setBusy] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);

  const loadInterviewData = useCallback(async (id?: string) => {
    const targetId = id || initialInterviewId;
    if (!targetId) return;

    setLoading(true);
    setError(null);
    try {
      const [iv, scorecards] = await Promise.all([
        api<Interview>(`/interviews/${targetId}`),
        api<InterviewScorecards>(`/interviews/${targetId}/scorecards`),
      ]);
      setInterview(iv);
      setPanel(scorecards);

      try {
        const my = await api<MyScorecard>(`/interviews/${targetId}/scorecard`);
        setMine(my);
        setDrafts(draftsFrom(my.scorecard));
        setRecommendation(my.scorecard?.recommendation ?? '');
        setSummary(my.scorecard?.summaryComment ?? '');
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) {
          setMine(null);
        } else {
          throw e;
        }
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load interview details');
    } finally {
      setLoading(false);
    }
  }, [initialInterviewId]);

  const writeScorecard = async (submit: boolean, targetId?: string) => {
    const activeId = targetId || interview?.id || initialInterviewId;
    if (!activeId || !mine) return;

    const body: SaveScorecardRequest = {
      recommendation: recommendation || null,
      summaryComment: summary.trim() || null,
      answers: toAnswers(mine.criteria, drafts),
    };

    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      await api(`/interviews/${activeId}/scorecard${submit ? '/submit' : ''}`, {
        method: submit ? 'POST' : 'PUT',
        body: JSON.stringify(body),
      });
      await loadInterviewData(activeId);
      setSaved(submit ? 'Submitted successfully.' : 'Draft saved.');
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Action failed';
      setError(msg);
      throw e;
    } finally {
      setBusy(false);
    }
  };

  const saveDraft = async (targetId?: string) => {
    return writeScorecard(false, targetId);
  };

  const submitEvaluation = async (targetId?: string) => {
    return writeScorecard(true, targetId);
  };

  return {
    interview,
    mine,
    panel,
    drafts,
    recommendation,
    summary,
    loading,
    busy,
    error,
    saved,
    setDrafts,
    setRecommendation,
    setSummary,
    loadInterviewData,
    saveDraft,
    submitEvaluation,
  };
}
