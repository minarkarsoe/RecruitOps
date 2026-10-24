import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {
  TimeToHireAnalyticsDto,
  ConversionFunnelAnalyticsDto,
  SourceOfHireAnalyticsDto,
  ReportQueryResultDto,
} from '@recruitops/types';
import { TimeToHireChart } from '../TimeToHireChart';
import { FunnelChart } from '../FunnelChart';
import { SourceDistributionChart } from '../SourceDistributionChart';
import { CustomReportBuilder } from '../CustomReportBuilder';

describe('Milestone 3 Analytics & Report Builder Empirical Stress Suite', () => {
  describe('1. TimeToHireChart Tab Switches & Empty Data Resilience', () => {
    it('renders empty data messages cleanly when arrays are empty', () => {
      const emptyData: TimeToHireAnalyticsDto = {
        stageDurations: [],
        departmentBreakdown: [],
        postingBreakdown: [],
      };

      render(<TimeToHireChart data={emptyData} loading={false} />);

      expect(screen.getByText('No stage duration data available.')).toBeInTheDocument();
    });

    it('switches tabs to By Department and By Job Posting smoothly', async () => {
      const user = userEvent.setup();
      const mockData: TimeToHireAnalyticsDto = {
        stageDurations: [{ stage: 'Screening', avgDays: 4.5 }],
        departmentBreakdown: [{ departmentId: 'd1', departmentName: 'Marketing', avgDays: 15.0, hiredCount: 2 }],
        postingBreakdown: [{ jobPostingId: 'jp1', postingTitle: 'Marketing Lead', avgDays: 18.0, hiredCount: 1 }],
      };

      render(<TimeToHireChart data={mockData} loading={false} />);

      // Switch to By Department
      const deptTab = screen.getByRole('button', { name: /By Department/i });
      await user.click(deptTab);
      expect(screen.getByTestId('tth-departments-view')).toBeInTheDocument();
      expect(screen.getByText('Marketing')).toBeInTheDocument();
      expect(screen.getByText('2 hired')).toBeInTheDocument();

      // Switch to By Job Posting
      const postingTab = screen.getByRole('button', { name: /By Job Posting/i });
      await user.click(postingTab);
      expect(screen.getByTestId('tth-postings-view')).toBeInTheDocument();
      expect(screen.getByText('Marketing Lead')).toBeInTheDocument();
      expect(screen.getByText('1 hired')).toBeInTheDocument();
    });
  });

  describe('2. FunnelChart & SourceDistributionChart Formatting & Custom Channels', () => {
    it('renders funnel entry point vs drop-off badges correctly', () => {
      const mockFunnel: ConversionFunnelAnalyticsDto = {
        funnel: [
          { stage: 'Applied', count: 100, dropOffRate: 0 },
          { stage: 'Interview', count: 20, dropOffRate: 0.8 },
        ],
      };

      render(<FunnelChart data={mockFunnel} loading={false} />);

      expect(screen.getByText('Entry Point')).toBeInTheDocument();
      expect(screen.getByText('-80.0% drop-off')).toBeInTheDocument();
    });

    it('renders fallback styles for custom unknown sourcing channels', () => {
      const mockSources: SourceOfHireAnalyticsDto = {
        sources: [
          { source: 'TikTok', count: 50, percentage: 0.25 },
        ],
      };

      render(<SourceDistributionChart data={mockSources} loading={false} />);

      expect(screen.getByText('TikTok')).toBeInTheDocument();
      expect(screen.getByText('25.0%')).toBeInTheDocument();
    });
  });

  describe('3. CustomReportBuilder Parameter Toggles & CSV Export Payload', () => {
    it('allows toggling stage filters and column selections before running query', async () => {
      const user = userEvent.setup();
      const onQueryMock = vi.fn();
      const onExportMock = vi.fn();

      render(
        <CustomReportBuilder
          onQueryReport={onQueryMock}
          onExportCsv={onExportMock}
          reportResult={null}
          reportLoading={false}
          exportLoading={false}
          reportError={null}
        />
      );

      // Toggle stage 'Interview'
      const stageBtn = screen.getByTestId('stage-toggle-Interview');
      await user.click(stageBtn);

      // Toggle column 'Email'
      const colBtn = screen.getByTestId('column-toggle-Email');
      await user.click(colBtn);

      // Click Run Query
      await user.click(screen.getByTestId('run-query-btn'));

      expect(onQueryMock).toHaveBeenCalledWith(
        expect.objectContaining({
          stages: ['Interview'],
          columns: expect.arrayContaining(['Email']),
        })
      );
    });

    it('renders report result rows accurately with null handling fallback', () => {
      const mockResult: ReportQueryResultDto = {
        headers: ['CandidateName', 'Email', 'DaysInProcess'],
        rows: [
          { CandidateName: 'Jane Doe', Email: null, DaysInProcess: 10 },
        ],
      };

      render(
        <CustomReportBuilder
          onQueryReport={vi.fn()}
          onExportCsv={vi.fn()}
          reportResult={mockResult}
          reportLoading={false}
          exportLoading={false}
          reportError={null}
        />
      );

      expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      expect(screen.getByText('-')).toBeInTheDocument(); // null Email fallback
      expect(screen.getByText('10')).toBeInTheDocument();
    });
  });
});
