// Shared API types — mirror the backend DTOs and Domain enums.
// A backend contract change should break BOTH frontends at compile time, which is
// the point of keeping these here rather than duplicating per app (ADR-0012).

// ---------- Enums (mirror RecruitOps.Domain.Enums) ----------
export type UserRole =
  | 'Admin' | 'HrDirector' | 'Recruiter' | 'HiringManager' | 'Approver' | 'SuperAdmin' | string;

export type PipelineStatus =
  | 'Sourced' | 'Applied' | 'Screening' | 'Shortlisted'
  | 'Interview' | 'Offer' | 'Hired' | 'Rejected';

export type RequisitionStatus =
  | 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';

export type ApprovalDecision = 'Waiting' | 'Approved' | 'Rejected';

export type JobStatus = 'Draft' | 'Live' | 'Closed';

export type EmploymentType =
  | 'FullTime' | 'PartTime' | 'Contract' | 'Internship' | 'Temporary';

export type SourceChannel =
  | 'Direct' | 'Facebook' | 'LinkedIn' | 'Telegram' | 'Referral' | 'ExcelImport';

// ---------- Auth ----------
export interface LoginRequest { email: string; password: string; }

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: UserRole;
  displayName: string;
  userId: string;
  isSuperAdmin?: boolean;
  tenantId?: string;
  activeTenantId?: string;
  activeTenantName?: string;
  /**
   * The user's resolved permission codes. Required, not optional: `hasPermission()` must be
   * able to distinguish "no permissions" (empty array) from "not sent" — when this was
   * optional, the missing case was read as "unknown, allow" and every user saw the full UI.
   */
  permissions: string[];
}

// ---------- RBAC & User Management (Milestone 4) ----------
export interface Permission {
  id: string;
  code: string;
  name: string;
  description: string;
  module: string;
  feature: string;
  action: string;
}

export interface PermissionFeature {
  feature: string;
  permissions: Permission[];
}

export interface PermissionModule {
  module: string;
  features: PermissionFeature[];
}

export interface RoleListItem {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystemRole: boolean;
  isSuperAdmin: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
}

export interface RoleDetail {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystemRole: boolean;
  isSuperAdmin: boolean;
  isActive: boolean;
  assignedPermissions: Permission[];
  assignedPermissionCodes: string[];
  userCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateRoleRequest {
  name: string;
  code?: string | null;
  description?: string | null;
  permissionCodes: string[];
}

export interface UpdateRoleRequest {
  name: string;
  description?: string | null;
  isActive: boolean;
  permissionCodes: string[];
}

export interface UserRoleInfo {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystemRole: boolean;
  isSuperAdmin: boolean;
}

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  role: string;
  roleId?: string | null;
  roleName?: string | null;
  isActive?: boolean;
  createdAt?: string;
}

