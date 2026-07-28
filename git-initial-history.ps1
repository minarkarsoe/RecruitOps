<#
  RecruitOps — build the initial history and push.

  WHY THIS EXISTS: everything since the scaffold commit (the pivot, Modules 1-3, both
  frontends, the whole docs/ knowledge base) was sitting uncommitted in one working tree.
  This replays it as nine area-based commits so `git log` is readable.

  HONEST CAVEAT: these commits are readable, not bisectable. The tree has never been
  compiled, and shared files (AppDbContext, Program.cs, DependencyInjection) can only be
  committed once, so intermediate commits will not build. Only the tip is meant to.

  RUN IT FROM THE REPO ROOT, in PowerShell:
      .\git-initial-history.ps1
      .\git-initial-history.ps1 -RemoteUrl https://github.com/<you>/RecruitOps.git

  Create the GitHub repo FIRST (empty, no README/.gitignore/licence), then pass its URL.
  Without -RemoteUrl the script commits everything and stops before pushing.
#>
param(
  [string]$RemoteUrl = ''
)

# Deliberately NOT 'Stop': git writes ordinary progress to stderr (push especially), and
# Windows PowerShell 5.1 turns native stderr into a terminating error when it is. Exit
# codes are checked explicitly instead.
$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot

function Step($text) { Write-Host "`n=== $text" -ForegroundColor Cyan }
function Guard($what) { if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)" } }

# --- Sanity ------------------------------------------------------------------
if (-not (Test-Path '.git')) { throw 'Not a git repository. Run this from the repo root.' }
if (-not (Test-Path 'CLAUDE.md')) { throw 'This does not look like the RecruitOps root.' }

# --- Stale lock files --------------------------------------------------------
# Left behind on 24 Jul by a git process that crashed. Every git write fails until they
# are gone. Safe to delete: no git process is running.
Step 'Clearing stale git locks'
# Swept recursively, not by name: index.lock and HEAD.lock are the well-known two, but
# refs/heads/<branch>.lock is left behind by the same crash and blocks every ref update.
$locks = @(Get-ChildItem '.git' -Recurse -Force -Filter '*.lock' -ErrorAction SilentlyContinue)
$locks += @(Get-ChildItem '.git' -Force -Filter 'probe.tmp' -ErrorAction SilentlyContinue)
if ($locks.Count -eq 0) { Write-Host '  none' }
foreach ($lock in $locks) {
  Remove-Item $lock.FullName -Force
  Write-Host ("  removed {0}" -f $lock.FullName.Substring((Get-Location).Path.Length + 1))
}
# If a real git process were running, the locks would come straight back.
$still = @(Get-ChildItem '.git' -Recurse -Force -Filter '*.lock' -ErrorAction SilentlyContinue)
if ($still.Count -gt 0) { throw 'Locks reappeared — a git process really is running. Close it and retry.' }

# --- Clean index -------------------------------------------------------------
# Makes a re-run after a failure deterministic: each Commit below stages its own paths,
# so anything a previous attempt left staged would otherwise ride along with commit 1.
# This unstages only — it touches no file and no commit.
Step 'Resetting the index'
git reset -q
Guard 'git reset'

# --- Strays ------------------------------------------------------------------
Step 'Removing strays'
# Left over from the pre-ADR-0012 single frontend app. The authoritative lock is the one
# at the workspace root.
if (Test-Path 'frontend\package-lock.json') { Remove-Item 'frontend\package-lock.json' -Force; Write-Host '  removed frontend/package-lock.json' }

# --- Branches ----------------------------------------------------------------
Step 'Branches'
git branch -M main
Guard 'git branch -M main'
# Agency-era work, superseded by ADR-0001. It is in the reflog if it is ever wanted.
$stale = git branch --list 'feat/client-crm-list'
if ($stale) { git branch -D feat/client-crm-list }
git branch

function Commit($subject, $body, $paths) {
  Step $subject
  git add -- $paths
  Guard 'git add'
  $staged = git diff --cached --name-only
  if (-not $staged) { Write-Host '  nothing staged, skipping' -ForegroundColor Yellow; return }
  Write-Host ("  {0} file(s)" -f ($staged | Measure-Object).Count)
  git commit -q -m $subject -m $body
  Guard "commit '$subject'"
}

