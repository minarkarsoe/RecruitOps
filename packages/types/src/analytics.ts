// Shared API types for Analytics & Reporting (Milestone 3)
// Mirrors RecruitOps.Application.DTOs for Analytics

/** `GET /api/analytics/kpis` — no longer on any screen (ADR-0031 §5). ⚠️ `avgTimeToHireDays` is days
 *  to the **Hired stage**, not Time-to-Hire; that is `ExecutiveDashboardDto.timeToHire`. */
export interface KpiMetricsDto {
  avgTimeToHireDays: number;
  activeRequisitions: number;
  totalApplications: number;
  overallHireRate: number;
}

export interface StageDurationDto {
  stage: string;
  avgDays: number;
}

export interface DepartmentTimeDto {
  departmentId: string;
  departmentName: string;
  avgDays: number;
  hiredCount: number;
}

export interface PostingTimeDto {
  jobPostingId: string;
  postingTitle: string;
  avgDays: number;
  hiredCount: number;
}

export interface TimeToHireAnalyticsDto {
  stageDurations: StageDurationDto[];
  departmentBreakdown: DepartmentTimeDto[];
  postingBreakdown: PostingTimeDto[];
}

export interface StageFunnelItemDto {
  stage: string;
  count: number;
  dropOffRate: number;
}

export interface ConversionFunnelAnalyticsDto {
  funnel: StageFunnelItemDto[];
}

export interface SourceDistributionItemDto {
  source: string;
  count: number;
  percentage: number;
}

export interface SourceOfHireAnalyticsDto {
  sources: SourceDistributionItemDto[];
}

export interface ReportQueryRequestDto {
  dateFrom?: string | null;
  dateTo?: string | null;
  departmentId?: string | null;
  jobPostingId?: string | null;
  stages?: string[] | null;
  columns?: string[] | null;
}

export interface ReportQueryResultDto {
  headers: string[];
  rows: Record<string, any>[];
}

// ── Executive dashboard (Module 5.1, ADR-0031) — GET /api/analytics/executive ──

export type ReportingPeriodKind = 'Month' | 'Quarter' | 'Year';

/** `from`/`to` are calendar dates (`YYYY-MM-DD`) on the company's calendar; `to` is today for the
 *  current period. */
export interface ReportingPeriodDto {
  kind: ReportingPeriodKind;
  from: string;
  to: string;
}

/** An average over accepted offers, in days. `null` when none fell in the period — never 0. */
export interface DurationMetricDto {
  days: number | null;
  count: number;
  previousDays: number | null;
  previousCount: number;
}

/** Accepted ÷ (accepted + declined + expired), by when each outcome fell. `rate` is a percentage. */
export interface AcceptanceMetricDto {
  accepted: number;
  decided: number;
  rate: number | null;
  previousAccepted: number;
  previousDecided: number;
  previousRate: number | null;
}

export interface DepartmentVacanciesDto {
  departmentId: string;
  departmentName: string;
  open: number;
}

export interface FilterOptionDto {
  id: string;
  name: string;
}

/** `Company` (Admin, HR Director), `Recruiter` (the offers they made), `Department` (a Hiring
 *  Manager's departments). */
export type ExecutiveDashboardScope = 'Company' | 'Recruiter' | 'Department';

export interface ExecutiveDashboardDto {
  scope: ExecutiveDashboardScope;
  period: ReportingPeriodDto;
  previous: ReportingPeriodDto;
  /** `null` for a Hiring Manager — omitted by the API, not hidden by the screen. */
  timeToHire: DurationMetricDto | null;
  timeToFill: DurationMetricDto;
  /** `null` for a Hiring Manager. */
  acceptance: AcceptanceMetricDto | null;
  vacancies: DepartmentVacanciesDto[];
  openVacancies: number;
  departments: FilterOptionDto[];
  /** The recruiter filter's choices: empty unless the scope is `Company`. */
  recruiters: FilterOptionDto[];
}
