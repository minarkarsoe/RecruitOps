// Shared API types for Analytics & Reporting (Milestone 3)
// Mirrors RecruitOps.Application.DTOs for Analytics

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