Commit 'chore(build): move to .NET 10 LTS and add container packaging' @'
All six projects target net10.0 with matching package versions (ADR-0010), and
System.Security.Cryptography.Xml is pinned to 10.0.6 — 10.0.0-10.0.5 carry CVE-2026-33116.

The Dockerfile is the packaging artefact (ADR-0015): a multi-stage build whose `test`
target runs the whole suite inside the SDK image, so the suite is runnable without a
local .NET SDK. docker-compose brings up Postgres + API + both web apps.

.gitignore paths are unanchored now — ADR-0012 split one app at frontend/ into two, so
`frontend/.next/` alone had stopped matching.
'@ @(
  '.gitignore',
  'backend/RecruitOps.sln',
  'backend/src/Api/.env.example',
  'backend/src/Api/RecruitOps.Api.csproj',
  'backend/src/Api/appsettings.json',
  'backend/src/Application/RecruitOps.Application.csproj',
  'backend/src/Domain/RecruitOps.Domain.csproj',
  'backend/src/Infrastructure/RecruitOps.Infrastructure.csproj',
  'backend/tests/RecruitOps.Domain.Tests/RecruitOps.Domain.Tests.csproj',
  '.env.example',
  'backend/.dockerignore',
  'backend/Dockerfile',
  'backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj',
  'docker-compose.yml'
)

Commit 'refactor(domain)!: pivot from recruitment agency to in-house talent acquisition' @'
A tenant is now a company running its own talent acquisition, not an agency serving
clients (ADR-0001). The agency vocabulary is not deprecated, it is removed: Client,
Contract, ClientTier, ContractStatus and ClientFeedback are gone, along with their
services and controllers, because a half-migrated model invites code that reads as if
both worlds still exist.

Renamed rather than aliased, for the same reason: Tenant -> Company, Job -> JobPosting
(now department-owned), Application -> JobApplication. That last one is not cosmetic —
`Application` collided with the RecruitOps.Application namespace (CS0118).

UserRole loses `Client` and gains HrDirector/HiringManager/Approver; PipelineStatus
loses `SentToClient` and renames `Placed` to `Hired`.

BREAKING CHANGE: the agency entity model and its endpoints no longer exist.
'@ @(
  'backend/src/Api/Controllers/CandidatesController.cs',
  'backend/src/Api/Controllers/ClientsController.cs',
  'backend/src/Api/Controllers/ContractsController.cs',
  'backend/src/Api/Controllers/JobsController.cs',
  'backend/src/Api/Controllers/PortalController.cs',
  'backend/src/Application/Interfaces/IClientService.cs',
  'backend/src/Application/Interfaces/IContractService.cs',
  'backend/src/Domain/Entities/Application.cs',
  'backend/src/Domain/Entities/Candidate.cs',
  'backend/src/Domain/Entities/Client.cs',
  'backend/src/Domain/Entities/Contract.cs',
  'backend/src/Domain/Entities/Job.cs',
  'backend/src/Domain/Entities/JobChannelPost.cs',
  'backend/src/Domain/Entities/PortalLink.cs',
  'backend/src/Domain/Entities/Tenant.cs',
  'backend/src/Domain/Entities/User.cs',
  'backend/src/Domain/Enums/ClientFeedback.cs',
  'backend/src/Domain/Enums/ClientTier.cs',
  'backend/src/Domain/Enums/ContractStatus.cs',
  'backend/src/Domain/Enums/PipelineStatus.cs',
  'backend/src/Domain/Enums/UserRole.cs',
  'backend/tests/RecruitOps.Domain.Tests/PipelineStatusTests.cs',
  'backend/src/Domain/Entities/Company.cs'
)

Commit 'feat(auth): JWT bearer, RBAC, tenant isolation and department scoping' @'
Self-issued HS256 tokens carrying sub, tenant_id and a role claim (ADR-0002), with a
FallbackPolicy of `authenticated` so a new endpoint is closed until someone opens it.

Two filters, deliberately different in kind:

- Tenant isolation is a global EF query filter — one instance and one database per
  company (ADR-0004) makes it a dormant safety net rather than the load-bearing rule.
- Department scoping is an explicit predicate per service method (ADR-0003), because
  the rule is not uniform. It reads from the database, not from the token, so revoking
  someone's access takes effect immediately instead of when their 8-hour token expires.

