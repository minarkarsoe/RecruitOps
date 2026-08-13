import React, { useState } from 'react';
import type { ReportQueryRequestDto, ReportQueryResultDto } from '@recruitops/types';
import {
  Card,
  Button,
  Input,
  Select,
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
  SkeletonRow,
} from '@recruitops/ui';

interface DepartmentOption {
  id: string;
  name: string;
}

interface JobPostingOption {
  id: string;
  title: string;
}

interface CustomReportBuilderProps {
  onQueryReport: (query: ReportQueryRequestDto) => Promise<void>;
  onExportCsv: (query: ReportQueryRequestDto) => Promise<void>;
  reportResult: ReportQueryResultDto | null;
  reportLoading: boolean;
  exportLoading: boolean;
  reportError: string | null;
  departments?: DepartmentOption[];
  jobPostings?: JobPostingOption[];
}

const ALL_STAGES = [
  'Sourced',
  'Applied',
  'Screening',
  'Shortlisted',
  'Interview',
  'Offer',
  'Hired',
  'Rejected',
];

const ALL_COLUMNS = [
  'CandidateName',
  'Email',
  'JobPostingTitle',
  'DepartmentName',
  'CurrentStage',
  'Source',
  'AppliedAt',
  'HiredAt',
  'DaysInProcess',
];

