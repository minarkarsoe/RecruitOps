import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {
  KpiMetricsDto,
  TimeToHireAnalyticsDto,
  ConversionFunnelAnalyticsDto,
  SourceOfHireAnalyticsDto,
  ReportQueryResultDto,
} from '@recruitops/types';
import { AnalyticsPage } from '../../../pages/AnalyticsPage';
import { analyticsApi } from '../analyticsApi';

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

const mockKpis: KpiMetricsDto = {
  avgTimeToHireDays: 24.5,
  activeRequisitions: 12,
  totalApplications: 350,
  overallHireRate: 0.085,
};

const mockTimeToHire: TimeToHireAnalyticsDto = {
  stageDurations: [
    { stage: 'Applied', avgDays: 3.2 },
    { stage: 'Screening', avgDays: 5.0 },
    { stage: 'Interview', avgDays: 12.1 },
    { stage: 'Offer', avgDays: 4.2 },
  ],
  departmentBreakdown: [
    { departmentId: 'dept-1', departmentName: 'Engineering', avgDays: 28.0, hiredCount: 8 },
    { departmentId: 'dept-2', departmentName: 'Product', avgDays: 21.0, hiredCount: 4 },
  ],
  postingBreakdown: [
    { jobPostingId: 'jp-1', postingTitle: 'Senior Frontend Engineer', avgDays: 25.0, hiredCount: 3 },
  ],
};

const mockConversion: ConversionFunnelAnalyticsDto = {
  funnel: [
    { stage: 'Applied', count: 350, dropOffRate: 0.0 },
    { stage: 'Screening', count: 200, dropOffRate: 0.428 },
    { stage: 'Interview', count: 50, dropOffRate: 0.75 },
    { stage: 'Offer', count: 15, dropOffRate: 0.7 },
    { stage: 'Hired', count: 12, dropOffRate: 0.2 },
  ],
};

const mockSourceOfHire: SourceOfHireAnalyticsDto = {
  sources: [
    { source: 'Referral', count: 140, percentage: 40.0 },
    { source: 'LinkedIn', count: 105, percentage: 30.0 },
    { source: 'Direct', count: 70, percentage: 20.0 },
    { source: 'Facebook', count: 35, percentage: 10.0 },
  ],
};

const mockReportResult: ReportQueryResultDto = {
  headers: ['CandidateName', 'DepartmentName', 'CurrentStage', 'AppliedAt'],
  rows: [
    {
      CandidateName: 'Alice Smith',
      DepartmentName: 'Engineering',
      CurrentStage: 'Interview',
      AppliedAt: '2026-08-01',
    },
    {
      CandidateName: 'Bob Johnson',
      DepartmentName: 'Product',
      CurrentStage: 'Hired',
      AppliedAt: '2026-07-15',
    },
  ],
};