Out-of-scope rows return 404 rather than 403, so existence is not leaked. RoleScope is
the only place a role name is written — every predicate about a role goes through it.

Login is brute-force resistant (ADR-0016): a per-IP limiter plus a per-account throttle
that locks after 5 failures for 15 minutes, and locks identically for unknown emails so
the lockout is not an enumeration oracle.

Departments get full admin CRUD and membership assignment, with no delete — requisitions
and the audit trail reference them, so deactivation stops new work and keeps the history.
'@ @(
  'backend/src/Api/Program.cs',
  'backend/src/Infrastructure/DependencyInjection.cs',
  'backend/src/Infrastructure/Persistence/AppDbContext.cs',
  'backend/src/Api/Auth/AppClaims.cs',
  'backend/src/Api/Auth/CurrentTenant.cs',
  'backend/src/Api/Auth/CurrentUser.cs',
  'backend/src/Api/Auth/LoginRateLimitOptions.cs',
  'backend/src/Api/Auth/Policies.cs',
  'backend/src/Api/Auth/RateLimitPolicies.cs',
  'backend/src/Api/Auth/Roles.cs',
  'backend/src/Api/Controllers/AuthController.cs',
  'backend/src/Api/Controllers/DepartmentsController.cs',
  'backend/src/Api/Controllers/UsersController.cs',
  'backend/src/Application/Common/ICurrentUser.cs',
  'backend/src/Application/Common/IDepartmentAccess.cs',
  'backend/src/Application/DTOs/DepartmentDtos.cs',
  'backend/src/Application/DTOs/DepartmentListItemDto.cs',
  'backend/src/Application/DTOs/LoginRequest.cs',
  'backend/src/Application/DTOs/LoginResponse.cs',
  'backend/src/Application/DTOs/UserListItemDto.cs',
  'backend/src/Application/Interfaces/IAuthService.cs',
  'backend/src/Application/Interfaces/IDepartmentService.cs',
  'backend/src/Application/Interfaces/ILoginThrottle.cs',
  'backend/src/Application/Interfaces/ITokenService.cs',
  'backend/src/Domain/Entities/Department.cs',
  'backend/src/Domain/Entities/UserDepartment.cs',
  'backend/src/Domain/RoleScope.cs',
  'backend/src/Infrastructure/Migrations/20260727085909_InitialCreate.Designer.cs',
  'backend/src/Infrastructure/Migrations/20260727085909_InitialCreate.cs',
  'backend/src/Infrastructure/Persistence/AppDbContextFactory.cs',
  'backend/src/Infrastructure/Persistence/DatabaseStartup.cs',
  'backend/src/Infrastructure/Persistence/DbInitializer.cs',
  'backend/src/Infrastructure/Services/AuthService.cs',
  'backend/src/Infrastructure/Services/DepartmentAccess.cs',
  'backend/src/Infrastructure/Services/DepartmentService.cs',
  'backend/src/Infrastructure/Services/JwtTokenService.cs',
  'backend/src/Infrastructure/Services/LoginThrottle.cs',
  'backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs',
  'backend/tests/RecruitOps.Api.Tests/CustomWebAppFactory.cs',
  'backend/tests/RecruitOps.Api.Tests/DepartmentAdminTests.cs',
  'backend/tests/RecruitOps.Api.Tests/DepartmentIsolationTests.cs',
  'backend/tests/RecruitOps.Api.Tests/JwtTokenServiceTests.cs',
  'backend/tests/RecruitOps.Api.Tests/LoginThrottleTests.cs',
  'backend/tests/RecruitOps.Api.Tests/TestAuthHandler.cs'
)

Commit 'feat(requisitions): Module 1 — requisition lifecycle and sequential approval' @'
Draft -> submit -> sequential approve/reject -> Approved/Rejected, or cancel from Draft
or PendingApproval.

The approval chain is snapshotted onto the requisition at submit, so later edits to the
chain cannot rewrite decisions already recorded — the audit trail stays truthful. Step
sequence is derived from list order, which makes gaps and duplicates unrepresentable.

The inbox returns only requisitions whose lowest-sequence Waiting step belongs to the
caller and whose status is still PendingApproval, so approvers cannot work the queue out
of order and a cancelled requisition does not linger in it.

