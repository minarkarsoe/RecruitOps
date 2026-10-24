import { auth } from '../../lib/auth';
import { apiFetch, ApiError, tenantHeader } from '../../lib/api';
import type {
  ChannelGroup,
  ExecutiveDashboardDto,
  PipelineAnalyticsDto,
  PipelineWindow,
  ReportingPeriodKind,
  ReportQueryRequestDto,
  ReportQueryResultDto,
} from '@recruitops/types';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

export interface ExecutiveDashboardQuery {
  period: ReportingPeriodKind;
  departmentId?: string;
  recruiterId?: string;
}

export interface PipelineAnalyticsQuery {
  window: PipelineWindow;
  postingId?: string;
  source?: ChannelGroup;
}

// The August endpoints — `kpis`, `time-to-hire`, `conversion`, `source-of-hire` — are deliberately absent:
// no screen draws them any more (ADR-0031 §5, ADR-0032 §7). They stay for API callers.
export const analyticsApi = {
  getExecutive: ({ period, departmentId, recruiterId }: ExecutiveDashboardQuery) => {
    const params = new URLSearchParams({ period });
    if (departmentId) params.set('departmentId', departmentId);
    if (recruiterId) params.set('recruiterId', recruiterId);
    return apiFetch<ExecutiveDashboardDto>(`/analytics/executive?${params}`);
  },
  getPipeline: ({ window, postingId, source }: PipelineAnalyticsQuery) => {
    const params = new URLSearchParams({ window });
    if (postingId) params.set('postingId', postingId);
    if (source) params.set('source', source);
    return apiFetch<PipelineAnalyticsDto>(`/analytics/pipeline?${params}`);
  },
  queryReport: (request: ReportQueryRequestDto) =>
    apiFetch<ReportQueryResultDto>('/analytics/reports/query', {
      method: 'POST',
      body: JSON.stringify(request),
    }),
  exportReportCsv: async (request: ReportQueryRequestDto): Promise<Blob> => {
    const session = auth.get();
    const params = new URLSearchParams();
    if (request.dateFrom) params.append('dateFrom', request.dateFrom);
    if (request.dateTo) params.append('dateTo', request.dateTo);
    if (request.departmentId) params.append('departmentId', request.departmentId);
    if (request.jobPostingId) params.append('jobPostingId', request.jobPostingId);
    if (request.stages) {
      request.stages.forEach((s) => params.append('stages', s));
    }
    if (request.columns) {
      request.columns.forEach((c) => params.append('columns', c));
    }

    const queryString = params.toString();
    const url = `${BASE}/analytics/reports/export${queryString ? `?${queryString}` : ''}`;

    const res = await fetch(url, {
      headers: {
        ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
        ...tenantHeader(session),
      },
    });

    if (!res.ok) {
      throw new ApiError(res.status, 'Failed to export CSV report');
    }

    return await res.blob();
  },
  downloadReportCsv: async (request: ReportQueryRequestDto, filename = 'report.csv'): Promise<void> => {
    const blob = await analyticsApi.exportReportCsv(request);
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    if (typeof window.URL.revokeObjectURL === 'function') {
      window.URL.revokeObjectURL(url);
    }
  },
};