describe('AnalyticsPage Component', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    apiFetchMock.mockImplementation(async (path: string) => {
      if (path === '/departments') {
        return [
          { id: 'dept-1', name: 'Engineering', code: 'ENG', isActive: true },
          { id: 'dept-2', name: 'Product', code: 'PROD', isActive: true },
        ];
      }
      if (path === '/jobpostings') {
        return [
          { id: 'jp-1', title: 'Senior Frontend Engineer', departmentId: 'dept-1' },
        ];
      }
      return [];
    });

    vi.mocked(analyticsApi.getKpis).mockResolvedValue(mockKpis);
    vi.mocked(analyticsApi.getTimeToHire).mockResolvedValue(mockTimeToHire);
    vi.mocked(analyticsApi.getConversionFunnel).mockResolvedValue(mockConversion);
    vi.mocked(analyticsApi.getSourceOfHire).mockResolvedValue(mockSourceOfHire);
    vi.mocked(analyticsApi.queryReport).mockResolvedValue(mockReportResult);
    vi.mocked(analyticsApi.downloadReportCsv).mockResolvedValue(undefined);
  });

  it('1. renders Analytics page header and loading skeletons initial state', async () => {
    vi.mocked(analyticsApi.getKpis).mockReturnValue(new Promise(() => {}));
    vi.mocked(analyticsApi.getTimeToHire).mockReturnValue(new Promise(() => {}));
    vi.mocked(analyticsApi.getConversionFunnel).mockReturnValue(new Promise(() => {}));
    vi.mocked(analyticsApi.getSourceOfHire).mockReturnValue(new Promise(() => {}));

    render(<AnalyticsPage />);

    expect(screen.getByRole('heading', { name: /Reporting & Analytics/i })).toBeInTheDocument();
    expect(screen.getByTestId('kpi-skeleton-grid')).toBeInTheDocument();
    expect(screen.getByTestId('time-to-hire-skeleton')).toBeInTheDocument();
    expect(screen.getByTestId('funnel-chart-skeleton')).toBeInTheDocument();
    expect(screen.getByTestId('source-distribution-skeleton')).toBeInTheDocument();
  });

  it('2. renders KPI metrics summary cards correctly with backend data', async () => {
    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('kpi-cards-grid')).toBeInTheDocument();
    });

    expect(screen.getByText('Average Time-to-Hire')).toBeInTheDocument();
    expect(screen.getByText('24.5 days')).toBeInTheDocument();
    expect(screen.getByText('Active Requisitions')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Total Applications')).toBeInTheDocument();
    expect(screen.getByText('350')).toBeInTheDocument();
    expect(screen.getByText('Overall Hire Rate')).toBeInTheDocument();
    expect(screen.getByText('8.5%')).toBeInTheDocument();
  });

  it('3. renders Time-to-Hire, Conversion Funnel, and Source Distribution visual charts', async () => {
    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('time-to-hire-chart-card')).toBeInTheDocument();
    });

    // Time to Hire chart tabs & stages
    expect(screen.getByText('Pipeline Stages')).toBeInTheDocument();
    expect(screen.getByText('12.1 days')).toBeInTheDocument();

    // Funnel chart
    expect(screen.getByTestId('funnel-chart-card')).toBeInTheDocument();
    expect(screen.getByText('Pipeline Conversion Funnel')).toBeInTheDocument();
    expect(screen.getByText('350 candidates')).toBeInTheDocument();

    // Source distribution chart
    expect(screen.getByTestId('source-distribution-card')).toBeInTheDocument();
    expect(screen.getByText('Source of Hire Distribution')).toBeInTheDocument();
    expect(screen.getByText('Referral')).toBeInTheDocument();
    expect(screen.getByText('40.0%')).toBeInTheDocument();
  });

  it('4. executes custom report query with selected filters and updates preview table', async () => {
    const user = userEvent.setup();
    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('custom-report-builder-card')).toBeInTheDocument();
    });

    // Fill date from
    const dateFromInput = screen.getByTestId('date-from-input');
    await user.type(dateFromInput, '2026-08-01');

    // Wait for department options to be populated asynchronously
    await waitFor(() => {
      expect(screen.getByRole('option', { name: 'Engineering' })).toBeInTheDocument();
    });

    // Select department
    const deptSelect = screen.getByTestId('department-select');
    await user.selectOptions(deptSelect, 'dept-1');

    // Click Run Query
    const runQueryBtn = screen.getByTestId('run-query-btn');
    await user.click(runQueryBtn);

    await waitFor(() => {
      expect(analyticsApi.queryReport).toHaveBeenCalledTimes(1);
    });

    expect(analyticsApi.queryReport).toHaveBeenCalledWith(
      expect.objectContaining({
        departmentId: 'dept-1',
      })
    );

    // Verify rows rendered in table
    expect(screen.getByText('Alice Smith')).toBeInTheDocument();
    expect(screen.getByText('Bob Johnson')).toBeInTheDocument();
    expect(screen.getByTestId('report-row-0')).toBeInTheDocument();
  });

  it('5. handles Export to CSV button click and triggers CSV report download', async () => {
    const user = userEvent.setup();
    render(<AnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('custom-report-builder-card')).toBeInTheDocument();
    });

    // Click Export to CSV
    const exportCsvBtn = screen.getByTestId('export-csv-btn');
    await user.click(exportCsvBtn);

    await waitFor(() => {
      expect(analyticsApi.downloadReportCsv).toHaveBeenCalledTimes(1);
    });

    expect(analyticsApi.downloadReportCsv).toHaveBeenCalledWith(
      expect.objectContaining({
        columns: expect.arrayContaining(['CandidateName', 'JobPostingTitle']),
      })
    );
  });
});