Editing is Draft-only and 409s after submit — approvers never decide on a moving target.
Moving a Draft between departments requires access to both ends.
'@ @(
  'backend/src/Api/Controllers/ApprovalChainsController.cs',
  'backend/src/Api/Controllers/JdTemplatesController.cs',
  'backend/src/Api/Controllers/RequisitionsController.cs',
  'backend/src/Application/DTOs/ApprovalChainDto.cs',
  'backend/src/Application/DTOs/ApprovalDecisionRequest.cs',
  'backend/src/Application/DTOs/ApprovalStepDto.cs',
  'backend/src/Application/DTOs/CreateApprovalChainRequest.cs',
  'backend/src/Application/DTOs/CreateJdTemplateRequest.cs',
  'backend/src/Application/DTOs/CreateRequisitionRequest.cs',
  'backend/src/Application/DTOs/JdTemplateDto.cs',
  'backend/src/Application/DTOs/RequisitionDetailDto.cs',
  'backend/src/Application/DTOs/RequisitionListItemDto.cs',
  'backend/src/Application/DTOs/UpdateRequisitionRequest.cs',
  'backend/src/Application/Interfaces/IApprovalChainService.cs',
  'backend/src/Application/Interfaces/IJdTemplateService.cs',
  'backend/src/Application/Interfaces/IRequisitionService.cs',
  'backend/src/Domain/Entities/ApprovalChain.cs',
  'backend/src/Domain/Entities/ApprovalChainStep.cs',
  'backend/src/Domain/Entities/JdTemplate.cs',
  'backend/src/Domain/Entities/Requisition.cs',
  'backend/src/Domain/Entities/RequisitionApproval.cs',
  'backend/src/Domain/Enums/ApprovalDecision.cs',
  'backend/src/Domain/Enums/RequisitionStatus.cs',
  'backend/src/Infrastructure/Migrations/20260727101933_Module1Requisitions.Designer.cs',
  'backend/src/Infrastructure/Migrations/20260727101933_Module1Requisitions.cs',
  'backend/src/Infrastructure/Services/ApprovalChainService.cs',
  'backend/src/Infrastructure/Services/JdTemplateService.cs',
  'backend/src/Infrastructure/Services/RequisitionService.cs',
  'backend/tests/RecruitOps.Api.Tests/RequisitionApprovalFlowTests.cs',
  'backend/tests/RecruitOps.Api.Tests/RequisitionScopingTests.cs'
)

Commit 'feat(ats): Module 2 — job postings, public application page and pipeline' @'
Requisition -> posting -> public page -> application -> pipeline, with stage history
written from the first moment.

Nothing is advertised without an approval behind it: a posting requires an Approved
requisition, one per requisition, enforced in the service and by a unique index so the
guarantee survives future code paths.

The anonymous surface has no tenant claim, so the global query filters match nothing
there. PublicJobService reads with IgnoreQueryFilters() and re-applies the tenant from
the token's own row, and sets TenantId explicitly on every write. Unknown, revoked,
expired and unpublished tokens are one indistinguishable 404. Salary is private unless
opted in, and PublicJobDto is a deliberately narrower type so internal fields cannot
drift onto a public page.

Customer-defined application fields are validated twice by ApplicationFormSchema — the
schema when a recruiter saves it, the answers when a stranger submits them — and the
answer document is rebuilt from the schema rather than stored as sent.

