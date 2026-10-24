import { useCallback, useEffect, useState } from 'react';
import type {
  TimeToHireAnalyticsDto,
  ConversionFunnelAnalyticsDto,
  SourceOfHireAnalyticsDto,
  ReportQueryRequestDto,
  ReportQueryResultDto,
} from '@recruitops/types';
import { analyticsApi } from './analyticsApi';

/** The Pipeline & source screen's three charts, loaded together. */
export function usePipelineAnalytics() {
  const [timeToHire, setTimeToHire] = useState<TimeToHireAnalyticsDto | null>(null);
  const [conversion, setConversion] = useState<ConversionFunnelAnalyticsDto | null>(null);
  const [sourceOfHire, setSourceOfHire] = useState<SourceOfHireAnalyticsDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchDashboard = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [timeRes, convRes, srcRes] = await Promise.all([
        analyticsApi.getTimeToHire(),
        analyticsApi.getConversionFunnel(),
        analyticsApi.getSourceOfHire(),
      ]);
      setTimeToHire(timeRes);
      setConversion(convRes);
      setSourceOfHire(srcRes);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load analytics dashboard data.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  return { timeToHire, conversion, sourceOfHire, loading, error, refreshDashboard: fetchDashboard };
}

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
