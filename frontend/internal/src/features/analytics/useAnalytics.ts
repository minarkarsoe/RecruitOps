import { useCallback, useState } from 'react';
import type { ReportQueryRequestDto, ReportQueryResultDto } from '@recruitops/types';
import { analyticsApi } from './analyticsApi';

/** The Reports screen: run a query, export it. Nothing loads until asked. */
export function useReportBuilder() {
  const [reportResult, setReportResult] = useState<ReportQueryResultDto | null>(null);
  const [reportLoading, setReportLoading] = useState<boolean>(false);
  const [exportLoading, setExportLoading] = useState<boolean>(false);
  const [reportError, setReportError] = useState<string | null>(null);

  const runReportQuery = useCallback(async (request: ReportQueryRequestDto) => {
    setReportLoading(true);
    setReportError(null);
    try {
      const res = await analyticsApi.queryReport(request);
      setReportResult(res);
    } catch (err) {
      setReportError(err instanceof Error ? err.message : 'Failed to execute custom report query.');
    } finally {
      setReportLoading(false);
    }
  }, []);

  const downloadReportCsv = useCallback(async (request: ReportQueryRequestDto) => {
    setExportLoading(true);
    try {
      await analyticsApi.downloadReportCsv(request);
    } catch (err) {
      setReportError(err instanceof Error ? err.message : 'Failed to export CSV report.');
    } finally {
      setExportLoading(false);
    }
  }, []);

  return { reportResult, reportLoading, exportLoading, reportError, runReportQuery, downloadReportCsv };
}
