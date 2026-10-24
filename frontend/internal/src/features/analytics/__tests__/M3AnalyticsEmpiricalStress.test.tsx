import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReportQueryResultDto } from '@recruitops/types';
import { CustomReportBuilder } from '../CustomReportBuilder';

describe('Milestone 3 Analytics & Report Builder Empirical Stress Suite', () => {
  describe('1. CustomReportBuilder Parameter Toggles & CSV Export Payload', () => {
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