ApplicationStageHistory is written on every stage change including the anonymous arrival:
Module 5's metrics are differences between these timestamps and cannot be reconstructed
later. Hired and Rejected are terminal.
'@ @(
  'backend/src/Api/Controllers/ApplicationsController.cs',
  'backend/src/Api/Controllers/JobPostingsController.cs',
  'backend/src/Api/Controllers/PublicJobsController.cs',
  'backend/src/Application/DTOs/JobPostingDtos.cs',
  'backend/src/Application/DTOs/PipelineDtos.cs',
  'backend/src/Application/DTOs/PublicJobDtos.cs',
  'backend/src/Application/Interfaces/IJobPostingService.cs',
  'backend/src/Application/Interfaces/IPipelineService.cs',
  'backend/src/Application/Interfaces/IPublicJobService.cs',
  'backend/src/Domain/ApplicationFormSchema.cs',
  'backend/src/Domain/ContactNormalizer.cs',
  'backend/src/Domain/Entities/ApplicationStageHistory.cs',
  'backend/src/Domain/Entities/JobApplication.cs',
  'backend/src/Domain/Entities/JobPosting.cs',
  'backend/src/Domain/Enums/EmploymentType.cs',
  'backend/src/Infrastructure/Migrations/20260728023109_Module2Ats.Designer.cs',
  'backend/src/Infrastructure/Migrations/20260728023109_Module2Ats.cs',
  'backend/src/Infrastructure/Services/JobPostingService.cs',
  'backend/src/Infrastructure/Services/PipelineService.cs',
  'backend/src/Infrastructure/Services/PublicJobService.cs',
  'backend/tests/RecruitOps.Api.Tests/JobPostingFlowTests.cs',
  'backend/tests/RecruitOps.Api.Tests/PublicApplicationTests.cs',
  'backend/tests/RecruitOps.Domain.Tests/ApplicationFormSchemaTests.cs'
)

Commit 'feat(interviews): Module 3 — interviews, blind scorecards and debrief notes' @'
Scheduling, panels, blind scoring and an @-mentionable note thread (ADR-0017).

Scheduling moves the application's stage and writes the history row in one
SaveChangesAsync, so an interview cannot exist against an application still at Screening.
A second round writes no history row, because a no-op transition would be counted.

Blind scoring is keyed on participation, not on reach: a panel member sees a count of
what is withheld until they submit, and everything after. Submitting is irreversible,
which is what makes the rule mean anything. Criteria are snapshotted onto each response
so a template edit cannot retroactively change what an interviewer was asked — which is
why ScorecardResponse deliberately has no FK to ScorecardCriterion.

Panel participation is a read grant scoped to one application: no department access, no
sibling application, no writes.

ADR-0018 is folded in. CanAccessAsync answers 'does this role cross departments', and
Approver does — on the requisition axis. Asked about a candidate, that same true handed
an approver every application in the company. Candidate reach is now a second question,
answered in RoleScope and applied through IApplicationAccess. NoteService had re-derived
the rule by hand as `role is UserRole.HiringManager` and so resolved @finance.approver —
the exact handle its own doc comment named as the thing it prevented.

Notes store raw text and escape on output; mentions resolve only for users who could
reach the application anyway, so an unresolved handle is a silent no-op by design.
'@ @(
  'backend/src/Api/Controllers/InterviewsController.cs',
  'backend/src/Api/Controllers/NotesController.cs',
  'backend/src/Api/Controllers/ScorecardTemplatesController.cs',
  'backend/src/Application/Common/IApplicationAccess.cs',
  'backend/src/Application/DTOs/InterviewDtos.cs',
  'backend/src/Application/DTOs/NoteDtos.cs',
  'backend/src/Application/DTOs/ScorecardDtos.cs',
  'backend/src/Application/Interfaces/IInterviewService.cs',
  'backend/src/Application/Interfaces/INoteService.cs',
  'backend/src/Application/Interfaces/IScorecardService.cs',
  'backend/src/Application/Interfaces/IScorecardTemplateService.cs',
  'backend/src/Domain/Entities/Interview.cs',
  'backend/src/Domain/Entities/InterviewParticipant.cs',
  'backend/src/Domain/Entities/Note.cs',
  'backend/src/Domain/Entities/NoteMention.cs',
  'backend/src/Domain/Entities/Scorecard.cs',
  'backend/src/Domain/Entities/ScorecardCriterion.cs',
  'backend/src/Domain/Entities/ScorecardResponse.cs',
  'backend/src/Domain/Entities/ScorecardTemplate.cs',
  'backend/src/Domain/Enums/CriterionType.cs',
  'backend/src/Domain/Enums/HireRecommendation.cs',
  'backend/src/Domain/Enums/InterviewMode.cs',
  'backend/src/Domain/Enums/InterviewStatus.cs',
  'backend/src/Domain/Enums/ScorecardStatus.cs',
  'backend/src/Domain/MentionParser.cs',
  'backend/src/Infrastructure/Migrations/20260728061832_Module3Interviews.Designer.cs',
  'backend/src/Infrastructure/Migrations/20260728061832_Module3Interviews.cs',
  'backend/src/Infrastructure/Migrations/AppDbContextModelSnapshot.cs',
  'backend/src/Infrastructure/Services/ApplicationAccess.cs',
  'backend/src/Infrastructure/Services/InterviewService.cs',
  'backend/src/Infrastructure/Services/NoteService.cs',
  'backend/src/Infrastructure/Services/ScorecardService.cs',
  'backend/src/Infrastructure/Services/ScorecardTemplateService.cs',
  'backend/tests/RecruitOps.Api.Tests/ApplicationNoteTests.cs',
  'backend/tests/RecruitOps.Api.Tests/ApproverReachTests.cs',
  'backend/tests/RecruitOps.Api.Tests/InterviewFlowTests.cs',
  'backend/tests/RecruitOps.Api.Tests/Module3Scenario.cs',
  'backend/tests/RecruitOps.Api.Tests/ScorecardBlindScoringTests.cs',
  'backend/tests/RecruitOps.Api.Tests/ScorecardTemplateResolutionTests.cs',
  'backend/tests/RecruitOps.Domain.Tests/MentionParserTests.cs'
)

