import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AnalyticsPage } from '../../../pages/AnalyticsPage';
import { analyticsApi } from '../analyticsApi';
import { KpiCardSection } from '../KpiCardSection';
import { TimeToHireChart } from '../TimeToHireChart';
import { FunnelChart } from '../FunnelChart';
import { SourceDistributionChart } from '../SourceDistributionChart';
import { CustomReportBuilder } from '../CustomReportBuilder';
import type { ReportQueryResultDto } from '@recruitops/types';

const { apiFetchMock } = vi.hoisted(() => ({ apiFetchMock: vi.fn() }));

vi.mock('../../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api')>('../../../lib/api');
  return {
    ...actual,
    apiFetch: apiFetchMock,
    api: apiFetchMock,
  };
});

vi.mock('../analyticsApi', () => ({
  analyticsApi: {
    getKpis: vi.fn(),
    getTimeToHire: vi.fn(),
    getConversionFunnel: vi.fn(),
    getSourceOfHire: vi.fn(),
    queryReport: vi.fn(),
    exportReportCsv: vi.fn(),
    downloadReportCsv: vi.fn(),
  },
}));

describe('Analytics Empirical Edge-Case Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    apiFetchMock.mockImplementation(async (path: string) => {
      if (path === '/departments') return [];
      if (path === '/jobpostings') return [];
      return [];
    });

    vi.mocked(analyticsApi.getKpis).mockResolvedValue({
      avgTimeToHireDays: 0,
      activeRequisitions: 0,
      totalApplications: 0,
      overallHireRate: 0,
    });
    vi.mocked(analyticsApi.getTimeToHire).mockResolvedValue({
      stageDurations: [],
      departmentBreakdown: [],
      postingBreakdown: [],
    });
    vi.mocked(analyticsApi.getConversionFunnel).mockResolvedValue({
      funnel: [],
    });
    vi.mocked(analyticsApi.getSourceOfHire).mockResolvedValue({
      sources: [],
    });
    vi.mocked(analyticsApi.queryReport).mockResolvedValue({
      headers: ['CandidateName', 'Status'],
      rows: [],
    });
    vi.mocked(analyticsApi.downloadReportCsv).mockResolvedValue(undefined);
  });

  it('1. Loading Skeletons: renders skeletons when loading flag is true and null data', () => {
    render(<KpiCardSection kpis={null} loading={true} />);
    expect(screen.getByTestId('kpi-skeleton-grid')).toBeInTheDocument();

    render(<TimeToHireChart data={null} loading={true} />);
    expect(screen.getByTestId('time-to-hire-skeleton')).toBeInTheDocument();

    render(<FunnelChart data={null} loading={true} />);
    expect(screen.getByTestId('funnel-chart-skeleton')).toBeInTheDocument();

    render(<SourceDistributionChart data={null} loading={true} />);
    expect(screen.getByTestId('source-distribution-skeleton')).toBeInTheDocument();
  });

  it('2. Empty Responses: handles empty arrays for stageDurations, funnel, sources gracefully without crashing', async () => {
    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('kpi-cards-grid')).toBeInTheDocument();
    });

    expect(screen.getByText('0.0 days')).toBeInTheDocument();
    expect(screen.getByText('No stage duration data available.')).toBeInTheDocument();
    expect(screen.getByText('No funnel conversion data available.')).toBeInTheDocument();
    expect(screen.getByText('No source distribution data available.')).toBeInTheDocument();
  });

  it('3. Empty Report Response: CustomReportBuilder shows empty message when reportResult has 0 rows', () => {
    const emptyResult: ReportQueryResultDto = {
      headers: ['CandidateName', 'Status'],
      rows: [],
    };

    render(
      <CustomReportBuilder
        onQueryReport={vi.fn()}
        onExportCsv={vi.fn()}
        reportResult={emptyResult}
        reportLoading={false}
        exportLoading={false}
        reportError={null}
      />
    );

    expect(screen.getByTestId('no-report-data')).toBeInTheDocument();
    expect(screen.getByText(/No report data queried yet/i)).toBeInTheDocument();
  });

  it('4. CSV Blob Generation & Download: CustomReportBuilder triggers exportCsv handler with correct filter payload', async () => {
    const user = userEvent.setup();
    const handleExport = vi.fn();

    render(
      <CustomReportBuilder
        onQueryReport={vi.fn()}
        onExportCsv={handleExport}
        reportResult={null}
        reportLoading={false}
        exportLoading={false}
        reportError={null}
      />
    );

    const exportBtn = screen.getByTestId('export-csv-btn');
    await user.click(exportBtn);

    expect(handleExport).toHaveBeenCalledTimes(1);
    expect(handleExport).toHaveBeenCalledWith(
      expect.objectContaining({
        columns: expect.arrayContaining(['CandidateName', 'JobPostingTitle', 'DepartmentName']),
      })
    );
  });

  it('5. Dashboard Refresh & Error State: renders global error banner when dashboard fetch fails', async () => {
    vi.mocked(analyticsApi.getKpis).mockRejectedValue(new Error('Network error fetching analytics KPIs'));

    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('Network error fetching analytics KPIs')).toBeInTheDocument();
    });
  });
});