export interface UserDetail {
  id: string;
  email: string;
  displayName: string;
  role: string;
  roleId: string | null;
  roleDetails: UserRoleInfo | null;
  permissions: string[];
  isActive: boolean;
  isSuperAdmin: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface UserQueryParameters {
  page?: number;
  pageSize?: number;
  search?: string;
  roleId?: string;
  isActive?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  password: string;
  roleId?: string | null;
  role?: string | null;
}

export interface UpdateUserRequest {
  displayName: string;
  roleId?: string | null;
  role?: string | null;
}

export interface TenantInfo {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}


// ---------- Module 1: Requisition & Approval ----------
export interface DepartmentListItem {
  id: string;
  name: string;
  code: string | null;
  isActive: boolean;
}

/** Admin view — the counts exist so nobody deactivates a department blind. */
export interface DepartmentDetail extends DepartmentListItem {
  memberCount: number;
  openRequisitionCount: number;
}

export interface DepartmentMember {
  userId: string;
  displayName: string;
  email: string;
  role: UserRole;
  isMember: boolean;
}

export interface CreateDepartmentRequest {
  name: string;
  code?: string | null;
}

export interface UpdateDepartmentRequest {
  name: string;
  code?: string | null;
}

/** Replaces the whole list — membership is the ADR-0003 access-control axis, so it is
 *  committed as a complete state rather than as deltas. */
export interface SetDepartmentMembersRequest {
  userIds: string[];
}

/** Summary row — used in lists and the approver inbox. */
export interface RequisitionListItem {
  id: string;
  departmentId: string;
  departmentName: string;
  title: string;
  headcount: number;
  salaryBudget: number | null;
  status: RequisitionStatus;
  submittedAt: string | null;
  /** Label of the approval step currently waiting, if any. */
  awaitingApprovalFrom: string | null;
}

/** One step in the snapshotted approval chain. */
export interface ApprovalStep {
  sequence: number;
  label: string;
  approverUserId: string;
  decision: ApprovalDecision;
  decidedAt: string | null;
  comment: string | null;
}

/** Full detail — returned by GET /api/requisitions/:id. */
export interface RequisitionDetail extends RequisitionListItem {
  jobDescription: string;
  decidedAt: string | null;
  /** Who raised it. A display hint for the Cancel action — the backend re-checks it. */
  requestedByUserId: string;
  approvals: ApprovalStep[];
}

export interface CreateRequisitionRequest {
  departmentId: string;
  title: string;
  jobDescription: string;
  headcount: number;
  salaryBudget?: number | null;
}

/** Body for PUT /api/requisitions/:id. Only a Draft accepts this. */
export interface UpdateRequisitionRequest {
  departmentId: string;
  title: string;
  jobDescription: string;
  headcount: number;
  salaryBudget?: number | null;
}

export interface ApprovalDecisionRequest {
  approve: boolean;
  comment?: string | null;
}

export interface ApprovalChainStep {
  sequence: number;
  approverUserId: string;
  label: string;
}

export interface ApprovalChain {
  id: string;
  name: string;
  departmentId: string | null;
  isActive: boolean;
  steps: ApprovalChainStep[];
}

export interface CreateApprovalChainRequest {
  name: string;
  departmentId: string | null;
  steps: { label: string; approverUserId: string }[];
}

export interface JdTemplate {
  id: string;
  title: string;
  content: string;
  departmentId: string | null;
  isActive: boolean;
}

export interface CreateJdTemplateRequest {
  title: string;
  content: string;
  departmentId?: string | null;
}

// ---------- Module 2: ATS & Sourcing ----------

export interface JobPostingListItem {
  id: string;
  departmentId: string;
  departmentName: string;
  requisitionId: string;
  title: string;
  status: JobStatus;
  employmentType: EmploymentType;
  location: string | null;
  headcount: number;
  postedAt: string | null;
  closedAt: string | null;
  /** Null until published — there is nothing to share before then. */
  publicToken: string | null;
  applicationCount: number;
}

export interface JobPostingDetail extends JobPostingListItem {
  description: string;
  salaryMin: number | null;
  salaryMax: number | null;
  showSalary: boolean;
  applicationFormFieldsJson: string | null;
}

/** Only the requisition — title and description are copied from it server-side. */
export interface CreateJobPostingRequest {
  requisitionId: string;
}

export interface UpdateJobPostingRequest {
  title: string;
  description: string;
  location?: string | null;
  employmentType: EmploymentType;
  headcount: number;
  salaryMin?: number | null;
  salaryMax?: number | null;
  showSalary: boolean;
  applicationFormFieldsJson?: string | null;
}

/**
 * What the PUBLIC app is allowed to know. Deliberately narrower than JobPostingDetail —
 * no department, no requisition, no headcount, and salary only when the posting opted in.
 * Keeping it a separate type is what stops internal fields drifting onto a public page.
 */
export interface PublicJob {
  title: string;
  description: string;
  location: string | null;
  employmentType: EmploymentType;
  companyName: string;
  salaryRange: string | null;
  applicationFormFieldsJson: string | null;
  isOpen: boolean;
}

export interface SubmitApplicationRequest {
  fullName: string;
  email?: string | null;
  phone?: string | null;
  coverNote?: string | null;
  customFieldsJson?: string | null;
}

export interface SubmitApplicationResponse {
  message: string;
}

/**
 * One customer-defined question on an application form (Module 2.2).
 * Mirrors `RecruitOps.Domain.ApplicationFormField`. The array of these is stored as a
 * JSON string in `applicationFormFieldsJson` — the API keeps it opaque so a schema change
 * doesn't require a migration, so both apps parse it with `parseFormFields` below.
 */
export interface ApplicationFormField {
  key: string;
  label: string;
  type: 'text' | 'textarea' | 'number' | 'date' | 'select' | 'checkbox';
  required: boolean;
  /** Only meaningful for `select`. */
  options?: string[] | null;
}

/** Never throws: a malformed schema should render as "no custom fields", not a blank page. */
export function parseFormFields(json: string | null | undefined): ApplicationFormField[] {
  if (!json) return [];
  try {
    const parsed: unknown = JSON.parse(json);
    return Array.isArray(parsed) ? (parsed as ApplicationFormField[]) : [];
  } catch {
    return [];
  }
}

export interface PipelineItem {
  id: string;
  candidateId: string;
  candidateName: string;
  email: string | null;
  phone: string | null;
  status: PipelineStatus;
  source: SourceChannel;
  appliedAt: string;
  coverNote: string | null;
  customFieldsJson: string | null;
}

export interface StageHistoryItem {
  fromStatus: PipelineStatus | null;
  toStatus: PipelineStatus;
  changedAt: string;
  changedByName: string | null;
  note: string | null;
}

export interface MoveStageRequest {
  toStatus: PipelineStatus;
  note?: string | null;
}

// ---------------------------------------------------------------------------
// Module 3 — Interview & Assessment (ADR-0017, ADR-0018)
// ---------------------------------------------------------------------------

/**
 * A user who can be put on an interview panel — `GET /api/users/selectable`.
 *
 * Narrower than `UserListItem` on purpose: no email. The full directory stays Admin-only,
 * so the panel picker being open to recruitment staff does not open the directory too.
 * Approvers appear here deliberately — panel membership is how a role excluded from
 * candidate data (ADR-0018) reaches one application.
 */
export interface SelectableUser {
  id: string;
  displayName: string;
  role: UserRole;
}

export type InterviewMode = 'OnSite' | 'Video' | 'Phone';

export type InterviewStatus = 'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow';

/** `Rating` is 1–5 and the only type that contributes to a numeric comparison. */
export type CriterionType = 'Rating' | 'YesNo' | 'Text';

/** Ordered worst → best; the UI relies on this order for its scale. */
export type HireRecommendation = 'StrongNo' | 'No' | 'Yes' | 'StrongYes';

export type ScorecardStatus = 'Draft' | 'Submitted';

/**
 * A panel member on an interview.
 *
 * `hasSubmittedScorecard` is visible to the whole panel on purpose: knowing a colleague
 * is finished reveals nothing about what they said, and it is what lets a lead chase the
 * outstanding evaluation. Do not gate it behind the blind rule.
 */
export interface InterviewParticipant {
  userId: string;
  displayName: string;
  email: string | null;
  isLead: boolean;
  hasSubmittedScorecard: boolean;
}

export interface Interview {
  id: string;
  jobApplicationId: string;
  round: number;
  scheduledStart: string;
  durationMinutes: number;
  mode: InterviewMode;
  location: string | null;
  status: InterviewStatus;
  agenda: string | null;
  cancellationReason: string | null;
  scorecardTemplateId: string | null;
  scorecardTemplateName: string | null;
  participants: InterviewParticipant[];
}

/**
 * Scheduling also moves the application's stage and writes an `ApplicationStageHistory`
 * row in the same transaction — so a successful POST invalidates any pipeline or history
 * view the caller is holding.
 */
export interface ScheduleInterviewRequest {
  scheduledStart: string;
  durationMinutes: number;
  mode: InterviewMode;
  location?: string | null;
  agenda?: string | null;
  /** Must not be empty: an interview with nobody on it cannot be scored. */
  participantUserIds: string[];
  /** Optional, and must appear in `participantUserIds`. */
  leadUserId?: string | null;
}

/**
 * Rescheduling deliberately carries no panel. Moving the time and swapping an interviewer
 * are different intentions, and one endpoint would wipe a panel whenever a caller omitted
 * it — use `SetPanelRequest` for the panel.
 */
export interface RescheduleInterviewRequest {
  scheduledStart: string;
  durationMinutes: number;
  mode: InterviewMode;
  location?: string | null;
  agenda?: string | null;
}

export interface SetPanelRequest {
  participantUserIds: string[];
  leadUserId?: string | null;
}

export interface CancelInterviewRequest {
  reason?: string | null;
}

export interface CompleteInterviewRequest {
  /** Recorded as `NoShow` rather than `Completed` — Module 5 will want them apart. */
  noShow: boolean;
}

// ---------- Scorecard templates (3.3 configuration) ----------

export interface ScorecardCriterion {
  id: string;
  sequence: number;
  label: string;
  guidance: string | null;
  type: CriterionType;
  isRequired: boolean;
}

export interface ScorecardTemplate {
  id: string;
  name: string;
  description: string | null;
  departmentId: string | null;
  departmentName: string | null;
  jobPostingId: string | null;
  isActive: boolean;
  criteria: ScorecardCriterion[];
}

export interface ScorecardCriterionInput {
  label: string;
  guidance?: string | null;
  type: CriterionType;
  isRequired: boolean;
}

/**
 * `departmentId` and `jobPostingId` are mutually exclusive; both null makes this the
 * company-wide default. Resolution is most-specific-wins, and the API enforces **one
 * active template per scope** — so saving an active template into an occupied scope is a
 * 409, not a silent replacement.
 *
 * `sequence` is derived from the order of `criteria`, so gaps and duplicates cannot be
 * expressed — the same approach as `ApprovalChainStep`.
 */
export interface SaveScorecardTemplateRequest {
  name: string;
  description?: string | null;
  departmentId?: string | null;
  jobPostingId?: string | null;
  isActive: boolean;
  criteria: ScorecardCriterionInput[];
}

// ---------- Filling one in ----------

export interface ScorecardResponse {
  scorecardCriterionId: string;
  /**
   * Snapshotted onto the response when it was written, not joined from the template —
   * a later template edit must not retroactively change what an interviewer was asked.
   * Render this label, never the template's current one.
   */
  criterionLabel: string;
  criterionType: CriterionType;
  rating: number | null;
  yesNo: boolean | null;
  comment: string | null;
}

export interface Scorecard {
  id: string;
  interviewId: string;
  interviewerUserId: string;
  interviewerName: string;
  status: ScorecardStatus;
  submittedAt: string | null;
  recommendation: HireRecommendation | null;
  summaryComment: string | null;
  responses: ScorecardResponse[];
}

/**
 * The caller's own scorecard plus the criteria they are being asked to fill in. Criteria
 * travel with it rather than being fetched separately, so the form cannot be rendered
 * against a template the interview is not actually scored on.
 *
 * `scorecard` is null before the caller has saved anything; `criteria` is empty when no
 * template resolves for the posting, which the form must render as an explanatory state
 * rather than an empty page.
 */
export interface MyScorecard {
  interviewId: string;
  scorecardTemplateId: string | null;
  scorecardTemplateName: string | null;
  criteria: ScorecardCriterion[];
  scorecard: Scorecard | null;
}

/**
 * The panel view (ADR-0017 §3).
 *
 * `hiddenCount` is a count, not a list, and `blindedUntilYouSubmit` is the reason it is
 * non-zero. **Render this as a state, not an error** — "2 evaluations are waiting for
 * yours" is what makes the blind rule read as a process rather than a bug. A recruiter
 * who is not on the panel is not blinded and sees submitted scores immediately.
 */
export interface InterviewScorecards {
  interviewId: string;
  visible: Scorecard[];
  hiddenCount: number;
  blindedUntilYouSubmit: boolean;
}

export interface ScorecardAnswerInput {
  scorecardCriterionId: string;
  /** 1–5, and only meaningful for a `Rating` criterion. */
  rating?: number | null;
  yesNo?: boolean | null;
  comment?: string | null;
}

/**
 * Used for both save-draft (`PUT`) and submit (`POST .../submit`). Drafts may be partial;
 * submitting requires a `recommendation` and every required criterion answered, and is
 * **irreversible**, so the UI must confirm before calling submit.
 *
 * Answers whose `scorecardCriterionId` is not on the resolved template are dropped
 * server-side — the same defence applied to anonymous applicants — so a stale form does
 * not fail loudly, it just loses the stale field.
 */
export interface SaveScorecardRequest {
  recommendation?: HireRecommendation | null;
  summaryComment?: string | null;
  answers: ScorecardAnswerInput[];
}

// ---------- Notes (3.4) ----------

export interface NoteMention {
  userId: string;
  displayName: string;
}

export interface Note {
  id: string;
  jobApplicationId: string;
  interviewId: string | null;
  authorUserId: string;
  authorName: string;
  /** Exactly what the author typed, unescaped. Safe in JSON, **not** safe in the DOM. */
  body: string;
  /**
   * The same text already escaped server-side, with resolved mentions marked up. This is
   * what the SPA renders. Do not re-escape it, and do not build your own from `body` —
   * "escape on output" is meant to be the default path, not something each caller
   * remembers.
   */
  bodyHtml: string;
  createdAt: string;
  mentions: NoteMention[];
}

/**
 * Mentions are parsed server-side from `body` and only resolve for users who could reach
 * the application anyway (ADR-0018) — so an unresolved handle is a silent no-op, not an
 * error, and the client must not promise the user that a mention landed.
 */
export interface CreateNoteRequest {
  body: string;
  /** Optionally pin the note to one interview round. */
  interviewId?: string | null;
}