Commit 'feat(web): npm workspaces, shared UI and types, internal SPA and public job site' @'
ADR-0012 splits one Next.js app into two, because they answer to different constraints:

- frontend/internal — Vite + React SPA behind a login. No SEO to serve, so SSR was cost
  without benefit.
- frontend/public — Next.js SSR, which exists for one reason: a job link pasted into
  Facebook or Viber needs Open Graph metadata in the HTML, and a client-rendered page
  has none.

packages/ui and packages/types are consumed by both and are the anti-drift mechanism:
one status vocabulary, one set of API shapes mirroring the backend DTOs. Adding a status
to StatusPill is cheaper than the alternative, a page-local badge that drifts the first
time a colour changes.

Screens cover Module 1 end to end, Module 2's posting/form-builder/pipeline, and Module
3's scheduling, scorecard form, blind panel view, note thread and template admin.

Two rules the UI must not relax: notes render bodyHtml (server-escaped) and never body,
and lib/auth.ts is the client mirror of RoleScope — the only place this app writes a
role name, for the reason ADR-0018 exists.
'@ @(
  'frontend/.env.example',
  'frontend/app/candidates/page.tsx',
  'frontend/app/clients/page.tsx',
  'frontend/app/dashboard/page.tsx',
  'frontend/app/globals.css',
  'frontend/app/jobs/page.tsx',
  'frontend/app/layout.tsx',
  'frontend/app/page.tsx',
  'frontend/app/portal/[token]/page.tsx',
  'frontend/components/ui/StatusPill.tsx',
  'frontend/lib/api.ts',
  'frontend/lib/types.ts',
  'frontend/next.config.mjs',
  'frontend/package.json',
  'frontend/postcss.config.mjs',
  'frontend/tailwind.config.ts',
  'frontend/tests/statusPill.test.tsx',
  'frontend/tsconfig.json',
  'frontend/internal/.dockerignore',
  'frontend/internal/.env.example',
  'frontend/internal/Dockerfile',
  'frontend/internal/index.html',
  'frontend/internal/nginx.conf',
  'frontend/internal/package.json',
  'frontend/internal/postcss.config.js',
  'frontend/internal/src/App.tsx',
  'frontend/internal/src/components/AppLayout.tsx',
  'frontend/internal/src/components/ApplicationDebrief.tsx',
  'frontend/internal/src/components/ApplicationNotes.tsx',
  'frontend/internal/src/components/FormFieldBuilder.tsx',
  'frontend/internal/src/components/RequireAuth.tsx',
  'frontend/internal/src/index.css',
  'frontend/internal/src/lib/api.ts',
  'frontend/internal/src/lib/auth.ts',
  'frontend/internal/src/main.tsx',
  'frontend/internal/src/pages/ApprovalChainsPage.tsx',
  'frontend/internal/src/pages/DepartmentsPage.tsx',
  'frontend/internal/src/pages/InboxPage.tsx',
  'frontend/internal/src/pages/InterviewDetailPage.tsx',
  'frontend/internal/src/pages/JdTemplatesPage.tsx',
  'frontend/internal/src/pages/JobPostingDetailPage.tsx',
  'frontend/internal/src/pages/JobPostingsPage.tsx',
  'frontend/internal/src/pages/LoginPage.tsx',
  'frontend/internal/src/pages/RequisitionDetailPage.tsx',
  'frontend/internal/src/pages/RequisitionFormPage.tsx',
  'frontend/internal/src/pages/RequisitionsPage.tsx',
  'frontend/internal/src/pages/ScorecardTemplatesPage.tsx',
  'frontend/internal/src/vite-env.d.ts',
  'frontend/internal/tailwind.config.js',
  'frontend/internal/tsconfig.json',
  'frontend/internal/vite.config.ts',
  'frontend/public/.dockerignore',
  'frontend/public/.env.example',
  'frontend/public/Dockerfile',
  'frontend/public/app/error.tsx',
  'frontend/public/app/globals.css',
  'frontend/public/app/jobs/[token]/ApplicationForm.tsx',
  'frontend/public/app/jobs/[token]/page.tsx',
  'frontend/public/app/layout.tsx',
  'frontend/public/lib/api.ts',
  'frontend/public/next.config.mjs',
  'frontend/public/package.json',
  'frontend/public/postcss.config.mjs',
  'frontend/public/public/.gitkeep',
  'frontend/public/tailwind.config.js',
  'frontend/public/tsconfig.json',
  'package-lock.json',
  'package.json',
  'packages/types/package.json',
  'packages/types/src/index.ts',
  'packages/ui/package.json',
  'packages/ui/src/Button.tsx',
  'packages/ui/src/Card.tsx',
  'packages/ui/src/StatusPill.tsx',
  'packages/ui/src/index.ts',
  'packages/ui/tailwind-preset.js'
)

