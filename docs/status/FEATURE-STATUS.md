# Feature Status

**Last updated:** 2026-08-18 (**status re-derived from the code, not from the previous version of this file** — the module table below was wrong about four modules; see the note under it. Suites re-run the same day: **backend 527/527**, frontend 342/342, typecheck clean. Also: **UI/UX design kit complete — 25 static screens across all seven modules**, in `design/internal/` and `design/public/`, indexed at `design/internal/index.html`; five spec gaps it surfaced are logged under Known gaps) · Previously 2026-08-17 (marketing landing page added; design system pivoted off the agency model and its contrast failures fixed — frontend now **342/342 Vitest green**, 43 files, 0 typecheck errors) · Previously 2026-08-13 (Delivery Readiness & Feature Flags complete — 507/507 backend tests + 318/318 frontend tests green, 0 typecheck errors) · Legend: ✅ done · 🚧 partial · ⬜ not started · ❌ removed/to remove

> 🎨 **Marketing surface (2026-08-17):** `marketing/landing.html` — standalone single-file
> HTML + Tailwind CDN, not a route in either app. Visual system recorded in `DESIGN.md`,
> product truth in `PRODUCT.md` (both at repo root). No backend or frontend code was touched.

> ✅ **Backend: 644/644 green** (62 domain + 582 api), re-run 2026-08-26. That day's ADR-0026 security review found and fixed a HIGH — an Approver could bulk-upload CVs into any posting in the tenant, and read the batch back ([SECURITY-REVIEW-ADR-0026.md](SECURITY-REVIEW-ADR-0026.md)) — and its `X-Tenant-Id` observation was then closed by wiring the header up for super-admins only (+19 tests). Covers Module 1 end to end, login
> throttling, department administration, Module 2 requisitions/postings/pipeline/CV ingestion/Full-Text search, Module 3 interviews/scorecards/notes, Module 5 Reporting & Analytics, Module 7 Dynamic RBAC & User Management, and Delivery Prerequisites (`/api/version`, Feature Flags, Healthchecks).
>
> ✅ **`docker compose up --build` runs** — Postgres + API + both frontends, migrations applying on startup.
>
> ✅ **Frontend: 375/375 Vitest passing** across 46 test files, `npm run typecheck` 0 errors across both apps, `npm run build` clean (re-run 2026-08-25 after `features/pipeline`, `features/analytics` and `frontend/public/app` were rebuilt against the design kit; `ChartMarks.test.tsx` and `DeliveryLogPage.test.tsx` are new — the first pins the charts to one hue, `aria-pressed`, `role="img"` and no `dark:` variants; the second pins the delivery log's failure reason, its neutral treatment of `Suppressed`, and that filtering reaches the server rather than the browser).
>
> ⚠️ **The public app (`frontend/public`) has no tests at all.** It is a stranger's only view of
> the product and the least-covered surface in the repo; its rebuild was verified from the built
> stylesheet because the job page needs a running API.
>
> ✅ **Granular Dynamic RBAC & Permission-Aware UX complete** — `/api/roles`, `/api/permissions`, `/api/users`, `[HasPermission]` policy attribute, User Directory (`/users`), Role Builder (`/roles`), and dynamic permission-aware UI filtering across navigation sidebar and action buttons.
>
> 🔴 **Fixed 2026-08-03 — the permission-aware UX was inert from the day it shipped.**
> `LoginResponse` had no `Permissions` member, so the SPA's `session.permissions` was always
> `undefined`, and `hasPermission()` treated "no permissions field" as "unknown → allow".
> Every gated sidebar link, action button and `RequirePermission` route guard rendered for
> every user. Server-side `[HasPermission]` enforcement was never affected — the API re-derives
> permissions from the signed JWT — so this was a UI-disclosure bug, not a privilege
> escalation. The API now ships the permission set and the client fails closed.
> **The tests did not catch it because every one of them constructed a session with an
> explicit `permissions` array — a shape the API never returned.**

## Summary by module

| # | Module | Status | Notes |
|---|---|---|---|
| — | Foundation (scaffold, layering, CI-less tooling) | ✅ | Structure per CLAUDE.md |
| — | Multi-tenancy | ✅ | Query filters + claim-based resolver, isolation-tested |
| 1 | Job Requisition & Approval | ✅ API + UI | ⭐ MVP · full loop drivable from the browser: chain config → requisition → submit → sequential approve/reject → cancel. |
| 2 | ATS & Sourcing | 🚧 API + UI | ⭐ MVP · **2.1, 2.2, 2.6, 2.7 built. 2.3 is text-only — no OCR, so image and scanned CVs yield nothing. 2.4 Smart Match is API-only. 2.5 ships as a list, not the spec's Kanban + 360° view.** Posting → public page + custom form → application → pipeline; bulk CV ingestion with local extraction (`BulkResumeService` + `BulkResumeWorker`, `DocumentExtraction/`) — **rewritten 2026-08-21 onto ADR-0026's durable queue**, so a batch survives a restart and the uploaded bytes live in object storage rather than in process memory; AI profiling behind a key (`AiIntegrationService`); trigram search (`SearchService`, `AddPgTrgmAndSearchIndexes`). Remaining: file-upload field type (waits on ADR-0013), the merge-two-existing-candidates UI, and **wiring the AI surface to a route** — see the AI row below. |
| 3 | Interview & Assessment | 🚧 API + UI | ⭐ MVP · **3.3 scorecards + 3.4 notes built and tested; UI built** (scheduling, scorecard form, blind panel view, note thread, template admin). **3.2 invitations built 2026-08-20** — scheduling and rescheduling queue a candidate email through the ADR-0026 outbox, sent by the worker over SMTP; cancelling suppresses a queued one. 3.1 calendar free/busy still deferred — no calendar client exists. **The delivery log landed 2026-08-25** (`GET /api/delivery`, `/delivery`), so a failed or suppressed invitation is now visible to the recruiter instead of only to the database. ⚠️ Three behaviours have never been eyeballed — see the warning below the table. |
| 4 | Offer & Pre-boarding | ⬜ code · ✅ spec + design | Post-MVP. Scope rewritten 2026-08-18; 4 screens drawn. **No longer blocked** — `IEmailSender` and the outbox exist; 4.1/4.2/4.3 each need a handler and an enqueue call. |
| 5 | Reporting & Analytics | ✅ API + UI | ⭐ MVP · `AnalyticsController` + `AnalyticsService` + `AnalyticsPage.tsx`, built on Module 2's stage history. ⚠️ The 2026-08-18 spec re-defines both Time-to-* clocks to end at *offer accepted*, so **the shipped metrics do not match the current spec** and neither is computable until Module 4 exists. Scheduled report *delivery* is unblocked — `IEmailSender` exists — but needs a `ScheduledReport` handler and attachment support, which `EmailMessage` does not have yet. |
| 6 | Planning & Budgeting | ⬜ code · ✅ spec + design | Post-MVP. Needs `Requisition → HeadcountPlan` decided first, or its headline number is uncomputable. |
| 7 | Settings & Integrations | 🚧 | 7.1 RBAC ✅ (authorization engine, roles & permissions, User Directory, Role Builder UI, permission-aware UX). 7.2 HRMS / 7.3 mail & calendar / 7.4 retention ⬜ — all four screens drawn. |
| 8 | Multi-Channel Sourcing (bots) | ⬜ code · ✅ design | First post-MVP; contracted in Mid-Tier (ADR-0014). ⚠️ May be unbuildable on-premise — see Known gaps. |
| — | **AI (Claude + Gemini)** | 🚧 **API only — no UI reaches it** | Five endpoints, all working: `parse-resume`, `match-candidate`, `executive-summary`, `document-prep`, `burmese-localization`. **None of the five is reachable from the shipped SPA.** The components that call them — `SmartMatchBreakdown`, `CandidateSlideOver`, `ExecutiveSummaryPanel` — have zero production importers, so Vite drops them from the bundle. The only AI a user can trigger is bulk CV ingestion, which runs server-side. See the row below for the measurement. |

> ⚠️ **This table was materially wrong until 2026-08-18** and is worth a note, because the
> failure mode repeats. It listed Module 5 as not started, and 2.3 / 2.4 / 2.6 as not started,
> while `AnalyticsController`, `SearchController`, `BulkResumeService` and
> `MyanmarScriptNormalizer` were all in the tree and covered by passing tests. `NEXT-SESSION.md`
> said the opposite of this file. Two status docs disagreeing with each other and with the code
> is how a session gets sent to rebuild something that already ships. **Re-derive this table
> from the code, not from the previous version of the table.**

## Delivery readiness (ADR-0004)

Per-company install, subdomain routing. **Most prerequisites now exist — one real gap left**
(verified against the code 2026-08-18, not carried forward from a previous note):

| Item | Status |
|---|---|
| Docker/Compose packaging | ✅ **verified running** — `docker-compose.yml` + `docker-compose.prod.yml` |
| Feature-flag mechanism (add-on gating) | ✅ **done** — `FeatureFlagService`, `[FeatureGate]` |
| Background job runner (bulk CV processing) | ✅ **done 2026-08-21** — `BulkResumeWorker` on ADR-0026's durable queue; see the block below for what it replaced |
| Automated EF migrations on startup | ✅ **done** — `InitialCreate` generated |
| `/api/version` + customer/version registry | ✅ **done** — `VersionController`; registry still manual |
| Support policy (latest, latest-1) | ⬜ |
| Server sizing guide | ✅ **done** — `docs/architecture/server-sizing-guide.md` |
| Backup/restore + upgrade runbooks | ✅ **done** — `docs/architecture/deployment-runbook.md` |

✅ **Bulk CV upload was rewritten onto ADR-0026 on 2026-08-21.** It is kept here as a record of
what was wrong, because the entry sat at "asynchronous ✅" for weeks while it was none of these
things.

`BulkResumeService` used to hold batches in `private static readonly ConcurrentDictionary<Guid,
BatchStateHolder> Batches` — **including the raw uploaded file bytes** — and launch
`_ = Task.Run(() => ProcessBatchAsync(batchId))`. Nothing was written to the database.

| What was wrong | What it is now |
|---|---|
| A restart **lost the batch outright**, so `GetBatchStatusAsync` returned null and the recruiter's 50 files 404'd with no way to tell whether any candidate was created | `BulkUploadBatch` + `BulkUploadFile` rows, written before the response returns. A claim only pushes a due time forward, so a process that dies mid-batch leaves work that becomes due again by itself |
| Fifty CVs of several MB each sat in RAM per concurrent upload, which the sizing guide did not account for | Bytes go to object storage at upload (ADR-0013); the row keeps a key. The same object later becomes the application's résumé — uploaded once, referenced, never copied |
| An exception inside `Task.Run` was unobserved: no handler, no retry | Retry with exponential backoff and an attempt cap of 3, plus the between-claim-and-record wrapper the 2026-08-20 security review forced onto the mail worker |
| Two replicas would not see each other's batches | The queue is a table, so they would. It is still one worker per ADR-0004 |
| The candidate lookup used `IgnoreQueryFilters()` with a hand-written `c.TenantId == …` predicate | Ordinary filtered queries. The worker enters the tenant; there is no predicate left to forget (ADR-0026 §4) |

ADR-0008 required this to be asynchronous; what shipped was the *shape* of asynchronous, not the
thing. Replaced, not extended, by
[ADR-0026](../decisions/ADR-0026-outbound-delivery-and-background-jobs.md) — leaving it would have
meant two job mechanisms, which is the outcome the ADR exists to prevent.

## Built in detail

### ✅ Department administration
- `GET /api/departments` — active departments the caller may raise work in (scoped for
  Hiring Managers). **This feeds the new-requisition picker**; it used to require
  `RecruitmentStaff`, which excluded Hiring Managers and left their dropdown permanently
  empty — a bug the test suite was asserting as correct
- `GET /admin`, `POST`, `PUT /{id}`, `POST /{id}/deactivate`, `POST /{id}/activate`,
  `GET/PUT /{id}/members` — all **Admin only**
- **No delete** — requisitions, postings and the audit trail reference departments;
  deactivation stops new work and keeps the history
- **Deactivation is refused while requisitions are in flight**, since they would be stranded
  with no route forward
- **Membership is replaced as a whole list**, unknown user ids are a 409 rather than a
  silent skip, and existing rows survive so `CreatedAt` still records when access was granted
- Inactive departments are refused by the API, not merely hidden from the picker

### ✅ Department scoping (ADR-0003)
- `ICurrentUser` (claims) + `IDepartmentAccess` (DB lookup, per-request cached)
- **DB lookup, not JWT claims** — revoking access takes effect immediately rather than
  after the 8-hour token expires. Stale access control is a security problem.
- Applied as an **explicit predicate** in `RequisitionService`, not an EF query filter
- Out-of-scope rows return **404, not 403**, so existence isn't leaked

### ✅ Module 1 — Requisition & Approval (API + UI complete)
- Entities: `Requisition`, `RequisitionApproval`, `ApprovalChain`, `ApprovalChainStep`, `JdTemplate`
- Flow: create Draft → submit (snapshots the chain into per-requisition approval steps)
  → sequential approve/reject → Approved/Rejected, **or cancel** from Draft/PendingApproval
- Chain is **snapshotted on submit** so later edits to the chain never rewrite decisions
  already recorded — the audit trail stays truthful
- `GET/POST /api/requisitions`, `GET /inbox`, `GET /{id}`, `PUT /{id}`,
  `POST /{id}/submit`, `POST /{id}/decision`, `POST /{id}/cancel`
- **Authority is permission-driven, not a role literal (ADR-0022, 2026-08-15).** Every action
  above carries a `[HasPermission("permission:requisitions:requisitions:*")]` attribute
  resolved against the caller's actual role (system or custom) via the Role Builder — not
  `RequireRole` against the fixed five-value enum. See the mapping and behaviour changes in
  ADR-0022. Department scoping (ADR-0003) remains the security-critical, per-resource filter;
  the permission is a coarse gate in front of it.
- `GET/POST /api/approvalchains` (**`settings:read`/`settings:update`, ADR-0022** — was
  Admin-only by role literal; HrDirector now reads chains too, matching the nav item it was
  already shown, and any role can be granted chain-authoring through the Role Builder),
  `GET /{id}`
- `GET/POST /api/jdtemplates` (read: any internal user; create: recruitment staff)
- `GET /api/users` (**Admin only**) — populates the approver picker in the chain builder
- Approvers are validated on chain creation, so a requisition can never be submitted into
  a chain nobody can action
- Step `Sequence` is derived from list order, making gaps and duplicates impossible
- **Inbox** returns only requisitions whose *lowest-sequence* Waiting step belongs to the
  caller **and** whose status is still `PendingApproval` — so approvers can't work the queue
  out of order, and a cancelled requisition doesn't linger in it
- **Cancel** is permitted to the requester or a company-wide role only (not approvers);
  out-of-scope callers get 404, decided requisitions get 409. Approval steps are left
  `Waiting` on purpose — "cancelled while waiting on Finance" is a fact worth keeping
- **Edit** (`PUT`) uses the same authority rule (shared `IsOwnerOrCompanyWide` helper) and is
  **Draft-only** — 409 after submit, so approvers never decide on a moving target. Moving a
  Draft between departments requires access to **both** ends
- **UI (`frontend/internal`):** requisitions list, detail (job description + approval
  timeline + submit/approve/reject/cancel), one `RequisitionFormPage` serving both create and
  edit with JD-template prefill, approver inbox, approval-chain builder, JD templates,
  departments — with a role-aware sidebar

### 🚧 Module 2 — ATS & Sourcing (slice 1: requisition → public page → pipeline)

✅ Migration `20260728023109_Module2Ats` generated (applies on startup).

- Entities filled in: `JobPosting`, `PortalLink`, `Candidate`, `JobApplication`, plus new
  `ApplicationStageHistory`. New enum `EmploymentType`.
- `GET/POST /api/jobpostings`, `GET/PUT /{id}`, `POST /{id}/publish`, `POST /{id}/close`,
  `GET /{id}/pipeline`
- `POST /api/applications/{id}/stage`, `GET /{id}/history`
- **Anonymous:** `GET /api/public/jobs/{token}`, `POST /{token}/apply`
- **A posting requires an Approved requisition, one per requisition** — enforced in the
  service *and* by a unique index, so the guarantee survives future code paths
- **Salary is private by default**; `PublicJobDto` is a deliberately narrower type than the
  internal DTO so internal fields cannot drift onto a public page
- **The anonymous path has no tenant claim.** `PublicJobService` reads with
  `IgnoreQueryFilters()` and re-applies the tenant from the token's own row; every write
  sets `TenantId` explicitly because the auto-stamp would fill in `Guid.Empty`
- Unknown / revoked / expired / unpublished tokens are **one indistinguishable 404**
- Rate limited (`RateLimit:PublicApply`) — the only anonymous endpoint that writes
- **Duplicate detection** via `Domain/ContactNormalizer` (shared so future CV import
  produces identical keys); matches reuse the candidate and fill blanks without overwriting
- **`ApplicationStageHistory` is written from the first moment**, anonymous arrival included.
  Hired/Rejected are terminal — reopening would silently corrupt Module 5's figures
- **Custom application fields (2.2)** — `Domain/ApplicationFormSchema` validates the schema
  when a recruiter saves it *and* the answers when a stranger submits them, and **rebuilds**
  the answer document from the schema rather than storing what was sent. Unknown keys are
  dropped (a stale tab shouldn't cost someone their submission); required answers, `select`
  choices, numbers, dates and required checkboxes are all enforced server-side
- **UI:** postings list (with "approved and waiting to be advertised"), posting detail with
  advert editor + form-field builder + publish/close + pipeline board showing custom answers;
  public `/jobs/[token]` renders the real job with Open Graph metadata and a form that
  includes the customer-defined questions

### 🚧 Module 3 — Interview & Assessment (API complete, no UI)

Decisions: [ADR-0017](../decisions/ADR-0017-interview-and-assessment.md).
✅ Migration `20260728061832_Module3Interviews` generated (applies on startup) — 8 tables,
6 unique indexes. `ScorecardResponses` deliberately carries **only** the `Scorecards` FK.

- Entities: `Interview`, `InterviewParticipant`, `ScorecardTemplate`, `ScorecardCriterion`,
  `Scorecard`, `ScorecardResponse`, `Note`, `NoteMention`. New enums `InterviewMode`,
  `InterviewStatus`, `CriterionType`, `HireRecommendation`, `ScorecardStatus`.
- `POST/GET /api/applications/{id}/interviews`, `GET/PUT /api/interviews/{id}`,
  `PUT /{id}/panel`, `POST /{id}/cancel`, `POST /{id}/complete`
- `GET/PUT /api/interviews/{id}/scorecard`, `POST /{id}/scorecard/submit`, `GET /{id}/scorecards`
- `GET/POST /api/scorecardtemplates`, `GET/PUT /{id}`, `GET /resolve/{jobPostingId}`
- `GET/POST /api/applications/{id}/notes`
- **`IApplicationAccess` is new and load-bearing.** One implementation of "department reach
  (ADR-0003) **or** panel participation (ADR-0017 §4)", called by interviews, scorecards and
  notes. The rule is not re-derived per service, because a guard reaching two of three
  sibling methods is this repo's recurring bug.
- **Scheduling moves the stage and writes `ApplicationStageHistory` in one
  `SaveChangesAsync`** — an interview cannot exist against an application still at
  `Screening`. A second round writes no history row (a no-op transition would be counted).
- **Participation is a read grant, scoped to one application.** No department access, no
  sibling application, no writes — rescheduling and stage moves stay with recruitment staff.
- **Blind scoring** is enforced in `ScorecardService`, keyed on participation rather than
  reach; drafts are visible to their author alone; submitting is irreversible.
- **Criteria are snapshotted onto each `ScorecardResponse`**, so template edits cannot
  retroactively change what an interviewer was asked. `ScorecardResponse` deliberately has
  **no FK** to `ScorecardCriterion` for this reason.
- **One active template per scope** is enforced, or resolution becomes "whichever row came
  back first".
- **Notes** store raw and escape on output (`BodyHtml`); mentions are parsed server-side and
  only resolve for users who could reach the application anyway.

**UI (`frontend/internal`)** — added 2026-07-28, first run against a live API 2026-07-29:

- `components/ApplicationDebrief.tsx` — expands from a pipeline row: interview rounds
  (schedule / reschedule / panel / complete / no-show / cancel) plus the note thread. One
  component because the thread's round picker needs the rounds
- `pages/InterviewDetailPage.tsx` (`/interviews/:id`) — panel roster, the caller's own
  scorecard, the panel's evaluations, and the round's notes. **Not** nested under a posting:
  a cross-department panel member reaches this round and nothing around it
- `components/ApplicationNotes.tsx` — renders `bodyHtml` via `dangerouslySetInnerHTML`, which
  is correct here and is why the field exists; `.mention` is styled in `index.css` rather than
  with a utility class, because the markup comes from C# and Tailwind's scanner cannot see it
- `pages/ScorecardTemplatesPage.tsx` — criteria builder with ordering (sequence is list order,
  as in the approval-chain builder); scope is one choice yielding one of two mutually exclusive
  ids
- `lib/auth.ts` — the client mirror of `Domain/RoleScope.cs`. `isDepartmentScoped`,
  `isExcludedFromCandidateData`, `isRecruitmentStaff`, `canApprove`, `isAdmin`. `AppLayout`'s
  three local role lists were folded into it
- ✅ **New endpoint `GET /api/users/selectable`** ([ADR-0019](../decisions/ADR-0019-panel-picker-directory.md)) —
  `RecruitmentStaff`, returns id/name/role without email. Without it a Recruiter cannot name
  the panel the scheduling API requires. **11 tests** (`UserDirectoryTests.cs`), all passing in
  CI run #5, and the authorization change has been **human-reviewed** (2026-07-29)

### ✅ Foundation
- .NET 8 Clean Architecture solution: `Domain` / `Application` / `Infrastructure` / `Api` + 2 test projects
- Next.js App Router frontend; Tailwind theme encoding the full "Clear Pipeline" token set
- `docs/` knowledge base (this)

### ✅ Multi-tenancy (Module 7 partial)
- `ITenantScoped` marker + global query filters on all tenant-owned entities in `AppDbContext`
- `CurrentTenant` resolves `tenant_id` from the JWT principal
- **Tested:** tenant A sees only its rows; B can't see A's; 401 unauthenticated; 403 wrong role

### ✅ Authentication & RBAC (Module 7 partial)
- JWT bearer with full validation (issuer, audience, lifetime, signing key)
- `POST /api/auth/login` → `AuthService` + `JwtTokenService` (HS256, 8h)
- Password hashing via framework `IPasswordHasher<User>`
- Policies `AgencyStaff` / `AdminOnly`; **`FallbackPolicy` = authenticated by default**
- Dev-only `DbInitializer` seeds one tenant + admin, gated on config-supplied credentials
- **Tested:** valid login, wrong password → 401, unknown email → 401 (no enumeration), token claims
- ⚠️ **Role set is agency-flavoured — must be revised** (see auth doc)

### ✅ Frontend restructured (ADR-0012)
- **npm workspaces** at the repo root; `packages/ui` (Tailwind preset + StatusPill/Button/Card)
  and `packages/types` (mirrors backend DTOs) are consumed by both apps — the anti-drift
  mechanism ADR-0012 called for
- **`frontend/internal`** — Vite + React SPA: login, requisitions list, requisition detail
  (submit / approve / reject), new-requisition form with JD-template prefill, departments
- **`frontend/public`** — Next.js SSR, now serving `/jobs/[token]` with Open Graph metadata
  (the reason this app is SSR at all); the old `/portal/[token]` name is gone with the pivot
- Verified with `tsc --noEmit`: **no type errors**

### ✅ Frontend foundation (superseded by the split above)
- Server-aware API client (absolute URL server-side, rewrite path in browser)
- Route error boundary (no more blank pages on server-render failure)
- `StatusPill` (fixed vocabulary — **vocabulary changing**, see data-model.md)
- **Tested:** 9 Vitest tests passing (contract helpers + api module)

### ✅ Agency → in-house migration (Steps 1–4)
- **17 agency files deleted** (`Client`, `Contract`, `ClientTier`, `ContractStatus`,
  `ClientFeedback`, `ContractStatusCalculator`, `ClientService`, controllers, frontend
  clients table, `TierBadge`)
- **Renamed:** `Tenant`→`Company`, `Job`→`JobPosting` (now department-owned, links to a
  future `Requisition`), policy `AgencyStaff`→`RecruitmentStaff`
- **New enums:** `UserRole` = Admin/HrDirector/Recruiter/HiringManager/Approver (no
  `Client` role); `PipelineStatus` = Sourced/Applied/Screening/Shortlisted/Interview/
  Offer/Hired/Rejected (`SentToClient` gone, `Placed`→`Hired`)
- **New entities:** `Department`, `UserDepartment` (many-to-many per ADR-0003)
- **`PortalLink` repurposed** to the public applicant job page (was client CV review)
- **New slice:** `DepartmentsController` + `IDepartmentService` + `DepartmentService`
- **Framework:** all 6 projects moved to **`net10.0`** with matching package versions
- **Tests rewritten:** isolation tests now exercise `Department`; role/vocabulary guards updated

## Test inventory

| Suite | Count | Run? |
|---|---|---|
| `RecruitOps.Domain.Tests` | **39 cases** — 24 existing (2 vocabulary/role set, 22 application-form schema) + **15 mention parser** (7 `[Fact]`, 2 `[Theory]` with 8 rows between them) | ✅ **39/39** — the first backend figure in this repo that is not counted from source |
| `RecruitOps.Api.Tests` | **130** — 119 existing (isolation, department admin, login + throttle, token, scoping, approval flow, posting flow, public application, 42 Module 3, 7 ADR-0018 approver reach) + **11 ADR-0019 user directory** (10 `[Fact]`, 1 `[Theory]` with 2 rows) | ✅ **130/130** since the additive-`[Authorize]` fix. Run #4's 8 failures were the ADR-0019 cases failing **correctly** — see below |
| `frontend/internal` (Vitest) | **27** — 14 `lib/scorecard` (payload rules), 7 `InterviewDetailPage` (blind rule + draft payload), 6 `ApplicationNotes` (`bodyHtml` injection, mentions, thread filter) | ✅ **27/27 passing** (2026-07-28, node 22 / vitest 2.1.9) |

**169 backend green** (39 domain + 130 API), up from 92 · **27 frontend**, up from 0. The backend
figure is no longer counted from source — and the source count was wrong: the "existing" Api
suite was 119, not the 117 this table claimed for three sessions.

> **Why the green build is the evidence, not the summary step.** One `RUN` per test project plus
> `RunConfiguration.TreatNoTestsAsError=true` means a zero-test project exits non-zero and a
> failing case exits non-zero, per project. So `docker build --target test` cannot go green
> unless both projects ran and every case passed. The `Test counts` step is *reporting only*;
> when it cannot lift the numbers out of a truncated BuildKit log it now says so rather than
> adjudicating. **A count it cannot read is not a count of zero** — that mistake was made three
> times in one day (empty box, confident `21` against a runner-reported `122`, red tick over a
> green suite) and the fix was to take the vote away from it.

### ✅ Closed: the ADR-0019 tests failed on their first run, and found a real authorization bug

`GET /api/users/selectable` was **unreachable by the role it was written for**. `UsersController`
carried `[Authorize(Policy = AdminOnly)]` at the class level and `[Authorize(Policy =
RecruitmentStaff)]` on the action, intending to opt *down*. **ASP.NET Core authorization
attributes are additive** — an action-level attribute does not replace a class-level one, it is
evaluated *in addition* to it. The effective requirement was `AdminOnly` **AND**
`RecruitmentStaff`, so only an Admin could call it and a **Recruiter got 403**.

That is the exact condition ADR-0019 exists to remove: a Recruiter who cannot list users cannot
name a panel, and the panel is required and non-empty, so **Module 3 scheduling was undrivable
by the role it was opened to — twice, for the same underlying reason**. The first time it was a
missing endpoint; the second time the endpoint existed and was walled off.

The failure count is itself the evidence: **8 of the 11 new cases failed, and it is exactly the
8 that need a 200 from `selectable`.** The three that passed are the ones asserting a *refusal*
(HiringManager 403, Approver 403, unauthenticated 401) and the Admin case — all of which a
too-strict policy satisfies by accident. A suite that only tested "the right people are kept
out" would have been green over this.

**Fixed** 2026-07-28: the class-level policy is now a bare `[Authorize]` and `AdminOnly` is
declared on `Get` itself. ✅ **CI run #5's `docker build --target test` passed** — and with one
`RUN` per project plus `RunConfiguration.TreatNoTestsAsError`, a green build is only possible if
both projects ran and every case passed. **169/169.**

> Run #5's job *summary* said the opposite — "No tests executed for: RecruitOps.Api.Tests" — and
> was wrong. BuildKit truncates a step's log at 1MiB and drops the **end**, which is where the
> summary lives, so the reporting step lost the Api counts and adjudicated on their absence.
> **An instrument that contradicts the thing it measures is worse than no instrument.** Fixed
> twice over: the log limit is lifted (`BUILDKIT_STEP_LOG_MAX_SIZE: -1`) and the step no longer
> adjudicates at all — the build's exit code is the authority, and a missing count is now
> reported as a missing count.

✅ **Human review done 2026-07-29.** ADR-0019 is an authorization change, and CLAUDE.md requires
explicit human sign-off on those. The diff reviewed: bare `[Authorize]` on `UsersController`,
`AdminOnly` declared on `Get`, `RecruitmentStaff` on `selectable`. This item is closed.

🔧 **Still open, and it is the reporting step only:** the `Test counts` job summary remains
unreliable at lifting per-assembly numbers out of the BuildKit log. It is cosmetic — the suite is
green and the build's exit code proves it — but it is the last thing in this pipeline that can
print a sentence nobody should believe. Fix it or delete it; a half-trusted instrument is the
worst of the three options.

### What the ADR-0019 suite pins (`UserDirectoryTests.cs`, 2026-07-28)

Two endpoints on one controller under two policies — the shape most likely to be "simplified"
later by someone reading the class-level `AdminOnly` and the method-level `RecruitmentStaff` as
a contradiction. It is not one: **the wider audience gets the narrower payload.**

- **Both halves of the boundary are asserted in one case.** A Recruiter gets 200 on
  `/api/users/selectable` *and* 403 on `/api/users`. Split across two tests, a future edit that
  widened `Get` would leave a green test named "a recruiter can read selectable" standing over
  the hole. HrDirector is asserted separately — `RecruitmentStaff` is three roles, and two of
  three is this repo's recurring bug.
- **No email crosses the wire**, asserted against the **raw JSON**, not a deserialised
  `SelectableUserDto`. Reading into the DTO would drop an email property silently and report
  green, and what crosses the wire is the entire argument of ADR-0019.
- **An Approver is on the list, deliberately.** ADR-0018 removed their *standing* reach into
  candidate data and reads at a glance like "keep them off the panel". It says the opposite —
  panel membership is how an excluded role reaches one application on purpose (ADR-0017 §4).
  Filtering them out in a controller would delete that escape hatch far from the ADR granting it.
- **The picker is not department-scoped** — both hiring managers appear, so a Finance
  interviewer on a Sales hire stays buildable from the UI.
- **`Role` survives as a string**, which is the in-memory projection holding. Fold it back into
  the query and the endpoint throws against Postgres; this assertion is what stands between
  that and an install.
- Admin still reads both (the approval-chain builder was not broken), HiringManager and
  Approver get 403 on both, an unauthenticated caller gets 401, and the tenant query filter
  still empties the list for another tenant.

✅ **Verified by CI**, and its first run is what found the additive-`[Authorize]` bug above.

What the frontend suite proves — all three are cases that fail *quietly*, which is why they
were chosen first:
- **The blind rule has three renderings and each is pinned by what the page says.**
  `hiddenCount > 0` names what is withheld; `hiddenCount === 0` says nobody has submitted yet
  (the banner counts something, and "0 evaluations are waiting for yours" is not a sentence);
  not blinded shows the scores with no notice at all. A non-participant's 404 renders as an
  ordinary state with **no form** — offering one would be a button that can only fail
- **The scorecard payload omits untouched criteria** rather than sending nulls, `false` is a
  real `YesNo` answer, and a comment written against an answer never given is dropped with it.
  A truthiness check on either of the first two is invisible until drafts stop saving
- **`bodyHtml` is injected, not re-escaped**, the `span.mention` element that `index.css`
  styles survives, Burmese text passes through the round trip, and only server-resolved
  mentions are listed (ADR-0018)
- **`missingRequired` and `toAnswers` agree** — the completeness check and the payload filter
  are the same question asked twice, and they agreed only by construction until this test

> **The harness was proved to fail first.** Three deliberate mutations (truthy `YesNo`, blind
> banner ignoring `hiddenCount`, `NoteBody` escaping twice) produced 5 failures across all
> three files; `tsc --noEmit` was checked the same way. A green run from a checker nobody has
> seen fail is worse than no run.

`CustomWebAppFactory` gained a second hiring manager (Finance, owning only the Finance
department). Without a manager who legitimately *cannot* see the Sales pipeline, "participation
is what granted the access" is unprovable — several Module 3 assertions rest on that fixture.

What the Module 3 suite proves:
- **Scheduling and the stage move are one transaction** — an interview cannot exist against an
  application still at `Applied`, and a *second* round writes no extra history row (a no-op
  transition would be counted by Module 5 as a real stage change)
- **Participation grants access, and grants it narrowly** — the Finance manager gets 404 on a
  Sales application, 200 once on its panel, and 404 again on the *neighbouring* Sales
  application; being on one panel is not being in the department
- **Blind scoring holds over HTTP** — a panel member sees `hiddenCount: 1` and nothing else
  until they submit, then sees both evaluations including the one that disagrees with theirs;
  a recruiter who is not on the panel sees submitted scores immediately; a draft is visible to
  its author alone, even to a company-wide role
- **Submitting is irreversible**, incomplete submissions are refused, and drafts may be partial
- **Someone who was not in the room cannot write a scorecard** even though they can read the
  interview, and an interviewer who has started one cannot be dropped from the panel
- **Answers are rebuilt from the template** — a criterion id that isn't on it is dropped, the
  same defence `ApplicationFormSchema` applies to anonymous applicants
- **Criteria resolution is most-specific-wins**, and one active template per scope is enforced
- **Notes escape on output** — `<script>` survives in `body` and appears only as `&lt;script&gt;`
  in `bodyHtml`; Burmese text passes through untouched
- **A mention only resolves for someone who could reach the application anyway**, so tagging a
  Finance manager on a Sales candidate records nothing — until they join the panel

**169/169 green** in CI. The historical 92/92 figure below refers to the pre-Module-3 suite and
is kept only because the bullets that follow describe what *that* suite proved.

> Counting note: `docker build --target test` hides test output behind BuildKit's progress
> collapsing, so "the build passed" is not by itself evidence the *new* tests ran — a cached
> `COPY . .` layer would happily re-report an old count. To see the numbers:
> `docker build --target test --progress=plain --no-cache-filter=build,test ./backend`.

What the suite proves:
- **Department scoping works** — a Hiring Manager sees only their own department, gets 404
  (not 403) for another's requisition, and cannot create in a department they don't own
- **Department administration holds** — only an Admin can create, rename, deactivate or
  reactivate a department or set its membership; a Hiring Manager *can* now read
  `GET /api/departments` (the bug the old suite defended); deactivation is refused while
  requisitions are in flight; an unknown user id in a membership PUT is a 409, not a
  silent skip; inactive departments are refused by the API, not merely hidden
- Tenant isolation works end-to-end — tenant A sees only its rows, B cannot see A's (ADR-0004)
- RBAC works — unauthenticated → 401, `HiringManager` on a cross-department endpoint → 403 (ADR-0003)
- Login works — valid credentials return a token; wrong password and unknown email both → 401 (no user enumeration)
- JWT carries `tenant_id`, role and `sub`, and refuses to sign without a key (ADR-0002)
- .NET 10 (ADR-0010) and the container build (ADR-0015) both work
- The approval chain runs in sequence, a rejection at step 1 leaves step 2 `Waiting` rather
  than auto-deciding it, and cancelling clears the requisition out of the approver's inbox
- A **later approver outranks an earlier one** (ADR-0024): they can approve every waiting step
  at or below their own in one action, and the closed steps record who really decided them.
  They cannot reject on a junior's behalf, and an earlier approver still cannot reach a later
  step — the rule reaches down the chain only
- A **rejected requisition can be revised and resubmitted** (ADR-0023): it returns to `Draft`
  for its requester, and resubmitting opens a new round beside the rejected one rather than
  over it, so the rejection and its comment stay readable. `Approved` and `Cancelled` remain
  terminal. An approver dropped from the chain between rounds loses the inbox item; a
  superseded round's leftover `Waiting` rows never resurface as work
- A Draft is editable by its requester but frozen after submit, and cannot be moved into a
  department the caller can't reach; an Approver can neither submit nor edit nor cancel
  someone else's requisition, and cannot learn its status by probing `/decision`
- Nothing is advertised without an approval behind it — an unapproved requisition cannot
  become a posting, one requisition cannot be advertised twice, and a Hiring Manager cannot
  create a posting at all
- The public page never leaks salary unless opted in, treats unknown and unpublished tokens
  identically, recognises the same person applying twice with a differently-formatted phone
  number, and records the application's arrival in the stage history
- Custom application fields cannot be used to write arbitrary JSON: a key that isn't in the
  posting's schema is dropped, a required answer is enforced, a `select` value outside the
  offered list is refused, and a broken schema is caught when the recruiter saves it
- Login is brute-force resistant — 5 failures lock an account for 15 minutes, unknown emails
  lock out identically (no enumeration oracle), and one locked account doesn't affect others

**All six projects compile and the full suite runs** under `docker build --target test ./backend`
(.NET 10). The one historical compile error worth remembering: `CS0118` — the `Application`
entity collided with the `RecruitOps.Application` namespace, fixed by renaming it to
**`JobApplication`**. Static verification had *not* caught that; only a compiler finds
namespace/type collisions, which is why "it looks consistent" is never sufficient here.

## Known gaps & risks

| Issue | Severity | Where |
|---|---|---|
| **Design-system tint pills fail WCAG AA** | ✅ **fixed 2026-08-17** | `-700` text-on-tint steps added to `packages/ui/tailwind-preset.js` (success `#146B43`, warning/accent `#8A5A08`, danger `#A63423`, info `#22528F`); `StatusPill` moved onto them and off `ink-400`. Pinned by 6 new cases in `signatureComponents.test.tsx`, **proved to fail first** by mutating two style entries |
| **Design system doc never pivoted (agency-era)** | ✅ **fixed 2026-08-17** | `RecruitOps_Design_System.md` rewritten for the in-house product. `ClientPortalCard`, `ClientFeedbackBar` and `ExpiryAttentionCard` deleted with their exports; `ExtendedStatusVocabulary` removed from `StatusPill`; `PipelineStageRail` defaults corrected to the real funnel |
| **No email sender and no job scheduler — now blocking 3 modules** | 🟠 High | Neither exists in the codebase. Already blocked Module 3 interview invitations; the 2026-08-18 scope revision adds Module 4 (`Remind Candidate`, `Send to Candidate`, IT/Admin handoff notifications) and Module 5 (`Schedule Email` for recurring reports). This is one shared capability, not four features — decide the provider and the job runner once, before any of the three modules starts |
| **Module 4 & 5 scope revised, specs ahead of code** | 🟡 Medium | Rewritten 2026-08-18 from sales requirement PDFs. Module 4 gains a second approval chain, an `OfferStatus` enum whose `Rejected` collides with `PipelineStatus.Rejected`, and HRMS API sync (QHRM/BetterHR/GlobalTA/CityHR — treat as ADR-0007 extension point, not core). Module 5 re-defines both Time-to-* clocks to end at *offer accepted*, so **neither metric is computable until Module 4 ships**, and adds a Recruiter Leaderboard that ranks named staff. See the module docs for the open questions |
| **Two token systems are now written down** | 🟡 Medium | A "RecruitOps V1.0" spec (slate `#0F172A` / emerald `#10B981` / Plus Jakarta Sans) was supplied 2026-08-17 and captured as [ADR-0025](../decisions/ADR-0025-token-system-v1-proposal.md), **Proposed, not adopted — no code implements it**. It conflicts with the shipped Clear Pipeline preset on ink, primary, attention colour, alert, border, display face, radius and elevation, and it drops the amber reservation. Adopt it, reconcile it, or mark it Rejected — leaving two systems written down is the exact condition that produced the agency-era design-doc rot |
| **The threshold-based extra approver is drawn but not modelled** | 🟠 High | Three design screens (`requisition-detail`, `requisition-new`, `offer-create`) show an approver *"added by threshold rule"*. `ApprovalChain` stores `Name`, `DepartmentId?` and `IsActive` — **no condition, no amount, no operator**, and `grep -i threshold` finds nothing in Domain, Application or Module 1's doc. Either the entity gains condition fields or those three screens are describing a feature that does not exist. Surfaced on `design/internal/settings-org.html`, where an admin would look for the switch |
| **An approval step names a person, not a role** | 🟡 Medium | `ApprovalChainStep.ApproverUserId` is a user id. Disabling a user on the Users screen therefore stalls every requisition waiting at their step, indefinitely and silently — nothing in the data links the two actions. The disable flow needs to name the chains it breaks before the admin confirms. Drawn as the "broken chain" state on `design/internal/settings-org.html` |
| **Module 8 may be unbuildable on-premise** | 🟠 High | Viber, Telegram and Facebook deliver by webhook; an on-prem install behind a corporate firewall has no publicly reachable endpoint. The module doc records this; nothing decides it. Three exits — publish an endpoint through the DMZ, sell sourcing channels as hosted-tier only, or outbound polling (Telegram supports it, Facebook does not). This is the module positioned as the **primary competitive differentiator**, so the answer is commercial as much as technical. `design/internal/channels.html` is drawn blocked-first for this reason |
| **Module 6 needs `Requisition → HeadcountPlan` decided first** | 🟡 Medium | "Are we over headcount?" is the question Module 6 exists to answer and it is uncomputable without that relationship, which the module doc lists as open. Without it the headcount table is hand-typed and stale within a week. Visible on `design/internal/planning-budget.html` — Credit Risk at −2 against plan is the case the screen is built around |
| **Age/gender filtering is unconfirmed for this market** | 🟡 Medium | Module 2.6 lists filtering by age and gender with data-protection implications not yet confirmed as lawful or intended. `design/internal/talent-pool.html` holds both behind a click with the question attached rather than placing them in the filter row. Resolve to: keep, gate behind a permission, or drop |
| **99.9% SLA is claimed publicly but recorded nowhere** | 🟡 Medium | `marketing/landing.html` states a 99.9% Enterprise uptime SLA on the product owner's authorisation (2026-08-17). No ADR, no commercial terms, and ADR-0004's operational prerequisites (sizing guide, runbooks, `/api/version`) are still open. Write the ADR before the page goes live |
| `System.Security.Cryptography.Xml` CVE-2026-33116 | ✅ **fixed** | Pinned to 10.0.6 in Infrastructure + Api.Tests (10.0.0–10.0.5 are also vulnerable) |
| **The whole client-side AI surface is orphaned — it builds, it is tested, it never loads** | 🟠 High | Measured 2026-08-29 against the **running container**, not the import graph: `grep -F` a UI string in the bytes nginx serves, validated first on strings known to ship (`Reporting & Analytics`, `Hide interviews`, `Sign out` — all found), so absence means absence rather than minification. Absent: `AI Smart Match Analysis`, `Analyze Fit`, `Generate AI Summary`, and all five endpoint paths. Present: `resumes/bulk`. `features/pipeline/` is **mixed, not orphaned wholesale** — `BulkCvUploadModal` ships (imported by `JobPostingDetailPage`); `CandidateSlideOver`, `SmartMatchBreakdown`, `ExecutiveSummaryPanel`, `PipelineKanbanBoard` and `usePipeline` do not. **84 of 456 frontend tests (18%) exercise components the browser never loads** — that is the number to decide against, because the cost of parking is not the dead files, it is that a fifth of the suite reports on software nobody runs. ⚠️ This also re-frames the three contract fixes below: each was a real defect, and **no user was ever shown the wrong Smart Match badge**, because no route renders it. Parked by the product owner 2026-08-25 — ask before acting |
| **The kit draws a Kanban pipeline; the app ships a table** | 🟡 Medium | `design/internal/board.html` is titled "Pipeline board" and module doc 2.5 specifies "Kanban board or list view" plus a 360° candidate view. `PipelineKanbanBoard.tsx` and `CandidateSlideOver.tsx` both exist and are both orphaned; what ships is `JobPostingDetailPage`'s row list with a stage `<select>`. Decide which one is the product before either is restyled again |
| **All three AI response contracts in `packages/types` were fiction** | ✅ **closed 2026-08-28** | `generateExecutiveSummary`, `prepareDocument` and `matchCandidate` each declared a response the API has never sent; all three now verified against the running service's OpenAPI *and* a live `200`. The last one had UI on it, so it did not render blanks — it rendered a red "Low Match" badge and "No critical gaps identified." over an 88-point Strong Fit whose concerns the model had actually listed. **The cause was a testing habit, not three separate slips: every fixture mocked the response in the shape the caller wanted**, so mock and interface agreed and neither was ever compared to the API. `SmartMatchLiveContract.test.tsx` now pins a verbatim live capture, typed rather than cast, so the next drift fails to compile |
| **A shared-type name can collide while the type disagrees** | 🟡 Medium | The Smart Match badge was wrong for a subtler reason than the blank panels: `recommendation` existed on *both* sides, so nothing looked missing — the API sends a sentence, the SPA typed it as a 4-member enum and `switch`ed on it, and every real value fell to `default:`. Rule adopted: **free model text may be displayed, never decided on.** Anywhere a `switch` runs over a value the backend types as `string`, the default arm is a bug waiting for its first real input |



| Module 3 never compiled; no migration generated | ✅ **fixed** | `20260728061832_Module3Interviews` generated; suite compiles and passes |
| Module 3 authorization surfaces not security-reviewed | ✅ **done** | 2026-07-28 — [`SECURITY-REVIEW-MODULE-3.md`](SECURITY-REVIEW-MODULE-3.md). Two High findings, both fixed (ADR-0018); blind filter cleared |
| The ADR-0018 fix was unbuilt | ✅ **fixed** | CI green; 169/169 |
| `GET /api/users/selectable` needed human review | ✅ **closed 2026-07-29** | ADR-0019. 11 cases (`UserDirectoryTests.cs`) green, and the authorization diff has been read by a human per CLAUDE.md |
| CI `Test counts` summary step is unreliable | 🟢 Low (cosmetic) | It reports; it does not adjudicate, so it cannot fail a green suite any more. But it can still print a number nobody should trust. Fix or delete — see the inventory note |
| `GET /api/users` projects `enum.ToString()` inside the query | 🟡 Medium | EF Core 10 does not translate it. The stack now runs against real Postgres, so this is **cheap to settle**: open the approval-chain builder as an Admin. Two-step pattern if it throws (ADR-0019 follow-up) |
| Module 3 UI has run, but 3 behaviours are un-eyeballed | 🟡 Medium | Stack came up 2026-07-29. Still unseen: (a) the **panel picker populated as a Recruiter** — ADR-0019's entire reason to exist, and it has never been observed working; (b) the **blind state** on `/interviews/:id` with two panel members; (c) **`.mention` styling surviving the Tailwind build** (the markup comes from C#, so the content scanner cannot see the class) |
| Mention resolution loads every active user, then N+1s | 🔵 Low (perf) | `NoteService.ResolveMentionsAsync` — full user scan per note POST, plus 2–3 queries per matched handle. Also `InterviewService.ListForApplicationAsync` (4 queries per round) |
| Role set is too coarse | 🟡 Medium | ADR-0018 needed a second axis because `Approver` fits neither "scoped" nor "sees everything". A third role reading candidate data will force the same question again |
| Module 3 has no frontend | ✅ **fixed** | Five screens built 2026-07-28, first run 2026-07-29 |
| No feature gating by tier (Mid vs Enterprise) | 🟡 Medium | Needed before first Enterprise deal (ADR-0011) |
| No object-storage abstraction (R2 vs on-prem MinIO) | 🟡 Medium | ADR-0013 |
| No EF migration exists; DB schema never created | ✅ **fixed** | `InitialCreate` applies on startup |
| No rate limiting / lockout on login | ✅ **fixed** | ADR-0016 — per-IP limiter + per-account throttle |
| `LoginThrottle` counters are in-process | 🟡 Medium | Fine for one instance per company (ADR-0004). **Must move to Redis/DB before running two replicas for one customer**, or the effective limit becomes N × the configured value |
| Compose publishes the API port, so `TrustForwardedHeaders` is off | 🟡 Medium | The per-IP limiter sees nginx, not the client. Production installs must drop the port mapping and enable the flag (ADR-0016) |
| No refresh token | 🟡 Medium | Auth |
| No edit/update on a Draft requisition | ✅ **fixed** | `PUT /api/requisitions/{id}`, Draft-only |
| Frontend has **no tests at all** | ✅ **fixed** | Vitest wired into `frontend/internal`; 27 tests over Module 3's blind rule, scorecard payload rules and note rendering. Modules 1–2 screens are still untested |
| **No CI** — nothing compiled the backend for three sessions | ✅ **fixed** | `github.com/minarkarsoe/RecruitOps`, first run green on both jobs 2026-07-28. Actions pinned to the Node 24 runtime (checkout@v5, setup-node@v5, setup-buildx@v4); app built on Node 22 |
| Departments are read-only (no create/edit) | ✅ **fixed** | Full admin CRUD + membership assignment |
| Email must be globally unique across tenants | 🟡 Medium | `AuthService` |
| `HiringManager` department-scoping undecided | 🟡 Medium | Needs ADR before Module 1 |
| Git unusable from the sandbox mount (lock files) | 🟢 Low | Environment |
| Department scoping | ✅ **implemented + tested** | Explicit predicate per ADR-0003; 6 scoping tests |


| No `package-lock.json` at the workspace root | 🟢 Low | A root `package-lock.json` now exists (2026-07-28). Remaining work: switch the frontend images to `npm ci` |
| Frontend images never built; only type-checked | ✅ **fixed** | `docker compose up --build` builds and serves both apps (2026-07-29) |
| No feature-flag mechanism (needed for add-ons, ADR-0007) | ✅ **fixed** | `IFeatureFlagService` + `[FeatureGate]`; `FeatureGate.test.tsx` on the client |
| PDF/OCR library licences not yet reviewed | 🟡 Medium | Copyleft would be disqualifying |
| Zawgyi→Unicode normalization not implemented | ✅ **fixed** | `MyanmarScriptNormalizer` + `IMyanmarScriptNormalizer`, applied at ingest across CV extraction, search and `JobApplication`. This row sat at 🔴 High for days after the code landed |
| No .NET client for `myanmar-tools` — integration undecided | ✅ **resolved** | Superseded by `MyanmarScriptNormalizer` — no external client was needed |
| ~~Burmese OCR accuracy unverified~~ **There is no OCR at all** | ⏸ **Paused 2026-08-29 by the product owner** — images are now rejected rather than silently mis-imported; the build/buy decision is deferred. Both upload paths accept **PDF and DOCX only**, and a scanned PDF (unidentifiable until its text stream comes back empty) is marked **`Skipped`**, not Failed, with a reason the recruiter can read. `BulkFileStatus.Skipped` — whose own comment said "nothing produces it today" — now has a producer. The extractor's image path returns empty instead of fabricating; `IDocumentTextExtractor` documents that **empty means "not read", never "read, found nothing"**. 9 tests, 3 mutations. Detail below | This row said "accuracy unverified", which reads as *OCR exists and nobody has measured it*. Read the code 2026-08-29: `DocumentTextExtractor.ExtractFromImageOrScannedAsync` performs **no character recognition**. It reads the PNG header for width/height and returns a string like `Image Document: cv.png \| Format: PNG \| Dimensions: 800x600 \| Size: 41213 bytes`. That is the path taken by every `.png` / `.jpg` / `.jpeg`, **and by every PDF whose text stream is empty** — i.e. every scanned CV. Consequences, none of them visible to the recruiter: the placeholder is stored as `JobApplication.ResumeExtractedText`, so it is what trigram search indexes (a photographed Burmese CV is unfindable, and searching `Image Document` returns all of them); contact parsing is regex over that same string, so name/email/phone come back empty and `FindOrCreateCandidateAsync` makes a blank candidate; and the file is still reported **Success**, not Skipped. Module doc 2.3 promises "OCR reads the documents and auto-builds candidate profiles" — that promise is unmet for images and scans. Fixing it means a real OCR engine (Tesseract with Burmese traineddata, or a vision model call), which is **a new dependency — ask first** per CLAUDE.md. ADR-0009's evaluation plan cannot start until something exists to evaluate |
| Burmese keyword search needs trigram/segmentation | ✅ **fixed** | `AddPgTrgmAndSearchIndexes` migration + `SearchService`. Still unverified against a corpus of real Burmese CVs — trigram *runs*, but nobody has measured whether its results are good |
| No `/api/version`, sizing guide, or upgrade runbook | ✅ **fixed** | `VersionController`, `docs/architecture/server-sizing-guide.md`, `docs/architecture/deployment-runbook.md`. The customer/version *registry* is still a manual list |
| Migration `AddOutboundDeliveryAndScheduledJobs` | ✅ applies on startup | `20260820072400`. Creates `OutboundMessages` and `ScheduledJobs` with three indexes and three check constraints. **Nobody runs `dotnet ef database update` in this project** — Postgres only exists in Docker, and `DatabaseStartup.MigrateAsync` applies pending migrations when the API container starts. Rebuilding the stack is the whole procedure |
| Two migrations directories exist | 🟢 Low | `backend/src/Infrastructure/Migrations/` is canonical (holds every migration and the snapshot). `backend/src/Infrastructure/Persistence/Migrations/` holds one stray duplicate of `AddPgTrgmAndSearchIndexes`. Harmless today, confusing the first time someone adds a migration to the wrong one |
| `ITenantScoped` doc comment still says "agency" | 🟢 Low | `backend/src/Domain/Common/ITenantScoped.cs` — "belongs to a single tenant (agency)". Missed by the 2026-07-27 pivot; a tenant is a **company** |
| **No email sender anywhere in the codebase** | ✅ **built 2026-08-20** | `IEmailSender` + `SmtpEmailSender` (`System.Net.Mail`, no new package), wired to the ADR-0026 outbox and driven by the worker. The first handler, `InterviewInvitationHandler`, ships with it. What this unblocked: Module 3.2 (**done**), Module 4.1/4.2/4.3, Module 5.3, Module 8 — each now needs a handler, not a capability. See the four rows below for what the adapter cannot do |
| SMTP adapter: no XOAUTH2, no implicit TLS | 🟠 High | `System.Net.Mail.SmtpClient` speaks STARTTLS on 587 and username/password only. **Microsoft 365 and Google Workspace both require XOAUTH2** — and `design/internal/settings-integrations.html` draws all three as first-class choices, so today two of those three tiles cannot be honoured. A relay offering only implicit TLS on 465 also cannot be used. Both are fixed by MailKit; that is a package decision ADR-0026 deliberately did not take, not an oversight to patch quietly |
| Candidate-facing email is English only | 🟡 Medium | `InterviewInvitationHandler` renders one language. `design/internal/postings.html` offers a per-posting language choice (Burmese / English / Both) that has **no backing field**, so there is nothing to render from. A Yangon field role advertised in Burmese currently gets its interview invitation in English |
| Nobody is told when a message reaches `Failed` | 🟠 High | The row records it and `design/internal/channels.html` draws the delivery log, but **no UI reads `OutboundMessages` yet**. So "the candidate was never told" is currently discoverable only by querying the database — which is the failure mode ADR-0026 exists to remove. The outbox is only half the answer without the screen |
| Sender identity is still `noreply@` | 🟡 Medium | One company-wide `Smtp:FromAddress`. A candidate replying to an interview invitation — which the body explicitly invites them to do — replies into a mailbox nobody may be reading. ADR-0026 lists sending as the acting recruiter (via Microsoft 365 delegated permission) as an open question; it is now a live one, because invitations are actually going out |
| Migration `AddBulkUploadPersistence` | ✅ applies on startup | `20260821…`. Creates `BulkUploadBatches` and `BulkUploadFiles` with three indexes. Additive — no existing table is touched, and there is nothing to back-fill because the state it replaces only ever existed in memory |
| Two `IBulkResumeService` interfaces existed | ✅ **deleted 2026-08-21** | An identical copy lived in `Application.Common.Interfaces` and was registered in DI alongside the real one. Nothing consumed it. Removed with the rewrite rather than carried forward |
| Migration `AddCompanyTimeZone` | ✅ applies on startup | `20260820081448`. Adds nullable `Companies.TimeZoneId` (IANA, e.g. `Asia/Yangon`). Needed because Npgsql stores `DateTimeOffset` as `timestamptz` and normalises to UTC — the instant survives a round-trip and the recruiter's *o'clock* does not, and "09:00" is the one thing a candidate acts on. Null falls back to UTC and the email labels itself UTC rather than lying |
| **`ICurrentTenant` is now settable** | ✅ **security-reviewed 2026-08-20 — no tenant-isolation finding** | Reviewed against `a2de09c`. Verified: an ambient tenant cannot redirect an authenticated request (middleware order checked, `EnterTenant` called from exactly one place); a scope carries at most one tenant and `CurrentTenant` resolves the same instance the worker set; the cross-tenant claim is contained and the claimed entity is never reattached to a later scope; `PublicJobService` is unaffected. The review found **one Low robustness defect, now fixed** — see the row below | ADR-0026 §4. `IAmbientTenantScope` lets the delivery worker establish a tenant with no HTTP request, so handlers query with filters on. The safety property is ordering: the request claim is read first and wins, making an ambient tenant inert inside a request — asserted by `CurrentTenantResolutionTests`, and a failure there is a security finding. `EnterTenant` refuses a second call so a recycled scope crashes rather than reading cross-tenant |
| Step 3 (invitations + SMTP) | ✅ **security-reviewed 2026-08-20 — nothing found** | Reviewed against `b7f65d3`. Six claims put up to be **disproved**, all held: (1) no path reads across tenants — the unfiltered `Companies` lookup is safe because `message.TenantId` itself comes from a tenant-filtered re-read, so it cannot diverge from the scope's tenant; (2) `Recipient` has exactly one write site and neither request DTO carries an address field, so nobody can have another candidate's details mailed to an address they control; (3) the absent department scoping is sound because `SubjectId` is only ever set inside an authorised write, so the handler's FK chain is the chain that was authorised — and `RescheduleAsync` re-reads the application only *after* `LoadWritableAsync`; (4) header injection blocked — the reviewer tested `MailAddress` against four CRLF variants directly rather than trusting the read, all rejected; (5) no credential or candidate data in any log or payload, nothing real committed to either `appsettings`; (6) the worker change is inside `ConfigureTestServices` and `Program.cs` still registers the real hosted service |
| A slow relay throttles a company's own queue | 🟢 Low | Raised by that review. A pass drains its batch sequentially, so its worst case is `BatchSize × Smtp:TimeoutSeconds` — 20 × 30 s = **10 minutes** against a 5-minute visibility timeout. **No duplicate sends today**: one worker, and passes never overlap. It is a throughput ceiling for one company with a degraded relay, and it becomes a duplicate-send bug the moment anyone runs two replicas — the **fourth** thing on that list. Recorded in `OutboundDeliveryOptions.BatchSize`; bounded parallelism is a product call, not a fix |
| A failure between claiming and recording dodged the attempt cap | ✅ **fixed 2026-08-20** | Found by the security review. Anything thrown between claiming a message and `Record()` escaped to the pass-level catch, so the row's cap was never checked: it could be reclaimed every visibility window **forever** — the exact poison message the cap exists to stop, dodging the cap — and it abandoned the rest of that pass's batch. Now wrapped: the failure is a counted, capped retry, and the batch continues. Two tests, proved to fail first |
| Claim is EF-level, not `FOR UPDATE SKIP LOCKED` | 🟡 Medium | ADR-0026 §3 promised `SKIP LOCKED`; the implementation reads then updates through EF. Crash safety is unchanged, but **two workers could claim the same batch and send twice**. Fine on one instance (ADR-0004). The list of things riding on "one replica" changed on 2026-08-21: the bulk dictionary **came off it** (it is a table now), so what is left is `LoginThrottle` (ADR-0016), this EF-level claim, and the pass-duration point in the row above — **two workers, not three**, plus one tuning invariant. Audit them together before any customer gets a second replica, not after |
| Dead code: `frontend/internal/src/features/requisitions/` | 🟡 Medium | Zero importers repo-wide (verified 2026-08-18, LSP + grep). Five files including `requisitions.test.tsx`, which passes and proves nothing about the shipped app. Delete it, or wire it up |
| ADR-0025 step 3 | 🚧 **3a–3c done 2026-08-21, 3d next** | The preset **is** V1.0 — a copy of `design/internal/ds.js` plus a fenced compat block, so both apps already render V1.0 colours and only the class *names* are old. Base CSS and fonts ported from `ds.css`. `packages/ui/src` is fully migrated (**0 left**), built against `components.html` and verified by computed style in a browser. `packages/ui/src`, `components/` and `pages/` are all at **0**, and `LoginPage` is rebuilt against its kit screen — all verified by computed style in a browser. Remaining: **340** — `features` 323, `public/app` 17. Delete the compat block when that reaches zero; the command is in NEXT-SESSION §0 |
| A failed login said "your session has expired" | ✅ **fixed 2026-08-21** | `apiFetch` mapped every 401 to that copy; the refresh branch excluded `/auth/login` but the fallthrough did not. Someone mistyping a password was told their session had expired and sent looking for a problem that did not exist. Now "Email or password is incorrect." — also the only thing ADR-0016 permits, since naming the field reveals whether an address belongs to a real employee |
| `Retry-After` was discarded, so the lockout had no countdown | ✅ **fixed 2026-08-21** | ADR-0016's 429 carries the remaining lock in seconds and `ApiError` had nowhere to put it, so the UI could not render the countdown the design draws. `ApiError.retryAfterSeconds` now carries it; the login screen counts down from the server's number and falls back to the full 15 minutes only when the header is absent — never a shorter guess |
| Kit nav labels failed contrast at `white/40` | ✅ **fixed 2026-08-21** | The design kit's own nav group labels were `text-white/40`, which on `ink-900` measures **3.81:1** — below AA for 11px text. Raised to `white/50` (5.23:1) in the code and in all 19 kit screens; `design/` is the source of truth and must not carry the defect. Found by measuring the dark rail rather than assuming a design file is right |
| Most screens hand-roll their controls | 🟠 High | Found 2026-08-21 while verifying the `packages/ui` migration: `frontend/internal/src` contains **50 raw `<input>`, 63 raw `<button>`, 24 raw `<select>`**, against 24 files importing `Button` and 9 importing `Input`. The login field is 40px/r6 — not the `Input` component. So a shared-component fix reaches a minority of the surface, and every rebrand costs the same again. Per-screen decision during 3d: adopt the shared component or restyle the local one |
| **163 shipped classes emitted no CSS** | ✅ **fixed 2026-08-21** | Found by building `frontend/internal` and grepping `dist/assets/*.css`: `text-ink-500` (57 uses), `text-ink-700` (39), `text-ink-800` (20), `border-line-300` (28), `bg-surface-100` (13), `border-line-100` (5), `bg-surface-200` (1) were **ABSENT** — the old preset defined `ink` at 900/600/400, `surface` at 0/50, `line` at 200 only, so those elements silently inherited their parent's colour. The V1.0 preset defines every step; re-verified in the build, all seven now PRESENT. It had been shipping that way |
| Fonts: Bricolage + IBM Plex were still downloading | ✅ **fixed 2026-08-21** | V1.0 drops both. Editing the `@import` in `index.css` was not enough — the real request is a `<link>` in `frontend/internal/index.html` and `frontend/public/app/layout.tsx`, so both faces kept loading after the "fix". Caught by reading `document.fonts` in the browser rather than trusting the diff. Now one request, three families: Inter, JetBrains Mono, Noto Sans Myanmar |
| Build warning `CS8604` in `ApplicationFormSchema.cs:102` | 🟢 Low | Possible null reference argument to `HashSet<string>.Add`. Nullable reference types are enabled, so this is a real path the compiler cannot prove safe |
