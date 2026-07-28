# Feature Status

**Last updated:** 2026-07-28 (CI added + first frontend tests; ADR-0018 fix still **unbuilt**) · Legend: ✅ done · 🚧 partial · ⬜ not started · ❌ removed/to remove

> ✅ **Full stack runs.** Backend builds, all tests pass, `InitialCreate` migration applies
> on startup, and `docker compose up` brings up Postgres + API + web (2026-07-27).
> Verified with `docker build --target test --progress=plain --no-cache-filter=build,test ./backend`
> on .NET 10 — **92/92 green** (24 domain + 68 API integration), covering Module 1 end to end,
> login throttling, department administration, and the
> Module 2 requisition → posting → public page → application → pipeline loop.
> `frontend/internal` type-checks clean (`tsc --noEmit`). Claims below are *verified
> running*, not just written.

## Summary by module

| # | Module | Status | Notes |
|---|---|---|---|
| — | Foundation (scaffold, layering, CI-less tooling) | ✅ | Structure per CLAUDE.md |
| — | Multi-tenancy | ✅ | Query filters + claim-based resolver, isolation-tested |
| 1 | Job Requisition & Approval | ✅ API + UI | ⭐ MVP · full loop drivable from the browser: chain config → requisition → submit → sequential approve/reject → cancel. |
| 2 | ATS & Sourcing | 🚧 | ⭐ MVP · **2.1/2.2/2.5/2.7 built** (posting → public page + custom form → application → pipeline). 2.3 OCR, 2.4 Smart Match, 2.6 search not started. |
| 3 | Interview & Assessment | 🚧 API + UI | ⭐ MVP · **3.3 scorecards + 3.4 notes built and tested; UI built** (scheduling, scorecard form, blind panel view, note thread, template admin). 3.1 calendar / 3.2 invitations deferred to Module 7 — no email sender or calendar client exists. UI is **type-checked only, never run**. |
| 4 | Offer & Pre-boarding | ⬜ | Deferred (post-MVP) |
| 5 | Reporting & Analytics | ⬜ | ⭐ MVP · build last · blocked on stage history (Module 2) |
| 6 | Planning & Budgeting | ⬜ | Deferred (post-MVP) |
| 7 | Settings & Integrations | 🚧 | RBAC ✅ (roles need revision); integrations ⬜ |
| 8 | Multi-Channel Sourcing (bots) | ⬜ | First post-MVP; contracted in Mid-Tier (ADR-0014) |

## Delivery readiness (ADR-0004)

Per-company install, subdomain routing. **None of the operational prerequisites exist yet:**

| Item | Status |
|---|---|
| Docker/Compose packaging | ✅ **verified running** |
| Feature-flag mechanism (add-on gating) | ⬜ |
| Background job runner (bulk CV processing) | ⬜ |
| Automated EF migrations on startup | ✅ **done** — `InitialCreate` generated |
| `/api/version` + customer/version registry | ⬜ |
| Support policy (latest, latest-1) | ⬜ |
| Server sizing guide | ⬜ |
| Backup/restore + upgrade runbooks | ⬜ |

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
- `GET/POST /api/approvalchains` (**Admin only** — editing a chain is equivalent to being
  able to approve), `GET /{id}`
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

**UI (`frontend/internal`)** — added 2026-07-28, type-checked but **never run**:

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
- ⚠️ **New endpoint `GET /api/users/selectable`** ([ADR-0019](../decisions/ADR-0019-panel-picker-directory.md)) —
  `RecruitmentStaff`, returns id/name/role without email. Without it a Recruiter cannot name
  the panel the scheduling API requires. **Authorization change: unbuilt, untested, unreviewed**

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
| `RecruitOps.Domain.Tests` | **39 cases** — 24 existing (2 vocabulary/role set, 22 application-form schema) + **15 mention parser** (7 `[Fact]`, 2 `[Theory]` with 8 rows between them) | ✅ **passing** |
| `RecruitOps.Api.Tests` | **117** — 68 existing (4 isolation, 11 department admin, 4 login, 4 login throttle, 2 token, 6 scoping, 17 approval flow, 7 posting flow, 13 public application) + **42 Module 3** (13 interview flow, 12 blind scoring, 7 template resolution, 10 notes) + **7 ADR-0018 approver reach** | ⚠️ **unrun** — 8 of these are new and have never executed |
| `frontend/internal` (Vitest) | **27** — 14 `lib/scorecard` (payload rules), 7 `InterviewDetailPage` (blind rule + draft payload), 6 `ApplicationNotes` (`bodyHtml` injection, mentions, thread filter) | ✅ **27/27 passing** (2026-07-28, node 22 / vitest 2.1.9) |