Commit 'test(web): wire up Vitest and cover Module 3''s UI logic; add CI' @'
The frontend had no tests at all, and Module 3 shipped the first real conditional logic
in this codebase — logic where being wrong is quiet rather than loud.

27 tests over the three cases that fail silently:

- the blind rule's three renderings (hidden > 0, hidden === 0, not blinded); picking the
  wrong one looks like a bug in the rule, not in the view
- the scorecard payload filter, extracted to lib/scorecard.ts so it can be asserted
  directly. A `No` is an answer and a rating of 0 is an answer; a truthiness check on
  either is invisible until drafts stop saving. One test pins that the payload filter and
  the submit-completeness check still agree — they agreed only by construction
- NoteBody's HTML injection: bodyHtml rendered as markup, not re-escaped, with the
  span.mention element that index.css styles surviving

The harness was proved to fail before it was believed: three deliberate mutations
produced 5 failures across all three files, and tsc was checked the same way. A green run
from a checker nobody has seen fail is worse than no run, because it gets believed.

CI runs `docker build --target test ./backend` with --progress=plain and
--no-cache-filter=build,test — BuildKit collapses test output and a cached COPY layer
will re-report an old pass count, so without both flags a green build is not evidence the
new tests ran. The frontend job runs npm ci, typecheck, test and build. This is the fix
for three sessions of code written in environments with no .NET SDK and an allowlist
blocking nuget.org.
'@ @(
  '.github/workflows/ci.yml',
  'frontend/internal/src/components/ApplicationNotes.test.tsx',
  'frontend/internal/src/lib/scorecard.test.ts',
  'frontend/internal/src/lib/scorecard.ts',
  'frontend/internal/src/pages/InterviewDetailPage.test.tsx',
  'frontend/internal/src/test/fixtures.ts',
  'frontend/internal/src/test/setup.ts',
  'frontend/internal/vitest.config.ts'
)

Commit 'docs: knowledge base — ADRs, module specs, status and the project constitution' @'
docs/ is the single source of truth and every task starts at docs/README.md.

- decisions/ — 19 ADRs, from the pivot (0001) through per-company deployment (0004),
  department scoping (0003), the frontend split (0012) and approver candidate-data
  exclusion (0018)
- product/ — overview and the 7 module specs
- architecture/ — data model, auth and tenancy, deployment, local development
- status/ — FEATURE-STATUS (what is built), CHANGELOG (what changed), NEXT-SESSION (where
  to pick up), MIGRATION-PLAN, and the Module 3 security review

NEXT-SESSION and FEATURE-STATUS exist so a fresh session starts cheaply: conversation
history is re-sent every turn, so a session that outlives its feature costs a lot and
adds nothing.