export const CustomReportBuilder: React.FC<CustomReportBuilderProps> = ({
  onQueryReport,
  onExportCsv,
  reportResult,
  reportLoading,
  exportLoading,
  reportError,
  departments = [],
  jobPostings = [],
}) => {
  const [dateFrom, setDateFrom] = useState<string>('');
  const [dateTo, setDateTo] = useState<string>('');
  const [departmentId, setDepartmentId] = useState<string>('');
  const [jobPostingId, setJobPostingId] = useState<string>('');
  const [selectedStages, setSelectedStages] = useState<string[]>([]);
  const [selectedColumns, setSelectedColumns] = useState<string[]>([
    'CandidateName',
    'JobPostingTitle',
    'DepartmentName',
    'CurrentStage',
    'AppliedAt',
  ]);

  const buildQueryPayload = (): ReportQueryRequestDto => {
    return {
      dateFrom: dateFrom ? new Date(dateFrom).toISOString() : null,
      dateTo: dateTo ? new Date(dateTo).toISOString() : null,
      departmentId: departmentId || null,
      jobPostingId: jobPostingId || null,
      stages: selectedStages.length > 0 ? selectedStages : null,
      columns: selectedColumns.length > 0 ? selectedColumns : null,
    };
  };

  const handleRunQuery = () => {
    onQueryReport(buildQueryPayload());
  };

  const handleExportCsv = () => {
    onExportCsv(buildQueryPayload());
  };

  const toggleStage = (stage: string) => {
    setSelectedStages((prev) =>
      prev.includes(stage) ? prev.filter((s) => s !== stage) : [...prev, stage]
    );
  };

  const toggleColumn = (col: string) => {
    setSelectedColumns((prev) =>
      prev.includes(col) ? prev.filter((c) => c !== col) : [...prev, col]
    );
  };

  return (
    <div data-testid="custom-report-builder-card">
      <Card title="Custom Report Builder">
        <div className="mb-5 pb-3 border-b border-zinc-100 dark:border-zinc-800 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <p className="text-xs text-zinc-500 dark:text-zinc-400">
            Filter parameters, select visible report columns, and export custom CSV datasets
          </p>

          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="secondary"
              onClick={handleRunQuery}
              disabled={reportLoading}
              data-testid="run-query-btn"
            >
              {reportLoading ? 'Running...' : 'Run Query'}
            </Button>

            <Button
              type="button"
              variant="primary"
              onClick={handleExportCsv}
              disabled={exportLoading}
              data-testid="export-csv-btn"
            >
              {exportLoading ? 'Exporting...' : 'Export to CSV'}
            </Button>
          </div>
        </div>

        {/* Filter Parameters */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-5 bg-zinc-50 dark:bg-zinc-800/50 p-4 rounded-lg border border-zinc-100 dark:border-zinc-800">
          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Date From
            </label>
            <Input
              type="date"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
              className="w-full text-xs"
              data-testid="date-from-input"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Date To
            </label>
            <Input
              type="date"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
              className="w-full text-xs"
              data-testid="date-to-input"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Department
            </label>
            <Select
              value={departmentId}
              onChange={(e) => setDepartmentId(e.target.value)}
              options={[
                { value: '', label: 'All Departments' },
                ...departments.map((d) => ({ value: d.id, label: d.name })),
              ]}
              className="w-full text-xs"
              data-testid="department-select"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Job Posting
            </label>
            <Select
              value={jobPostingId}
              onChange={(e) => setJobPostingId(e.target.value)}
              options={[
                { value: '', label: 'All Postings' },
                ...jobPostings.map((jp) => ({ value: jp.id, label: jp.title })),
              ]}
              className="w-full text-xs"
              data-testid="job-posting-select"
            />
          </div>
        </div>

        {/* Stage Selection */}
        <div className="mb-4">
          <span className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1.5">
            Filter by Pipeline Stage (Optional):
          </span>
          <div className="flex flex-wrap gap-2">
            {ALL_STAGES.map((stage) => {
              const isSelected = selectedStages.includes(stage);
              return (
                <button
                  key={stage}
                  type="button"
                  onClick={() => toggleStage(stage)}
                  className={`text-xs px-2.5 py-1 rounded-full border transition-colors ${
                    isSelected
                      ? 'bg-teal-50 dark:bg-teal-950/60 border-teal-500 text-teal-700 dark:text-teal-300 font-semibold'
                      : 'bg-white dark:bg-zinc-800 border-zinc-200 dark:border-zinc-700 text-zinc-600 dark:text-zinc-400 hover:border-zinc-300'
                  }`}
                  data-testid={`stage-toggle-${stage}`}
                >
                  {stage}
                </button>
              );
            })}
          </div>
        </div>

        {/* Column Selection */}
        <div className="mb-6">
          <span className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1.5">
            Select Output Columns:
          </span>
          <div className="flex flex-wrap gap-2">
            {ALL_COLUMNS.map((col) => {
              const isSelected = selectedColumns.includes(col);
              return (
                <button
                  key={col}
                  type="button"
                  onClick={() => toggleColumn(col)}
                  className={`text-xs px-2.5 py-1 rounded-md border transition-colors ${
                    isSelected
                      ? 'bg-cyan-50 dark:bg-cyan-950/60 border-cyan-500 text-cyan-700 dark:text-cyan-300 font-semibold'
                      : 'bg-white dark:bg-zinc-800 border-zinc-200 dark:border-zinc-700 text-zinc-600 dark:text-zinc-400 hover:border-zinc-300'
                  }`}
                  data-testid={`column-toggle-${col}`}
                >
                  {col}
                </button>
              );
            })}
          </div>
        </div>

        {/* Error Alert */}
        {reportError && (
          <div className="mb-4 p-3 text-xs rounded-md bg-red-50 dark:bg-red-950/50 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800">
            {reportError}
          </div>
        )}

        {/* Tabular Preview */}
        <div className="border border-zinc-200 dark:border-zinc-800 rounded-lg overflow-hidden" data-testid="report-preview-container">
          <div className="bg-zinc-100 dark:bg-zinc-800/80 px-4 py-2 text-xs font-medium text-zinc-700 dark:text-zinc-300 border-b border-zinc-200 dark:border-zinc-800 flex justify-between items-center">
            <span>Report Data Preview</span>
            {reportResult && (
              <span className="text-[10px] text-zinc-500">
                {reportResult.rows.length} rows returned
              </span>
            )}
          </div>

          {reportLoading ? (
            <div className="p-4 space-y-2">
              <SkeletonRow />
              <SkeletonRow />
              <SkeletonRow />
            </div>
          ) : !reportResult || reportResult.rows.length === 0 ? (
            <div className="p-8 text-center text-xs text-zinc-500 dark:text-zinc-400" data-testid="no-report-data">
              No report data queried yet. Click &quot;Run Query&quot; to fetch live tabular data preview.
            </div>
          ) : (
            <div className="overflow-x-auto max-h-96">
              <Table>
                <TableHeader>
                  <TableRow>
                    {reportResult.headers.map((hdr, i) => (
                      <TableHead key={i} className="text-xs font-semibold">
                        {hdr}
                      </TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {reportResult.rows.map((row, rIdx) => (
                    <TableRow key={rIdx} data-testid={`report-row-${rIdx}`}>
                      {reportResult.headers.map((hdr, cIdx) => (
                        <TableCell key={cIdx} className="text-xs">
                          {row[hdr] !== undefined && row[hdr] !== null
                            ? String(row[hdr])
                            : '-'}
                        </TableCell>
                      ))}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </div>
      </Card>
    </div>
  );
};
