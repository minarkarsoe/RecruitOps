import { useCallback, useState } from 'react';
import type {
  ApprovalDecisionRequest,
  CreateRequisitionRequest,
  RequisitionDetail,
  RequisitionListItem,
} from '@recruitops/types';
import { api } from '../../lib/api';

export interface UseRequisitionsOptions {
  initialDepartmentId?: string;
}

export function useRequisitions(_options: UseRequisitionsOptions = {}) {

  const [items, setItems] = useState<RequisitionListItem[] | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<RequisitionDetail | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [actionBusy, setActionBusy] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Filters and sorting state
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [sortBy, setSortBy] = useState<'title' | 'departmentName' | 'headcount' | 'salaryBudget' | 'submittedAt'>('title');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  const loadRequisitions = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api<RequisitionListItem[]>('/requisitions');
      setItems(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load requisitions');
    } finally {
      setLoading(false);
    }
  }, []);

  const loadDetail = useCallback(async (id: string) => {
    setLoading(true);
    setError(null);
    try {
      const data = await api<RequisitionDetail>(`/requisitions/${id}`);
      setDetail(data);
      setSelectedId(id);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load requisition detail');
      setDetail(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const selectRequisition = useCallback((id: string | null) => {
    setSelectedId(id);
    if (id) {
      loadDetail(id);
    } else {
      setDetail(null);
    }
  }, [loadDetail]);

  const submitRequisition = async (id: string): Promise<RequisitionDetail> => {
    setActionBusy(true);
    setError(null);
    try {
      const updated = await api<RequisitionDetail>(`/requisitions/${id}/submit`, { method: 'POST' });
      setDetail(updated);
      await loadRequisitions();
      return updated;
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to submit requisition';
      setError(msg);
      throw e;
    } finally {
      setActionBusy(false);
    }
  };

  const decideRequisition = async (id: string, approve: boolean, comment?: string): Promise<RequisitionDetail> => {
    setActionBusy(true);
    setError(null);
    try {
      const body: ApprovalDecisionRequest = { approve, comment: comment || null };
      const updated = await api<RequisitionDetail>(`/requisitions/${id}/decision`, {
        method: 'POST',
        body: JSON.stringify(body),
      });
      setDetail(updated);
      await loadRequisitions();
      return updated;
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to record decision';
      setError(msg);
      throw e;
    } finally {
      setActionBusy(false);
    }
  };

  const cancelRequisition = async (id: string): Promise<RequisitionDetail> => {
    setActionBusy(true);
    setError(null);
    try {
      const updated = await api<RequisitionDetail>(`/requisitions/${id}/cancel`, { method: 'POST' });
      setDetail(updated);
      await loadRequisitions();
      return updated;
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to cancel requisition';
      setError(msg);
      throw e;
    } finally {
      setActionBusy(false);
    }
  };

  const createRequisition = async (req: CreateRequisitionRequest): Promise<RequisitionDetail> => {
    setActionBusy(true);
    setError(null);
    try {
      const created = await api<RequisitionDetail>('/requisitions', {
        method: 'POST',
        body: JSON.stringify(req),
      });
      await loadRequisitions();
      return created;
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to create requisition';
      setError(msg);
      throw e;
    } finally {
      setActionBusy(false);
    }
  };

  // Filter and sort items
  const filteredItems = (items || []).filter((item) => {
    if (statusFilter !== 'all' && item.status !== statusFilter) {
      return false;
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const titleMatch = item.title.toLowerCase().includes(q);
      const deptMatch = item.departmentName.toLowerCase().includes(q);
      const awaitingMatch = item.awaitingApprovalFrom?.toLowerCase().includes(q) ?? false;
      return titleMatch || deptMatch || awaitingMatch;
    }
    return true;
  }).sort((a, b) => {
    let comparison = 0;
    if (sortBy === 'title') {
      comparison = a.title.localeCompare(b.title);
    } else if (sortBy === 'departmentName') {
      comparison = a.departmentName.localeCompare(b.departmentName);
    } else if (sortBy === 'headcount') {
      comparison = a.headcount - b.headcount;
    } else if (sortBy === 'salaryBudget') {
      comparison = (a.salaryBudget ?? 0) - (b.salaryBudget ?? 0);
    } else if (sortBy === 'submittedAt') {
      const dateA = a.submittedAt ? new Date(a.submittedAt).getTime() : 0;
      const dateB = b.submittedAt ? new Date(b.submittedAt).getTime() : 0;
      comparison = dateA - dateB;
    }
    return sortOrder === 'asc' ? comparison : -comparison;
  });

  return {
    items,
    filteredItems,
    selectedId,
    detail,
    loading,
    actionBusy,
    error,
    statusFilter,
    searchQuery,
    sortBy,
    sortOrder,
    setSelectedId: selectRequisition,
    setStatusFilter,
    setSearchQuery,
    setSortBy,
    setSortOrder,
    loadRequisitions,
    loadDetail,
    submitRequisition,
    decideRequisition,
    cancelRequisition,
    createRequisition,
  };
}