Also removes the duplicate reference doc left at the repo root — the copy under
docs/reference/ is byte-identical.
'@ @(
  'CLAUDE.md',
  'docs/architecture.md',
  'docs/README.md',
  'docs/architecture/auth-and-tenancy.md',
  'docs/architecture/data-model.md',
  'docs/architecture/deployment.md',
  'docs/architecture/local-development.md',
  'docs/architecture/overview.md',
  'docs/decisions/ADR-0001-pivot-to-inhouse.md',
  'docs/decisions/ADR-0002-jwt-auth.md',
  'docs/decisions/ADR-0003-department-scoping.md',
  'docs/decisions/ADR-0004-single-tenant-deployment.md',
  'docs/decisions/ADR-0005-commercial-model.md',
  'docs/decisions/ADR-0006-mvp-scope.md',
  'docs/decisions/ADR-0007-productization-and-addons.md',
  'docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md',
  'docs/decisions/ADR-0009-myanmar-script-handling.md',
  'docs/decisions/ADR-0010-dotnet-10-lts.md',
  'docs/decisions/ADR-0011-commercial-model-v2.md',
  'docs/decisions/ADR-0012-frontend-split.md',
  'docs/decisions/ADR-0013-infrastructure-and-storage.md',
  'docs/decisions/ADR-0014-multi-channel-sourcing.md',
  'docs/decisions/ADR-0015-containerisation.md',
  'docs/decisions/ADR-0016-login-brute-force-protection.md',
  'docs/decisions/ADR-0017-interview-and-assessment.md',
  'docs/decisions/ADR-0018-approver-candidate-data-exclusion.md',
  'docs/decisions/ADR-0019-panel-picker-directory.md',
  'docs/product/modules/01-job-requisition-approval.md',
  'docs/product/modules/02-ats-and-sourcing.md',
  'docs/product/modules/03-interview-and-assessment.md',
  'docs/product/modules/04-offer-and-preboarding.md',
  'docs/product/modules/05-reporting-and-analytics.md',
  'docs/product/modules/06-planning-and-budgeting.md',
  'docs/product/modules/07-settings-and-integrations.md',
  'docs/product/modules/08-multi-channel-sourcing.md',
  'docs/product/overview.md',
  'docs/reference/B2B Recruitment Agency Platform.docx',
  'docs/reference/In-house Recruitment - Product Overview.pdf',
  'docs/status/CHANGELOG.md',
  'docs/status/FEATURE-STATUS.md',
  'docs/status/MIGRATION-PLAN.md',
  'docs/status/NEXT-SESSION.md',
  'docs/status/SECURITY-REVIEW-MODULE-3.md'
)


# The duplicate reference doc at the repo root — byte-identical to the copy under
# docs/reference/, and only the latter is where anyone looks.
Step 'Removing the duplicated reference doc from the root'
$dupe = git ls-files -- 'B2B Recruitment Agency Platform.docx'
if ($dupe) {
  git rm -q 'B2B Recruitment Agency Platform.docx'
  Guard 'git rm'
  git commit -q -m 'docs: drop the duplicated reference doc from the repo root' -m 'Byte-identical to docs/reference/B2B Recruitment Agency Platform.docx.'
  Guard 'commit dupe removal'
}

# --- Result ------------------------------------------------------------------
Step 'History'
git log --oneline
Step 'Anything left uncommitted?'
git status --short

# --- Push --------------------------------------------------------------------
if ($RemoteUrl) {
  Step "Pushing to $RemoteUrl"
  $existing = git remote
  if ($existing -contains 'origin') { git remote set-url origin $RemoteUrl }
  else { git remote add origin $RemoteUrl }
  git push -u origin main
  Guard 'git push'
  Write-Host "`nPushed. CI runs on this push — check the Actions tab." -ForegroundColor Green
  Write-Host "The backend has never been compiled; that run is the first time it will be." -ForegroundColor Yellow
} else {
  Write-Host "`nCommitted, not pushed. Create an EMPTY repo on GitHub, then:" -ForegroundColor Yellow
  Write-Host '  git remote add origin https://github.com/<you>/RecruitOps.git'
  Write-Host '  git push -u origin main'
}

# This script is a one-shot; leaving it in the tree would only invite a second run.
Remove-Item $PSCommandPath -Force -ErrorAction SilentlyContinue
