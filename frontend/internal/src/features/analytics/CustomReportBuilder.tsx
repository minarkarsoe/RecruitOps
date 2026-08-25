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

// One toggle-chip treatment for both groups. They had two — teal for stages, cyan for columns —
// which under the V1.0 preset alias to the identical hex, so the code claimed a distinction the
// screen could not show. What separates the two groups is the label above them.
const chipClass = (isSelected: boolean) =>
  `h-7 rounded-full border px-2.5 text-sm transition-colors ${
    isSelected
      ? 'border-brand-700 bg-brand-50 font-medium text-brand-800'
      : 'border-line bg-white text-ink-600 hover:border-line-strong'
  }`;

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
        <div className="mb-5 flex flex-col justify-between gap-3 border-b border-line pb-3 sm:flex-row sm:items-center">
          <p className="text-sm text-ink-600">
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
        <div className="mb-5 grid grid-cols-1 gap-4 rounded-lg border border-line bg-canvas p-4 md:grid-cols-4">
          <div>
            <label className="mb-1 block text-sm text-ink-600">Date From</label>
            <Input
              type="date"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
              className="w-full"
              data-testid="date-from-input"
            />
          </div>

          <div>
            <label className="mb-1 block text-sm text-ink-600">Date To</label>
            <Input
              type="date"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
              className="w-full"
              data-testid="date-to-input"
            />
          </div>

          <div>
            <label className="mb-1 block text-sm text-ink-600">Department</label>
            <Select
              value={departmentId}
              onChange={(e) => setDepartmentId(e.target.value)}
              options={[
                { value: '', label: 'All Departments' },
                ...departments.map((d) => ({ value: d.id, label: d.name })),
              ]}
              className="w-full"
              data-testid="department-select"
            />
          </div>

          <div>
            <label className="mb-1 block text-sm text-ink-600">Job Posting</label>
            <Select
              value={jobPostingId}
              onChange={(e) => setJobPostingId(e.target.value)}
              options={[
                { value: '', label: 'All Postings' },
                ...jobPostings.map((jp) => ({ value: jp.id, label: jp.title })),
              ]}
              className="w-full"
              data-testid="job-posting-select"
            />
          </div>
        </div>

        {/* Stage Selection */}
        <div className="mb-4">
          <span className="mb-1.5 block text-sm text-ink-600">
            Filter by Pipeline Stage (Optional):
          </span>
          <div className="flex flex-wrap gap-2">
            {ALL_STAGES.map((stage) => {
              const isSelected = selectedStages.includes(stage);
              return (
                <button
                  key={stage}
                  type="button"
                  aria-pressed={isSelected}
                  onClick={() => toggleStage(stage)}
                  className={chipClass(isSelected)}
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
          <span className="mb-1.5 block text-sm text-ink-600">Select Output Columns:</span>
          <div className="flex flex-wrap gap-2">
            {ALL_COLUMNS.map((col) => {
              const isSelected = selectedColumns.includes(col);
              return (
                <button
                  key={col}
                  type="button"
                  aria-pressed={isSelected}
                  onClick={() => toggleColumn(col)}
                  className={chipClass(isSelected)}
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
          <div className="mb-4 rounded-md border border-critical-100 bg-critical-50 p-3 text-sm text-critical-700">
            {reportError}
          </div>
        )}

        {/* Tabular Preview */}
        <div className="overflow-hidden rounded-lg border border-line" data-testid="report-preview-container">
          <div className="flex items-center justify-between border-b border-line bg-canvas px-4 py-2 text-sm text-ink-600">
            <span>Report Data Preview</span>
            {reportResult && (
              <span className="tnum">{reportResult.rows.length} rows returned</span>
            )}
          </div>

          {reportLoading ? (
            <div className="space-y-2 p-4">
              <SkeletonRow />
              <SkeletonRow />
              <SkeletonRow />
            </div>
          ) : !reportResult || reportResult.rows.length === 0 ? (
            <p className="p-8 text-center text-sm text-ink-600" data-testid="no-report-data">
              No report data queried yet. Click &quot;Run Query&quot; to fetch live tabular data preview.
            </p>
          ) : (
            <div className="max-h-96 overflow-x-auto">
              {/* No className overrides on the table parts — Table already carries the kit's
                  treatment, and overriding its type size here is how four hand-rolled tables
                  drifted apart before they were unified. */}
              <Table>
                <TableHeader>
                  <TableRow>
                    {reportResult.headers.map((hdr, i) => (
                      <TableHead key={i}>{hdr}</TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {reportResult.rows.map((row, rIdx) => (
                    <TableRow key={rIdx} data-testid={`report-row-${rIdx}`}>
                      {reportResult.headers.map((hdr, cIdx) => (
                        <TableCell key={cIdx}>
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
