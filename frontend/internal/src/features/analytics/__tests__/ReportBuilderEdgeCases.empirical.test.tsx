import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { analyticsApi } from '../analyticsApi';
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
    getExecutive: vi.fn(),
    getPipeline: vi.fn(),
    queryReport: vi.fn(),
    exportReportCsv: vi.fn(),
    downloadReportCsv: vi.fn(),
  },
}));

describe('Report builder edge cases', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    apiFetchMock.mockImplementation(async (path: string) => {
      if (path === '/departments') return [];
      if (path === '/jobpostings') return [];
      return [];
    });

    vi.mocked(analyticsApi.queryReport).mockResolvedValue({
      headers: ['CandidateName', 'Status'],
      rows: [],
    });
    vi.mocked(analyticsApi.downloadReportCsv).mockResolvedValue(undefined);
  });

  it('1. Empty Report Response: CustomReportBuilder shows empty message when reportResult has 0 rows', () => {
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

  it('2. CSV Blob Generation & Download: CustomReportBuilder triggers exportCsv handler with correct filter payload', async () => {
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
});
