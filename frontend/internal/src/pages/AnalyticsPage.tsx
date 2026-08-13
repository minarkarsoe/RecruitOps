import React, { useEffect, useState } from 'react';
import type { DepartmentListItem, JobPostingListItem } from '@recruitops/types';
import { apiFetch } from '../lib/api';
import { Button } from '@recruitops/ui';
import {
  useAnalytics,
  KpiCardSection,
  TimeToHireChart,
  FunnelChart,
  SourceDistributionChart,
  CustomReportBuilder,
} from '../features/analytics';

export const AnalyticsPage: React.FC = () => {
  const {
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
    refreshDashboard,
    runReportQuery,
    downloadReportCsv,
  } = useAnalytics();

  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [jobPostings, setJobPostings] = useState<JobPostingListItem[]>([]);

  useEffect(() => {
    async function loadMetadata() {
      try {
        const [depts, postings] = await Promise.all([
          apiFetch<DepartmentListItem[]>('/departments').catch(() => []),
          apiFetch<JobPostingListItem[]>('/jobpostings').catch(() => []),
        ]);
        setDepartments(depts);
        setJobPostings(postings);
      } catch {
        // Non-blocking fallback
      }
    }
    loadMetadata();
  }, []);

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6" data-testid="analytics-page-container">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-zinc-200 dark:border-zinc-800 pb-5">
        <div>
          <h1 className="text-2xl font-bold text-zinc-900 dark:text-zinc-100 tracking-tight">
            Reporting &amp; Analytics
          </h1>
          <p className="text-sm text-zinc-500 dark:text-zinc-400 mt-1">
            Real-time recruitment metrics, time-to-hire benchmarks, conversion funnels, and custom CSV reporting.
          </p>
        </div>

        <div className="flex items-center gap-3">
          <Button
            type="button"
            variant="secondary"
            onClick={refreshDashboard}
            disabled={loading}
            data-testid="refresh-dashboard-btn"
          >
            {loading ? 'Refreshing...' : 'Refresh Data'}
          </Button>
        </div>
      </div>

      {/* Global Error Banner */}
      {error && (
        <div className="p-4 rounded-lg bg-red-50 dark:bg-red-950/50 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 text-sm">
          {error}
        </div>
      )}

      {/* KPI Cards Section */}
      <KpiCardSection kpis={kpis} loading={loading} />

      {/* Visual Analytics Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <TimeToHireChart data={timeToHire} loading={loading} />
        <FunnelChart data={conversion} loading={loading} />
      </div>

      {/* Source Distribution Chart */}
      <SourceDistributionChart data={sourceOfHire} loading={loading} />

      {/* Custom Report Builder UI */}
      <CustomReportBuilder
        onQueryReport={runReportQuery}
        onExportCsv={downloadReportCsv}
        reportResult={reportResult}
        reportLoading={reportLoading}
        exportLoading={exportLoading}
        reportError={reportError}
        departments={departments.map((d) => ({ id: d.id, name: d.name }))}
        jobPostings={jobPostings.map((j) => ({ id: j.id, title: j.title }))}
      />
    </div>
  );
};

export default AnalyticsPage;