**≈156 backend** (39 domain + 117 API), up from 92 · **27 frontend**, up from 0.

> ⚠️ **The backend figures are still counted from source, not read off a run.** CI compiles
> the suite and exits 0 (first green run 2026-07-28), so the code is no longer unbuilt — but
> "the build passed" has never been the question here. A cached `COPY . .` layer would happily
> re-report an old count, which is why the build carries `--progress=plain
> --no-cache-filter=build,test`.
>
> **Next session: read the number off the CI job summary** — the `Test counts` step lifts the
> `Passed!` lines out of the BuildKit output — and replace these figures with it. **≈156**
> means the Module 3 and ADR-0018 tests executed; **92** means they did not.

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

**92/92 green** at the last run (2026-07-28), with `--progress=plain --no-cache-filter=build,test`
so the count is the *new* suite and not a cached layer re-reporting an old one. This run is
the first to include the 11 department-admin tests and the rewritten
`DepartmentIsolationTests` case (it previously asserted the bug described in the changelog).
No migration needed. `frontend/internal` type-checks clean (`tsc --noEmit`).

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
- The approval chain runs in sequence, a later approver cannot jump the queue, a rejection
  at step 1 leaves step 2 `Waiting` rather than auto-deciding it, and cancelling clears the
  requisition out of the approver's inbox
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
| `System.Security.Cryptography.Xml` CVE-2026-33116 | ✅ **fixed** | Pinned to 10.0.6 in Infrastructure + Api.Tests (10.0.0–10.0.5 are also vulnerable) |



| Module 3 never compiled; no migration generated | ✅ **fixed** | `20260728061832_Module3Interviews` generated; suite compiles and passes |
| Module 3 authorization surfaces not security-reviewed | ✅ **done** | 2026-07-28 — [`SECURITY-REVIEW-MODULE-3.md`](SECURITY-REVIEW-MODULE-3.md). Two High findings, both fixed (ADR-0018); blind filter cleared |
| The ADR-0018 fix was unbuilt | ✅ **compiles** | CI green 2026-07-28. ⚠️ The *test count* has still not been read off a run — see the inventory note below |
| 🔴 **`GET /api/users/selectable` has no test and no review** | 🔴 **High** | ADR-0019. It compiles now, but an authorization change with zero tests is the gap. Needs a policy-boundary test (Recruiter: 200 on `selectable`, 403 on `/api/users`) and human review |
| `GET /api/users` projects `enum.ToString()` inside the query | 🟡 Medium | EF Core 10 does not translate it; the endpoint has only ever run in-memory, so it may throw against Postgres. Two-step pattern if confirmed (ADR-0019 follow-up) |
| Module 3 UI type-checks but has never been run | 🟡 Medium | No backend to run it against in the authoring environment. First `docker compose up` is the real test |
| Mention resolution loads every active user, then N+1s | 🔵 Low (perf) | `NoteService.ResolveMentionsAsync` — full user scan per note POST, plus 2–3 queries per matched handle. Also `InterviewService.ListForApplicationAsync` (4 queries per round) |
| Role set is too coarse | 🟡 Medium | ADR-0018 needed a second axis because `Approver` fits neither "scoped" nor "sees everything". A third role reading candidate data will force the same question again |
| Module 3 has no frontend | ✅ **fixed** | Five screens built 2026-07-28; type-checked, not yet run |
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
| Frontend images never built; only type-checked | 🟡 Medium | `docker compose up --build` will be the first real test |
| No feature-flag mechanism (needed for add-ons, ADR-0007) | 🟡 Medium | Cheap now, invasive later |
| PDF/OCR library licences not yet reviewed | 🟡 Medium | Copyleft would be disqualifying |
| Zawgyi→Unicode normalization not implemented | 🔴 High | Affects MVP Phase 1, not just OCR (ADR-0009) |
| No .NET client for `myanmar-tools` — integration undecided | 🟡 Medium | Resolve before Module 2 ingest |
| Burmese OCR accuracy unverified | 🟡 Medium | Deferred; evaluation plan in ADR-0009 |
| Burmese keyword search needs trigram/segmentation | 🟡 Medium | Module 2.6; default Postgres FTS won't work |
| No `/api/version`, sizing guide, or upgrade runbook | 🟡 Medium | Required before first install |
