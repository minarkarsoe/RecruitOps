import { useEffect, useState } from 'react';
import type { DepartmentListItem, JobPostingListItem } from '@recruitops/types';
import { apiFetch } from '../lib/api';
import { useReportBuilder, CustomReportBuilder } from '../features/analytics';

/**
 * Module 5.3 — Reports (`/analytics/reports`), the third analytics screen
 * (`design/internal/report-builder.html`). The August report builder, moved here unchanged from the
 * old combined page by ADR-0031 §5; scheduled reports are not built.
 */
export function ReportsPage() {
  const { reportResult, reportLoading, exportLoading, reportError, runReportQuery, downloadReportCsv } = useReportBuilder();

  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [jobPostings, setJobPostings] = useState<JobPostingListItem[]>([]);

  useEffect(() => {
    // The filter choices are a convenience: the builder still runs without them.
    Promise.all([
      apiFetch<DepartmentListItem[]>('/departments').catch(() => []),
      apiFetch<JobPostingListItem[]>('/jobpostings').catch(() => []),
    ]).then(([depts, postings]) => {
      setDepartments(depts);
      setJobPostings(postings);
    });
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Custom report</h1>
        <p className="mt-1 text-sm text-ink-600">
          Choose the candidates and the columns, preview them, and export a CSV.
        </p>
      </div>

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
}

export default ReportsPage;
