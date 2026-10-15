import { auth } from '../../lib/auth';
import { apiFetch, ApiError, tenantHeader } from '../../lib/api';
import type {
  KpiMetricsDto,
  TimeToHireAnalyticsDto,
  ConversionFunnelAnalyticsDto,
  SourceOfHireAnalyticsDto,
  ReportQueryRequestDto,
  ReportQueryResultDto,
} from '@recruitops/types';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

export const analyticsApi = {
  getKpis: () => apiFetch<KpiMetricsDto>('/analytics/kpis'),
  getTimeToHire: () => apiFetch<TimeToHireAnalyticsDto>('/analytics/time-to-hire'),
  getConversionFunnel: () => apiFetch<ConversionFunnelAnalyticsDto>('/analytics/conversion'),
  getSourceOfHire: () => apiFetch<SourceOfHireAnalyticsDto>('/analytics/source-of-hire'),
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
