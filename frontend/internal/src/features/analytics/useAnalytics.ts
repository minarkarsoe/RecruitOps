import { useCallback, useEffect, useState } from 'react';
import type {
  KpiMetricsDto,
  TimeToHireAnalyticsDto,
  ConversionFunnelAnalyticsDto,
  SourceOfHireAnalyticsDto,
  ReportQueryRequestDto,
  ReportQueryResultDto,
} from '@recruitops/types';
import { analyticsApi } from './analyticsApi';

export function useAnalytics() {
  const [kpis, setKpis] = useState<KpiMetricsDto | null>(null);
  const [timeToHire, setTimeToHire] = useState<TimeToHireAnalyticsDto | null>(null);
  const [conversion, setConversion] = useState<ConversionFunnelAnalyticsDto | null>(null);
  const [sourceOfHire, setSourceOfHire] = useState<SourceOfHireAnalyticsDto | null>(null);

  const [reportResult, setReportResult] = useState<ReportQueryResultDto | null>(null);

  const [loading, setLoading] = useState<boolean>(true);
  const [reportLoading, setReportLoading] = useState<boolean>(false);
  const [exportLoading, setExportLoading] = useState<boolean>(false);

  const [error, setError] = useState<string | null>(null);
  const [reportError, setReportError] = useState<string | null>(null);

  const fetchDashboard = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [kpiRes, timeRes, convRes, srcRes] = await Promise.all([
        analyticsApi.getKpis(),
        analyticsApi.getTimeToHire(),
        analyticsApi.getConversionFunnel(),
        analyticsApi.getSourceOfHire(),
      ]);
      setKpis(kpiRes);
      setTimeToHire(timeRes);
      setConversion(convRes);
      setSourceOfHire(srcRes);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load analytics dashboard data.');
    } finally {
      setLoading(false);
    }
  }, []);

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

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  return {
    kpis,
    timeToHire,
    conversion,
    sourceOfHire,
    reportResult,
    loading,
    reportLoading,
    exportLoading,
    error,
    reportError,
    refreshDashboard: fetchDashboard,
    runReportQuery,
    downloadReportCsv,
  };
}
