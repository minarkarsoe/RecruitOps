import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReportQueryResultDto } from '@recruitops/types';
import { ReportsPage } from '../../../pages/ReportsPage';
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
    getExecutive: vi.fn(),
    getPipeline: vi.fn(),
    queryReport: vi.fn(),
    exportReportCsv: vi.fn(),
    downloadReportCsv: vi.fn(),
  },
}));

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

// The August page held this beside its charts; ADR-0031 §5 gave the builder its own screen.
describe('Reports screen', () => {
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

    vi.mocked(analyticsApi.queryReport).mockResolvedValue(mockReportResult);
    vi.mocked(analyticsApi.downloadReportCsv).mockResolvedValue(undefined);
  });

  it('1. executes custom report query with selected filters and updates preview table', async () => {
    const user = userEvent.setup();
    render(<ReportsPage />);

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

  it('2. handles Export to CSV button click and triggers CSV report download', async () => {
    const user = userEvent.setup();
    render(<ReportsPage />);

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

  it('3. asks for no analytics until a query is run', async () => {
    render(<ReportsPage />);
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Custom report' })).toBeInTheDocument());
    expect(analyticsApi.getPipeline).not.toHaveBeenCalled();
    expect(analyticsApi.getExecutive).not.toHaveBeenCalled();
    expect(analyticsApi.queryReport).not.toHaveBeenCalled();
  });
});
