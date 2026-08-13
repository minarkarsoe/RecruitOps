import { apiFetch } from '../../lib/api';
import type { SearchResponse, SearchQueryParameters } from '@recruitops/types';

export const searchApi = {
  /**
   * Executes a full-text search query against GET /api/search via apiFetch.
   *
   * @param params Query string, category filter, and pagination options.
   * @param signal Optional AbortSignal to cancel pending fetch requests.
   */
  search: async (
    params: SearchQueryParameters,
    signal?: AbortSignal
  ): Promise<SearchResponse> => {
    const trimmed = params.q?.trim() ?? '';

    // Short-circuit empty queries to prevent unnecessary requests
    if (!trimmed) {
      return {
        query: '',
        normalizedQuery: '',
        category: params.category ?? 'All',
        totalMatches: 0,
        categoryCounts: { all: 0, candidates: 0, postings: 0, requisitions: 0 },
        items: [],
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
        totalPages: 0,
      };
    }

    const queryParams = new URLSearchParams();
    queryParams.append('q', trimmed);
    if (params.category && params.category !== 'All') {
      queryParams.append('category', params.category);
    }
    if (params.page !== undefined) {
      queryParams.append('page', params.page.toString());
    }
    if (params.pageSize !== undefined) {
      queryParams.append('pageSize', params.pageSize.toString());
    }

    return apiFetch<SearchResponse>(`/search?${queryParams.toString()}`, { signal });
  },
};
