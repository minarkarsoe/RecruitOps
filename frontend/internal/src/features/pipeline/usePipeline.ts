import { useCallback, useState } from 'react';
import type {
  Interview,
  PipelineItem,
  PipelineStatus,
  StageHistoryItem,
} from '@recruitops/types';
import { api } from '../../lib/api';

export interface UsePipelineOptions {
  postingId?: string;
  autoLoad?: boolean;
}

export function usePipeline(options: UsePipelineOptions = {}) {
  const { postingId: initialPostingId } = options;

  const [pipeline, setPipeline] = useState<PipelineItem[]>([]);
  const [selectedCandidateId, setSelectedCandidateId] = useState<string | null>(null);
  const [stageHistory, setStageHistory] = useState<StageHistoryItem[]>([]);
  const [interviews, setInterviews] = useState<Interview[]>([]);
  const [activeTab, setActiveTab] = useState<string>('overview');
  const [loading, setLoading] = useState<boolean>(false);
  const [movingStage, setMovingStage] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [sourceFilter, setSourceFilter] = useState<string>('all');

  const loadPipeline = useCallback(async (jobPostingId?: string) => {
    const targetId = jobPostingId || initialPostingId;
    if (!targetId) return;

    setLoading(true);
    setError(null);
    try {
      const data = await api<PipelineItem[]>(`/jobpostings/${targetId}/pipeline`);
      setPipeline(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load pipeline candidates');
    } finally {
      setLoading(false);
    }
  }, [initialPostingId]);

  const loadStageHistory = useCallback(async (applicationId: string) => {
    try {
      const history = await api<StageHistoryItem[]>(`/applications/${applicationId}/history`);
      setStageHistory(history);
    } catch (e) {
      // Fallback empty if history endpoint not present or errors
      setStageHistory([]);
    }
  }, []);

  const loadCandidateInterviews = useCallback(async (applicationId: string) => {
    try {
      const data = await api<Interview[]>(`/applications/${applicationId}/interviews`);
      setInterviews(data);
    } catch (e) {
      setInterviews([]);
    }
  }, []);

  const selectCandidate = useCallback((candidateId: string | null) => {
    setSelectedCandidateId(candidateId);
    if (candidateId) {
      // Look up application ID from candidate ID or item ID
      const candidateItem = pipeline.find((p) => p.candidateId === candidateId || p.id === candidateId);
      if (candidateItem) {
        loadStageHistory(candidateItem.id);
        loadCandidateInterviews(candidateItem.id);
      }
    } else {
      setStageHistory([]);
      setInterviews([]);
    }
  }, [pipeline, loadStageHistory, loadCandidateInterviews]);

  const moveStage = async (
    applicationId: string,
    toStatus: PipelineStatus,
    note?: string,
    jobPostingId?: string
  ) => {
    setMovingStage(true);
    setError(null);
    try {
      await api(`/applications/${applicationId}/stage`, {
        method: 'POST',
        body: JSON.stringify({ toStatus, note: note || null }),
      });
      await loadPipeline(jobPostingId || initialPostingId);
      if (selectedCandidateId) {
        await loadStageHistory(applicationId);
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to move candidate stage';
      setError(msg);
      throw e;
    } finally {
      setMovingStage(false);
    }
  };

  const selectedCandidate = pipeline.find(
    (item) => item.candidateId === selectedCandidateId || item.id === selectedCandidateId
  ) || null;

  const filteredPipeline = pipeline.filter((item) => {
    if (sourceFilter !== 'all' && item.source !== sourceFilter) {
      return false;
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const nameMatch = item.candidateName.toLowerCase().includes(q);
      const emailMatch = item.email?.toLowerCase().includes(q) ?? false;
      const phoneMatch = item.phone?.toLowerCase().includes(q) ?? false;
      return nameMatch || emailMatch || phoneMatch;
    }
    return true;
  });

  return {
    pipeline,
    filteredPipeline,
    selectedCandidateId,
    selectedCandidate,
    stageHistory,
    interviews,
    activeTab,
    loading,
    movingStage,
    error,
    searchQuery,
    sourceFilter,
    setSelectedCandidateId: selectCandidate,
    setActiveTab,
    setSearchQuery,
    setSourceFilter,
    loadPipeline,
    loadStageHistory,
    loadCandidateInterviews,
    moveStage,
  };
}
