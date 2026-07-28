# Changelog

Track record of every meaningful change. Newest first.
Format: what changed · why · what it touched.

---

## 2026-07-28 (latest)

### 🧪 CI, and the first frontend tests since ADR-0012
**Why:** "written but never compiled" had repeated across three sessions, and the frontend had
**zero** tests while Module 3 had just shipped the first real conditional logic in the repo.

**`.github/workflows/ci.yml`** — two jobs on every push and every PR to `main`:

- **backend**: `docker build --target test ./backend`, with `--progress=plain` and
  `--no-cache-filter=build,test`. Both flags matter: BuildKit collapses test output, and a
  cached `COPY . .` layer will re-report an old pass count, so without them a green build is
  not evidence the new tests ran. Building the Dockerfile rather than using
  `actions/setup-dotnet` also keeps the packaging artefact (ADR-0015) under test.
- **frontend**: `npm ci` at the workspace root (the `@recruitops/*` packages are workspace
  links, so a per-app install would not resolve them) → `typecheck` → `test` → `build`.

This is the fix for the environment problem, not a workaround for it. The authoring sandboxes
have no .NET SDK and an allowlist blocking `mcr.microsoft.com` and `nuget.org`; a hosted runner
has neither constraint. **It does nothing until a git remote exists** — there still isn't one.

**Vitest wired into `frontend/internal`** — jsdom, Testing Library, a setup file that unmounts
between tests (jsdom keeps one document per file, so a leftover tree turns a broken assertion
into a passing one). No `globals`, so `tsc --noEmit` checks the tests under the same config as
the app. `npm run test` added at the workspace root and to each app.

**27 tests, covering the three quiet-failure cases** named in NEXT-SESSION.md — the ones where
being wrong looks like a bug in the *rule* rather than in the view:

- **`lib/scorecard.ts` (new)** — `isSendable` / `toAnswers` / `draftsFrom` / `missingRequired`
  lifted out of `InterviewDetailPage` so they can be asserted directly. 14 cases, including the
  two that a truthiness check would break silently: a **`No` is an answer**, and a rating of
  `0` is too. One test asserts the payload filter and the submit-completeness check still
  agree — they agree only by construction, so nothing else was holding them together.
- **`InterviewDetailPage.test.tsx`** — the blind rule's three renderings pinned by what the
  page actually says (`hiddenCount > 0`, `hiddenCount === 0`, not blinded), plus the
  non-participant's 404 rendering as an ordinary state with no form, and a saved draft
  omitting untouched criteria from the payload rather than sending nulls.
- **`ApplicationNotes.test.tsx`** — `bodyHtml` injected as markup and *not* re-escaped, the
  `span.mention` element the `index.css` rule needs surviving, Burmese text through the
  round trip, and only server-resolved mentions listed.

**The harness was proved to fail before it was believed.** Three deliberate mutations — a
truthiness check on `YesNo`, the blind banner rendered regardless of `hiddenCount`, and
`NoteBody` escaping what the server escaped — produced **5 failures across all three files**;
`tsc` was checked the same way. A green run from a checker nobody has seen fail is worse than
no run, because it gets believed.

**Touched:** `.github/workflows/ci.yml` (new), `frontend/internal/vitest.config.ts` (new),
`src/test/setup.ts` + `src/test/fixtures.ts` (new), `src/lib/scorecard.ts` (new, extracted),
three `*.test.{ts,tsx}` (new), `InterviewDetailPage.tsx` (imports the extracted module),
`package.json` ×2, `package-lock.json`.

⚠️ **The backend is still unbuilt.** This session had no .NET SDK or Docker either — CI is what
will build it, on the first push to a remote that does not yet exist.

### 🖥️ Module 3 gets a UI — scheduling, blind scoring, debrief, templates
**Why:** the whole of Module 3 existed on the API and none of it was reachable from the SPA.

Five screens in `frontend/internal`, plus the Module 3 half of `packages/types`, which had
none of these shapes:

- **`ApplicationDebrief`** — expands from a pipeline row. Interview rounds (schedule,
  reschedule, panel, complete, no-show, cancel) and the note thread. Named for what it is:
  the thread's round picker needs the rounds, so splitting the two would mean a second
  request for the same list.
- **`InterviewDetailPage`** (`/interviews/:id`) — the caller's own scorecard, and the panel's.
  Not nested under a posting: a panel member from another department reaches this round and
  nothing around it (ADR-0017 §4), so the URL must not imply otherwise.
- **`ApplicationNotes`** — renders `bodyHtml`, the server-escaped field, and does not re-escape
  it or rebuild it from `body`.
- **`ScorecardTemplatesPage`** — criteria builder with ordering; scope is one choice producing
  one of two mutually exclusive ids rather than two fields that can contradict each other.

**The blind rule is rendered as a state, not an error** — *"2 evaluations are waiting for yours.
They unlock as soon as you submit"* — and separately when nothing is hidden yet, because
"0 evaluations are waiting" is not a sentence. A recruiter who was not on the panel is not
blinded, and gets no form at all: `GET /interviews/{id}/scorecard` 404s for a non-participant,
which the page treats as an ordinary state rather than a failure.

**Three traps found by writing the client, none of which the test suite could have caught:**

1. **The panel picker had no data source.** `GET /api/users` is `AdminOnly`, so a Recruiter
   could not name the panel the API requires of them — [ADR-0019](../decisions/ADR-0019-panel-picker-directory.md),
   new `GET /api/users/selectable`. ⚠️ Authorization change, **unbuilt and unreviewed**.
2. **`ValidateAnswer` runs on draft saves, not just submits.** A `Rating` sent with
   `rating: null` is rejected outright, so an untouched criterion must be *omitted* from the
   payload rather than sent as nulls. That also matches what "unanswered" means to the
   completeness check, so the two agree — but a client that sent a full grid of nulls would
   find drafts impossible to save.
3. **`GET /api/users` projects `enum.ToString()` inside the query**, which EF Core 10 does not
   translate. It has never run against Postgres. Logged as a follow-up in ADR-0019.

**Role predicates consolidated.** `AppLayout` had three local copies of role lists; they now
live in `lib/auth.ts` as the client mirror of `Domain/RoleScope.cs`. Writing "the only place a
role is named" into a comment while three copies sat one file away was not going to age well.
`StatusPill` absorbed `InterviewStatus` rather than growing a page-local badge beside it.

Both apps type-check clean. Frontend tests remain **zero** — this is the first UI in the repo
with real conditional logic in it, so that gap is now the expensive one.

### 🔒 Module 3 security review — an Approver could read every candidate in the company
**Why:** CLAUDE.md requires a review pass on anything touching authorization, and Module 3
added three surfaces that are not cosmetic. Full report:
[`SECURITY-REVIEW-MODULE-3.md`](SECURITY-REVIEW-MODULE-3.md).

Two High findings, one root cause.

**The blast radius.** An `Approver` is not department-scoped — ADR-0003 said so for a good
reason, because an approval chain crosses departments and Finance has to be able to see a Sales
headcount request. But "not department-scoped" was one boolean, and every candidate-facing
service asked it. So `CanAccessAsync` returned true for every department, and
`ApplicationAccess` handed an Approver `Kind = Department, CanWrite = true` against **every job
application in the company**: any candidate's note thread (readable *and* writable), every
interview, every submitted scorecard — not being a participant, they weren't even blinded — plus
any department's pipeline board and any candidate's stage history. All through
`Policies.InternalUser`, which includes `Approver`.

Nothing was broken. Every layer did exactly what it documented, and the total was a privilege
nobody granted on purpose.

**The sharper half.** `NoteService.CanUserReachAsync` re-derived the reach rule by hand as
`role is UserRole.HiringManager`, so an Approver passed it. The doc comment eight lines above
gave the threat as, verbatim, *"`@finance.approver` what do you think of this candidate" would
put a name and a judgement in front of someone with no business seeing either.* **The named
example was the case that passed.** The existing test used `@finance.manager`, a HiringManager,
and stayed green. Once Module 7 ships notifications, that stops being a stray `<span>` and
becomes an email.

**The fix** — [ADR-0018](../decisions/ADR-0018-approver-candidate-data-exclusion.md): department
scoping and candidate reach are two questions, and `Approver` gets different answers.

- `Domain/RoleScope.cs` — the one place either question is answered. `IsDepartmentScoped`
  unchanged (HiringManager); `IsExcludedFromCandidateData` new (Approver). Both **fail closed**
  on an unrecognised role.
- `ApplicationAccess` applies the exclusion as clause 0, before department scoping;
  `PipelineService` applies it through one helper shared by its three methods.
- `IApplicationAccess.ResolveForUserAsync` answers reach for a **third party**, so mention
  resolution calls the same implementation instead of keeping a copy. `CanUserReachAsync` is
  deleted — it was the only place in the codebase naming a role literal.
- An excluded role is not locked out: an Approver reaches an application by sitting on its
  panel, like a Hiring Manager from another department. Deliberate, visible, and it expires.

**Cleared, with no change:** the blind-scoring filter. Another author's draft never becomes
readable, `blinded` correctly keys on participation rather than reach, and `hiddenCount` carries
strictly less information than `GET /api/interviews/{id}` already returns. `MentionParser`'s
escaping and its bounded regex also held.

**Docs corrected where they contradicted the code:** `ApplicationReach.CanWrite` claimed a panel
member could not "touch anything else" and ADR-0017 §4 said "no writes", but posting to the note
thread is the job a participant was added to do and a test already pinned it. `Policies.cs`
claimed `Approver` was department-scoped, which was simply false.

**Touched:** `Domain/RoleScope.cs` (new), `ICurrentUser`, `CurrentUser`, `IApplicationAccess`,
`ApplicationAccess`, `NoteService`, `PipelineService`, `Policies`, ADR-0003, ADR-0017,
ADR-0018 (new), `ApproverReachTests` (new, 7), `ApplicationNoteTests` (+1), `Module3Scenario`.

> ⚠️ **Unbuilt and unrun.** This session had no .NET SDK and no Docker, and the SDK download is
> blocked by the network allowlist. The code follows the repo's existing patterns but has never
> been compiled, and the 8 new tests have never executed. Build first next session — and note
> the irony that `NEXT-SESSION.md` already warned "check the environment can build before
> writing into it". It was checked this time; the answer was no, and the work was worth doing
> anyway, but the debt is real and belongs at the top of the next session.

## 2026-07-28 (later)

### 🎤 Module 3 — Interview & Assessment (3.3 + 3.4 built; 3.1/3.2 deferred to Module 7)
**Why:** the pipeline had an `Interview` stage with nothing behind it. Scoring and debrief
were happening in spreadsheets and chat, which is the exact problem the module exists to fix.

**Scoped deliberately.** The spec's calendar integration (3.1) and automated invitations
(3.2) need an email sender and a calendar client, neither of which exists in this codebase
and both of which Module 7 owns. Building them here would have turned one session into
three. `NotificationLog` is therefore *not* created — there is nothing to log yet.

Four decisions were made before any entity was written, all recorded in
[ADR-0017](../decisions/ADR-0017-interview-and-assessment.md):

- **Scorecard criteria are department templates with a per-posting override.** The
  department is the level at which comparison means anything — criteria that make two
  salespeople comparable make a salesperson and an engineer incomparable. Resolution is
  most-specific-wins and is resolved *once, at scheduling time*, then stored on the
  interview, so moving a posting between departments later cannot retroactively change what
  an interview was conducted against.
- **A submitted answer snapshots its criterion's label and type.** Same reasoning as Module
  1's approval-chain snapshot: renaming "Communication" to "Stakeholder management" in
  September must not silently rewrite what someone answered in July.
- **Blind scoring.** A panel member sees only their own scorecard until they submit theirs;
  after that, every submitted one. Drafts are visible to their author alone, always. A
  non-participant (a recruiter tracking the loop) sees submitted scores immediately — the
  rule keys on *participation*, not on reach, because a recruiter isn't writing an
  assessment and blinding them would lock them out of their own pipeline. Enforced in the
  service, not the UI: a hidden element is a decoration, and the API is directly reachable.
- **Participation grants access.** An `InterviewParticipant` row grants read on *that one
  application* — its interviews, notes and permitted scorecards. Nothing else: no department
  access, no sibling application, **no writes**. Without this, a panel of HR plus a technical
  lead from another department is impossible under ADR-0003, and that is the most ordinary
  panel there is.

**And the one that touches existing data:** scheduling an interview **moves the application
to `Interview` and writes `ApplicationStageHistory` in the same `SaveChangesAsync`**. Two
writes would allow an interview to exist against an application still at `Screening`, and
Module 5 computes time-to-interview from exactly that row — a gap cannot be reconstructed
later. A second round on an already-`Interview` application writes no history row, because a
no-op transition would be counted as a real stage change.

- **New:** `IApplicationAccess` — the single place the "department reach **or** panel
  participation" rule lives. Interviews, scorecards and notes all call it. Three services
  re-deriving the same two-clause rule is precisely the shape of the bug this repo has
  already shipped three times (a guard added to two of three sibling methods).
- **New entities:** `Interview`, `InterviewParticipant`, `ScorecardTemplate`,
  `ScorecardCriterion`, `Scorecard`, `ScorecardResponse`, `Note`, `NoteMention`.
  New enums: `InterviewMode`, `InterviewStatus`, `CriterionType`, `HireRecommendation`,
  `ScorecardStatus`.
- **New endpoints:** `POST/GET /api/applications/{id}/interviews`,
  `GET/PUT /api/interviews/{id}`, `PUT /{id}/panel`, `POST /{id}/cancel`,
  `POST /{id}/complete`, `GET/PUT /{id}/scorecard`, `POST /{id}/scorecard/submit`,
  `GET /{id}/scorecards`, `GET/POST/PUT /api/scorecardtemplates`,
  `GET /api/scorecardtemplates/resolve/{postingId}`, `GET/POST /api/applications/{id}/notes`.
- **Notes (3.4)** store the body raw and escape **on output**. `MentionParser` produces a
  `BodyHtml` in which every character of user text is escaped and only server-generated
  mention markup survives. Mentions are parsed from the text and never taken from the
  request — a client-supplied list would let anyone forge a note that appears addressed to a
  colleague — and a handle only resolves if that user could reach the application anyway,
  so a mention cannot become a disclosure channel.
- **Removing an interviewer who has started a scorecard is refused** (409) rather than
  cascading the delete. Cancel the round instead.
- Terminal pipeline statuses are refused at scheduling too, not only in `PipelineService` —
  otherwise scheduling is a side door back into a closed application.

**Verified.** Migration `20260728061832_Module3Interviews` generated — 8 tables, 6 unique
indexes, and `ScorecardResponses` carrying **only** the `Scorecards` FK, which is the
snapshot design (§2) surviving into the schema rather than being quietly re-added by
convention. `AppDbContextModelSnapshot.cs` regenerated in the same step, so the next
migration will diff against the right model. Suite compiles and passes; Module 3 adds **56
cases** (41 API + 15 domain), taking the total to ≈148.

> The exact figure the run printed was not recorded here. If it said **92**, the new tests
> did not run — see the counting note in FEATURE-STATUS.md.

**Still outstanding:** the three new authorization surfaces have **not** been security
reviewed, which CLAUDE.md requires for auth changes; and there is no UI. Both are the next
session, per `NEXT-SESSION.md`.

---

## 2026-07-28

### ✅ Department-admin tests executed — 92/92 green
**Why:** the 11 department-admin tests had been written but never compiled or run, so the
whole department-administration slice below was unverified code.

- Run with `docker build --target test --progress=plain --no-cache-filter=build,test ./backend`.
  The flags matter: a bare `docker build` collapses test output and a cached `COPY . .`
  layer will re-report the previous count, so "the build passed" would not have been
  evidence the new tests ran at all.
- **92 total** (24 domain + 68 API), up from 81. First execution of the 11 department-admin
  cases and of the rewritten `DepartmentIsolationTests` case that used to assert the
  Hiring-Manager 403 as correct behaviour.
- No migration required. `FEATURE-STATUS.md` ⚠️ notes cleared.

**Nothing is now outstanding on the backend.** Next session picks up Module 3.

### 🏢 Department administration — and a bug that made the product unusable
**Why:** departments could only be created by hand-seeding the database, which made a
customer install impossible. But the more serious finding was underneath it.

- 🐛 **A Hiring Manager could not raise a requisition through the UI at all.**
  `GET /api/departments` required the `RecruitmentStaff` policy, which *excludes*
  `HiringManager` — so the department picker on the new-requisition form got a 403, the
  page swallowed it into an empty list, and the dropdown was permanently blank. The API
  accepted the request fine; only the screen was broken, which is why nothing caught it.
  A test asserted the 403 as **correct behaviour**, so the suite was actively defending
  the bug. Endpoint is now `InternalUser` and the service scopes the list per ADR-0003;
  the RBAC assertion moved to `/departments/admin`, which really is cross-department.
- **Create, rename, deactivate, reactivate** — all Admin-only. Creating a department creates
  a scope requisitions live in, and whoever can create one can put themselves in it: the
  same authority as editing an approval chain.
- **There is no delete.** Requisitions, postings and the audit trail point at departments;
  deactivating stops new work while leaving the history intact and readable.
- **Deactivating is refused while requisitions are in flight.** Otherwise those requisitions
  are stranded — nobody can finish an approval chain in a department that no longer accepts
  work, and the requester has no route forward. Better to say so than to half-do it.
- **Membership is set as a whole list (PUT), not as deltas.** It is the ADR-0003
  access-control axis, so an admin should be committing to the complete list they can see
  rather than issuing changes against a state they can't. Rows for people who stay are left
  untouched, so `CreatedAt` remains the record of when access was granted.
- **An unknown user id in that list is a 409, not a silent skip.** Skipping would leave an
  admin believing they granted access that nobody has — an invisible failure, on the
  access-control path.
- **Inactive departments are hidden from the picker *and* refused by the API.** Hiding is
  not enough; the API is the boundary, not the UI.
- Admin list carries member and open-requisition counts, so nobody deactivates blind.
- 11 tests, including that granting and revoking membership changes what a Hiring Manager
  can see immediately — which is why access is a DB lookup rather than a token claim.

### 📝 Custom application fields (Module 2.2)
**Why:** the posting already carried an `ApplicationFormFieldsJson` schema and the
application already stored `CustomFieldsJson` — but nothing validated either and nothing
rendered them. "Custom fields" that are stored unvalidated from an anonymous request are not
a feature; they are an anonymous write of arbitrary JSON into the customer's database.

- **`Domain/ApplicationFormSchema`** holds both halves: schema validation (recruiter side)
  and answer validation (applicant side). In Domain rather than a service **because two
  different code paths with two different threat models have to agree exactly on what the
  schema means** — the recruiter writes it through the internal API, a stranger answers it
  through the public one, and any disagreement between them is the vulnerability.
- **Answers are rebuilt, not passed through.** The validator returns a freshly constructed
  document containing only known keys with coerced types. Checking the applicant's JSON and
  then storing it would keep whatever else was in it.
- **Unknown keys are dropped, not rejected.** A stale browser tab answering a question the
  recruiter deleted five minutes ago shouldn't cost the applicant their whole submission —
  but the extra key must not reach the database either.
- **The schema is validated when the recruiter saves it**, not when an applicant meets it.
  A broken schema caught on the public page surfaces to a stranger with nobody watching.
- Types coerced to canonical forms: numbers stored as numbers so reporting can aggregate
  them, dates round-tripped as `yyyy-MM-dd` so nothing has to guess whether `03/04` was March
  or April, `select` values checked against the offered list (anything else came from a
  tampered form). `true` and `"true"` are both accepted — clients disagree about that, and it
  shouldn't be a submission failure.
- Optional unanswered fields are **omitted rather than stored as `""`** — "answered nothing"
  and "was never asked" are different facts.
- **Field keys are generated, never edited.** The key is the JSONB key answers are stored
  under; letting someone rename it later would orphan every answer already collected.
- **UI both sides:** a field builder in the posting editor (add/reorder/remove, per-type
  options), a renderer on the public form, and the answers shown on the pipeline card —
  under the *current* schema's labels, falling back to the raw key for a question deleted
  after someone answered it. Hiding those would be tidier and would misrepresent the candidate.
- 13 unit tests on the validator (the interesting cases are the malformed ones, which is
  slow and unclear to enumerate over HTTP) plus 3 through the API.

## 2026-07-27

### 🚀 Module 2 slice 1 — approved requisition → public job page → pipeline
**Why:** Module 1 produced approved requisitions that nothing consumed. This is the join:
the output of the governance loop becomes the input of the hiring loop, and the product has
an end-to-end path from "we need a person" to "here is a candidate at Screening".

✅ **Migration generated:** `20260728023109_Module2Ats` — new `ApplicationStageHistories`
table, real columns on the four stub entities, unique indexes on `PortalLinks.Token` and
`JobPostings.RequisitionId`. Per the CLAUDE.md guardrail it is **proposed, not applied** to
anything but a local database.

⚠️ One thing to know if this ever meets existing data: `JobPostings.RequisitionId` goes from
nullable to required with a `Guid.Empty` default. There are no rows today, so it is free —
but on a populated database every orphan posting would collapse onto the same key and the
new unique index would refuse to build. That is the correct failure (a posting with no
approval behind it should not exist), it just needs a data fix first, not a retry.

**The rule the whole slice is built around:** a posting can only be created from an
**Approved** requisition, and only one posting per requisition. Enforced in the service
*and* as a unique index, so "nothing is advertised that the business didn't approve" is a
property of the database, not a habit of the code.

- **`POST /api/jobpostings`** copies the requisition's title and description into a Draft
  posting. **Copied, not referenced** — a recruiter rewrites an internal JD into
  candidate-facing copy, and that must not alter the document approvers signed off on.
- **Publish mints the public token once and keeps it.** Re-issuing on every publish would
  break every link already posted to Facebook or sent to a candidate. 256 bits from a
  CSPRNG: it is the only thing protecting that page.
- **Salary is off by default on the public page.** The requisition carries a budget, so the
  posting inherits it — but `ShowSalary` starts false, because the alternative is that the
  first person to publish a job also publishes the company's pay bands. `PublicJobDto` is a
  separate, narrower type from `JobPostingDetailDto` for the same reason; reusing the
  internal DTO is exactly how internal fields end up on a page shared to Facebook.
- **The public endpoint has no tenant claim**, so `_tenant.TenantId` is `Guid.Empty` and the
  global query filters would match nothing. `PublicJobService` therefore reads with
  `IgnoreQueryFilters()` and re-applies the tenant *from the token's own row* — the token is
  what establishes the tenant. Every entity it writes sets `TenantId` explicitly, since the
  usual auto-stamp would fill in `Guid.Empty` and silently orphan the row.
- **Unknown, revoked, expired and unpublished tokens all return the same 404**, so nobody
  can probe for near-miss tokens. The confirmation response carries no ids either.
- **The public endpoint is rate limited** (`RateLimit:PublicApply`, 120/60s — higher than
  login, because a shared job link produces a genuine burst from one office address). It is
  the only anonymous endpoint that *writes*; without a limit, anyone can fill a customer's
  candidate database with junk.
- **Duplicate detection (2.7) keys on normalised contact details.** `ContactNormalizer`
  lives in Domain, not in a service, because the public form and any future CV import must
  produce byte-identical values — if they normalise differently, the same person imported
  twice is silently two people. `"+95 9 765 432 100"`, `"09765432100"` and
  `"0095 9765432100"` all reduce to one key. A match reuses the candidate and **fills blanks
  without overwriting**: a stranger sharing a household number must not rewrite someone
  else's name.
- **`ApplicationStageHistory` is written from the first moment**, including the anonymous
  arrival (with a null actor). Module 5's headline numbers are differences between these
  timestamps, and history that wasn't recorded as it happened cannot be reconstructed —
  the analytics module would launch blind. Hired and Rejected are terminal for the same
  reason: moving back out would corrupt time-to-hire silently.
- **Public app now renders for real** — `/jobs/[token]` fetches server-side so Open Graph
  metadata carries the actual title and a word-boundary-truncated description, which is the
  entire reason that app is SSR (ADR-0012). Only the form is a Client Component; making the
  page interactive to get one form would have thrown the metadata away.
- **`StatusPill` absorbed `JobStatus`** instead of the postings page growing its own badge —
  the component's job is to carry the whole status vocabulary, and a page-local copy drifts
  the first time a colour changes.
- 16 integration tests over the loop, including that a Hiring Manager gets **404, not an
  empty list**, for another department's pipeline.

### 🔒 Brute-force protection on login (ADR-0016) + security-review fixes
**Why:** `POST /api/auth/login` was the one anonymous endpoint that verifies a secret, and
nothing limited how often it could be called — the password policy was the only barrier
between an attacker and every account in an install holding salary bands and candidate PII.

- **Two axes, because they see different attacks.** Per-IP fixed-window limiter (60/60s,
  IPv6 grouped by /64) stops one host hammering; per-account `ILoginThrottle` (5 failures →
  15 min) stops credential stuffing spread across many IPs. A per-IP limiter is blind to the
  second, a per-account one is blind to the first.
- **Failures are counted for emails that don't exist**, so the 429 can't be used to
  enumerate valid addresses — which would have quietly undone the identical-401 behaviour
  `AuthService` already had.

**Found by the security-review pass, and worth recording because most were not obvious:**

- 🐛 **The test config override never took effect.** Program.cs read the limits into locals
  at startup; `WebApplicationFactory` adds its configuration during `Build()`, i.e. *after*
  those lines have run. The limiter kept the production default and the whole login suite
  would have tripped it in an order-dependent way. Limits now come from
  `IOptions<LoginRateLimitOptions>` resolved inside the partitioner.
- 🐛 **The throttle was a memory-exhaustion vector.** `[EmailAddress]` only requires one
  '@', so a ~30 MB address passed validation and was retained for 15 minutes. The key is now
  a SHA-256 of the normalised email, `LoginRequest` has length caps, and the entry cap is a
  real bound rather than a sweep trigger. Sweeps became time-based — a count-based trigger
  is attacker-controlled, since parking junk entries makes every later login walk the map.
- 🐛 **Thread-safety.** `RetryAfter` read `DateTimeOffset` (16 bytes, non-atomic) outside the
  lock the writer held — a torn read could clear a lockout. `AddOrUpdate`'s delegate mutated
  state, and it can be retried, so one failed login could count twice and lock an account out
  early. All access is now under a per-entry lock with a side-effect-free factory.
- 🐛 **Timing oracle.** An unknown email returned before hashing; a real one paid for PBKDF2.
  The measurable difference was the last enumeration channel, and the docs claimed there
  wasn't one. `AuthService` now verifies against a dummy hash on the miss path.
- 🐛 **IPv6 bypass.** Partitioning on the full address gives anyone with a /64 — i.e. a normal
  VPS or home connection — 2^64 buckets. Grouped by /64 now.
- 🐛 **`SubmitAsync` was missing the ownership check** that edit and cancel had just gained.
  `CanAccessAsync` returns true unconditionally for every non-department-scoped role, Approver
  included — so an approver could push someone else's Draft into the chain and then decide on
  it. This is exactly the gap that appears when a rule is added to two of three sibling methods.
- 🐛 **`DecideAsync` leaked status before checking the caller.** The 409 names the status, so
  any internal user could probe a GUID and tell "doesn't exist" from "exists, Approved" —
  the leak ADR-0003's 404-not-403 rule exists to prevent. Caller identified first now.
- ⚡ **`GetInboxAsync` loaded every waiting approval row in the company** on each request.
  Now filtered in SQL by approver first. `First()` → `MinBy(Sequence)` so the "whose turn"
  logic no longer silently depends on the query's ordering surviving a future edit.
- **`ForwardLimit = 1` is now commented as load-bearing** — it is what makes nginx's appended
  address the one that's trusted; raising it would let clients spoof the partition key.

### ✅ Module 1 — editable Drafts
**Why:** with no edit, a typo in a requisition meant cancelling it and raising a new one,
which pollutes the record with abandoned requisitions that never should have existed.

- `PUT /api/requisitions/{id}` — **Draft only**. Once submitted it returns **409**: approvers
  are deciding on those contents, and letting them change underneath would make every
  recorded decision refer to a document that no longer exists. (`A_Submitted_Requisition_
  Cannot_Be_Edited` uses a 1 → 50 headcount change to make the abuse case concrete.)
- Same authority rule as cancel — requester or Admin/HrDirector, **not** approvers.
  Extracted into a shared `IsOwnerOrCompanyWide` helper so the two can't drift apart.
- **Moving a Draft between departments is allowed, but both ends must be reachable.**
  Checking only the target would let a Hiring Manager push a requisition into a department
  they can't see; checking only the source would let them pull one out. 404 either way (ADR-0003).
- **`UpdateRequisitionRequest` is a separate DTO from `CreateRequisitionRequest`** even though
  the fields match today. What you may set at creation and what you may later change are
  different questions; sharing the type would make the first divergence a breaking change on both.
- **Frontend:** `NewRequisitionPage` became `RequisitionFormPage` with a `mode` prop and a
  `/requisitions/:id/edit` route. Create and edit are the same form over the same fields —
  two copies would have drifted the first time a field was added. Non-Draft loads show why
  editing is refused rather than letting someone fill in a form the API will reject.

### ✅ Module 1 usable end to end — cancel flow, approver inbox, admin UI
**Why:** the module had an API and a partial UI, but the loop couldn't actually be driven
from a browser: three of the five screens didn't exist, the SPA's `/api` calls never reached
the API container, and a submitted requisition could only ever move forwards.

- **Cancel / withdraw** — `POST /api/requisitions/{id}/cancel`, plus a Withdraw card on the
  detail page. `RequisitionStatus.Cancelled` had existed in the enum and in `StatusPill`
  since the pivot with nothing able to produce it. Design decisions worth recording:
  - **Permitted to the requester or a company-wide role** (Admin/HrDirector), not to
    approvers — being asked to approve something is not authority to withdraw it.
    Anyone else gets **404, not 403**, consistent with ADR-0003.
  - **Only Draft or PendingApproval.** Cancelling a decided requisition would rewrite
    history, so approved/rejected/already-cancelled → **409**.
  - **Approval steps are deliberately left `Waiting`.** "Cancelled while waiting on Finance"
    is a fact worth keeping; overwriting the steps would fabricate decisions nobody made.
- 🐛 **Consequence of that choice, found while writing the test:** `GetInboxAsync` keyed only
  off Waiting approval rows, so a cancelled requisition would have sat in the approver's
  inbox forever. The inbox now also requires `Status == PendingApproval`.
- **Approver inbox** — `GET /api/requisitions/inbox` returns only requisitions whose
  *lowest-sequence* Waiting step belongs to the caller, so approvers can't work the queue
  out of order. New `InboxPage`.
- **Admin screens** — `ApprovalChainsPage` (dynamic step rows, approver picker) and
  `JdTemplatesPage`. New `GET /api/users` (Admin only) exists to populate the approver picker.
- **`RequisitionDetailDto`** now supersedes the list DTO on single-item endpoints, carrying
  the job description and the full approval timeline so the detail page needs no second
  round-trip. It also carries `RequestedByUserId` — a *display hint* for whether to offer
  Cancel; the backend re-checks it.
- **`LoginResponse.UserId`** added and stored in the session. The detail page's "is it my
  turn?" check was previously comparing an approver id against the access token, so it was
  dead code that always evaluated false — the approve/reject form never appeared.
- **Role-aware sidebar** — Inbox for Admin/HrDirector/Approver, JD Templates for recruitment
  staff, Approval Chains + Departments for Admin. Still a UX affordance, not a boundary.
- 🐛 **405 on login.** `frontend/internal`'s nginx had only an SPA `try_files` block, which
  answers a POST to a non-existent static path with 405 — so `/api/auth/login` never left
  the browser's container. Added a real `nginx.conf` proxying `/api/` to `http://api:8080`.
  It's a file now rather than a `printf` heredoc in the Dockerfile, because the next person
  to edit an escaped nginx config inside a `RUN` line will get it wrong.
- 🐛 **500 on the requisitions list.** The projection called `r.Status.ToString()` and ran a
  correlated subquery for the awaiting-step label *inside* a `select` — neither translates
  in EF Core 10. Replaced with a two-query pattern: SQL join for rows, one batched load of
  Waiting labels (no N+1), then projection in memory where `ToString()` is safe.
- 🐛 **Seed credentials were blank** in `.env`, so `DbInitializer` silently skipped seeding
  and there was no account to log in with. Requires `docker compose down -v` to take effect.
- **Tests:** approval-flow tests now assert against `RequisitionDetailDto` and check the
  *timeline*, not just the status — including that a rejection at step 1 leaves step 2
  `Waiting` rather than auto-deciding it. Five new tests cover cancel (requester, mid-approval
  inbox clearing, approver refused, approved → conflict) and inbox hand-off ordering.

### 🎨 Frontend restructured into two apps + Module 1 UI
**Why:** ADR-0012 decided the split; doing it before building screens avoids moving a
dozen files later. Partner-led sales (ADR-0011) also needs something demoable.

- **npm workspaces** at the repo root. **`packages/ui`** holds the Tailwind preset — the
  single source of design tokens — plus `StatusPill`, `Button`, `Card`. **`packages/types`**
  mirrors the backend DTOs so a contract change breaks *both* apps at compile time.
  This is the anti-drift mechanism ADR-0012 warned about; without it the two frontends
  would have diverged within weeks.
- **`frontend/internal`** (Vite SPA) — login, requisitions list (with a note when the view
  is department-scoped), requisition detail with submit/approve/reject, new-requisition form
  with JD-template prefill, departments list.
- **`frontend/public`** (Next.js SSR) — `/jobs/[token]` with Open Graph + Twitter card
  metadata, which is the entire reason this app is server-rendered: shares to Facebook,
  Telegram and Viber (Module 8) need an unfurled preview a SPA cannot produce.
  Marked `robots: noindex` since these are unlisted links.
- **Two Dockerfiles** built from the repo root (they need `packages/*`); compose now has
  `internal` (nginx, with SPA history fallback) and `web` services.
- ⚠️ **Security trade-off documented, not silently chosen:** the SPA keeps its token in
  `sessionStorage` — readable by any XSS on the origin. An httpOnly cookie would be
  stronger but needs backend changes (cookie issuance + CSRF) that ADR-0002 hasn't made.
  `sessionStorage` over `localStorage` at least ends the session with the tab, which matters
  on shared/hot-desked machines. **Revisit before a bank deployment.**
- 🐛 Caught by `tsc --noEmit`: `import.meta.env` needs Vite's client types — added
  `vite-env.d.ts`. Type-check is now clean.
- Note: the route guard is a UX affordance, **not** a security boundary — every endpoint is
  independently authorised server-side.

### ✅ Module 1 complete (API) — approval chains + JD templates
**Why:** the module looked done but wasn't usable — there was no way to configure an
approval chain, so `submit` always threw "No active approval chain is configured".

- **`/api/approvalchains`** (Admin only) — create a chain with ordered steps in one call.
  Admin-only because *editing a chain is equivalent to being able to approve* headcount
  and spend; that's company configuration, not day-to-day recruiting.
- **`/api/jdtemplates`** — read for any internal user (a Hiring Manager drafting a
  requisition needs them), create limited to recruitment staff. Department-scoped users
  still see company-wide templates.
- **Two guards worth noting:** every approver is validated to be a real user in the company
  on chain creation (otherwise a requisition could be submitted into a chain nobody can
  action), and step `Sequence` is derived from list order so gaps and duplicates are
  impossible by construction.
- **6 new tests** against a seeded **two-step** chain (HR → Finance), so sequencing is
  exercised rather than assumed: full approval walks both steps; rejection at step 1 rejects
  the requisition; **a later approver cannot jump the queue**; submitting twice is 409;
  a recruiter cannot create a chain (403); a chain naming an unknown approver is refused.

### ✅ Module 1 green — 16/16 tests, migration applied
`Module1Requisitions` migration generated (ApprovalChains, ApprovalChainSteps, JdTemplates,
Requisitions, RequisitionApprovals). Schema review: **zero destructive operations in
`Up()`**, enums stored as `character varying`, `SalaryBudget` as `numeric(18,2)`, unique
indexes on `(ApprovalChainId, Sequence)` and `(RequisitionId, Sequence)` so approval steps
can't be duplicated or reordered by accident.

🐛 **Found by the tests — a silent data-loss bug.** `CreateAsync` never set `TenantId`, so
new rows saved with `Guid.Empty` and immediately became invisible to the tenant query
filter: the write "succeeded" but the row could never be read back (404 on create).
Fixed **systemically** rather than at the call site — `AppDbContext.SaveChanges` now stamps
`TenantId` on any added `ITenantScoped` entity whose value is empty (and `UpdatedAt` on
modified rows). Every future service is now immune to this rather than having to remember.

### 🏗️ Department scoping + Module 1 (Requisition & Approval)
**Why:** ADR-0003 was decided but unimplemented, and it was the last prerequisite before
the MVP's first module (ADR-0006).

**Department scoping (ADR-0003)** — `ICurrentUser` reads the principal from claims;
`IDepartmentAccess` resolves the user's departments **from the database on each request**,
cached per request. Chosen over embedding them in the JWT because revocation must take
effect immediately, not after an 8-hour token expires. Applied as an **explicit predicate**,
never a global query filter, and out-of-scope rows return **404 rather than 403** so
existence is not leaked.

**Module 1** — `Requisition`, `RequisitionApproval`, `ApprovalChain`, `ApprovalChainStep`,
`JdTemplate`; create → submit → sequential approve/reject. Notable design choice: submitting
**snapshots the approval chain onto the requisition** rather than referencing it live, so
editing a chain later cannot rewrite decisions that have already been made.

**6 new scoping tests** — a Recruiter sees all departments; a Hiring Manager sees only their
own, gets 404 for another department's requisition, cannot create in a department they don't
own, and can create in one they do.

- Also updated an existing isolation test: TenantA now seeds two departments, so
  `Assert.Single` became `Assert.Equal(2, ...)` plus explicit cross-tenant exclusion.
- `TestAuthHandler` now injects a user-id claim (`X-Test-UserId`) so scoping is testable.
- ⏳ **A second migration is required** — five new entities. Run
  `dotnet ef migrations add Module1Requisitions`.

### 🚀 Stack verified running end-to-end
`docker compose up` brings up Postgres + API + web; migrations apply automatically on
startup; the CVE pin cleared the NU1903 warnings. The foundation phase is complete —
next work is product features, not plumbing.

### 🧱 InitialCreate migration generated + CVE pinned
**Why:** the schema now exists, so a real Postgres database can be created.

- **`20260727085909_InitialCreate`** — 9 tables (Companies, Departments, Users,
  UserDepartments, JobPostings, JobChannelPosts, Candidates, JobApplications, PortalLinks).
- **Schema review passed:** `Up()` contains **zero** destructive operations (all 9
  Drop/Alter statements are in `Down()`, which is correct for a rollback);
  4 unique indexes as designed (`Companies.Slug`, `Departments(TenantId,Name)`,
  `UserDepartments(UserId,DepartmentId)`, `Users.Email`); enums persisted as
  `character varying`, not integers, so adding an enum value later cannot reinterpret
  existing rows; FK behaviour is 5 × Cascade (owned rows) + 2 × Restrict (referenced data).
- **Security fix:** pinned `System.Security.Cryptography.Xml` to **10.0.6** in Infrastructure
  and Api.Tests. ⚠️ Worth noting that 10.0.0–10.0.5 are *also* vulnerable
  (CVE-2026-33116, EncryptedXml DoS) — the obvious "pin to 10.0.0" would have silenced the
  warning while leaving the vulnerability in place.
- Also fixed while getting here: the documented migration command failed on PowerShell
  (`\$PATH` is not escaping there), then on stale Windows `obj/` folders — `docker run -v`
  *mounts* the source so `.dockerignore` does not apply, unlike `docker build`. Both are
  now documented in [local-development.md](../architecture/local-development.md).

### 🗄️ EF model configured + automatic migrations on startup
**Why:** with the in-house model settled (ADR-0006 said to wait for exactly this), the
schema can be created. A real Postgres database still had no tables.

- **Model configuration** in `OnModelCreating` — required fields and max lengths, FK
  relationships (no navigation properties, so configured explicitly), delete behaviour
  (`Restrict` on referenced data, `Cascade` on owned rows), and **enums stored as strings**
  so adding a value later can't reinterpret existing rows.
- **Indexes:** `Company.Slug` unique (subdomain routing); `Department (TenantId, Name)`
  unique; `UserDepartment (UserId, DepartmentId)` unique; `JobApplication` by posting/
  candidate and by `(TenantId, Status)` for pipeline views.
- **`User.Email` unique globally, not per tenant** — login matches on email alone
  (ADR-0002's known limitation), so the database now enforces that explicitly instead of
  leaving it as a latent ambiguity.
- **`AppDbContextFactory`** (`IDesignTimeDbContextFactory`) so `dotnet ef` can scaffold
  migrations **without booting the API** — otherwise generating a migration would run the
  startup migration logic and try to reach a database.
- **`DatabaseStartup.MigrateAsync`** applies pending migrations before serving traffic
  (ADR-0004: unattended installs on customer servers). Guarded by `IsRelational()` so the
  in-memory test provider is unaffected, and by `Database:AutoMigrateOnStartup`.
  Deliberately not wrapped in try/catch — a half-migrated database must not serve traffic.
- Caught while writing: the usual `PrivateAssets`/`IncludeAssets` template for the EF
  Design package omits `compile`, which would have broken `AppDbContextFactory`. Used a
  full package reference instead.
- ⏳ **The migration itself is not generated** — that needs `dotnet ef`, and per the
  `CLAUDE.md` guardrail migrations are proposed, not auto-applied. Command documented in
  [local-development.md](../architecture/local-development.md).

### ✅ Green build — backend compiles and all tests pass
**Why:** closing out the .NET 10 upgrade, the in-house migration and the container work.
This is the first time any of it has been verified rather than assumed.

**`docker build --target test ./backend` → FINISHED. 12/12 tests passing.**

Three bugs were found and fixed along the way — each only visible to a compiler or a
running test, which is the point:

1. **`CS0118`** — `Application` entity vs. `RecruitOps.Application` namespace →
   renamed the entity to `JobApplication`.
2. **`CS1061`** — `ConfigureTestServices` needs `using Microsoft.AspNetCore.TestHost`;
   plus `CS0108` where `TestAuthHandler.Scheme` shadowed the base property → renamed to
   `SchemeName`.
3. **Two EF providers registered** — EF Core 9+ applies provider config through
   `IDbContextOptionsConfiguration<TContext>`, so removing only `DbContextOptions<T>` left
   Npgsql registered alongside InMemory → now removes every `DbContextOptions*` registration.
4. **Cross-class test contamination** — `IClassFixture` gives one factory *per test class*,
   each with its own `TenantA` GUID, but all shared one static in-memory store name. The
   first class to run seeded the data; later classes skipped seeding (guarded by
   `if (!Any())`) and compared against GUIDs that were never persisted. Fixed by giving each
   factory its **own database name**, which also removes any parallel-run race.

**Now verified working:** tenant isolation, RBAC (401/403), login flow, JWT claims,
.NET 10, and the container build.

⚠️ Still open: no EF migration exists (a real Postgres database still has no schema), the
NU1903 vulnerable test dependency, and the frontend image is unbuilt.

### 🔨 First successful compile — and the first real bug
**Why:** ran `docker build --target test ./backend` on a machine with Docker. The backend
had never been compiled before this.

- ✅ **`RecruitOps.Domain`, `RecruitOps.Application`, `RecruitOps.Domain.Tests` build clean**
  on .NET 10 — the framework bump (ADR-0010) and the container setup (ADR-0015) both work.
- 🐛 **Fixed `CS0118`** in `AppDbContext.cs`: the `Application` **entity** collided with the
  `RecruitOps.Application` **namespace** (the Clean Architecture layer), so the compiler read
  `DbSet<Application>` as a namespace. **Renamed the entity to `JobApplication`** rather than
  aliasing — Clean Architecture always has an `Application` layer, so the collision would
  have recurred in every file that touched the entity. Updated `AppDbContext`,
  `frontend/lib/types.ts`, the data model and four module specs.
- 📌 **Lesson recorded:** the static checks used while no SDK was available did *not* catch
  this — namespace-vs-type ambiguity is only visible to a compiler. Static analysis was a
  useful substitute, not an equivalent.
- ⚠️ **Security finding:** `System.Security.Cryptography.Xml` 9.0.0 is pulled in transitively
  by the **test** project and carries **7 high-severity advisories** (NU1903). Not shipped to
  production, but worth closing before any bank security review — add an explicit
  `PackageReference` to a patched version, or run `dotnet list package --vulnerable`.
- ⏳ Infrastructure, Api and the test run remain unverified — rerun the build.

### 🐳 Containerised the stack
**Why:** two pressures, one answer — ADR-0004 already required containers for per-company
installs, and more than one person will now work on the codebase. It also gives a way to
compile without a local .NET 10 SDK.

- **[ADR-0015](../decisions/ADR-0015-containerisation.md)** — same image definitions for
  local development and customer installs; configuration by environment variable only.
- `backend/Dockerfile` — multi-stage `sdk:10.0` → **`test` target** → publish →
  `aspnet:10.0` runtime, non-root via `$APP_UID`.
  `docker build --target test ./backend` compiles **and runs the test suite with no local
  SDK** — currently the shortest path to a first successful build.
- `frontend/Dockerfile` — multi-stage Node 22.
- `docker-compose.yml` — `db` (Postgres 17, healthchecked), `storage` (MinIO, mirroring the
  on-prem S3 path of ADR-0013), `api`, `web`. `JWT_KEY` is **required with no default**, so
  startup fails loudly rather than running on a weak key.
- `.env.example` + `.dockerignore` files; new guide
  [architecture/local-development.md](../architecture/local-development.md).
- ⚠️ **Never built** — no Docker daemon and no `mcr.microsoft.com` access in the authoring
  environment. Expect real errors on the first `docker compose up --build`, from both the
  container setup and the never-compiled C#.
- ⚠️ Still missing: **automated EF migrations on startup** (no migration exists yet, so a
  fresh database has no schema), a committed `package-lock.json` (build not reproducible),
  and production proxy/TLS/subdomain routing.

### 🧹 Agency code removed, renamed to in-house, moved to .NET 10
**Why:** execute MIGRATION-PLAN Steps 1–4 and ADR-0010 together — the framework bump and
the first compile will surface errors at the same time, so they belong in one change.

- **Deleted 17 agency-only files** — `Client`, `Contract`, `ClientTier`, `ContractStatus`,
  `ClientFeedback`, `ContractStatusCalculator`, `ClientService`, `IClientService`,
  `IContractService`, `ClientListItemDto`, `ClientsController`, `ContractsController`,
  its domain test, and the frontend clients page / `TierBadge` / contract helpers + test.
- **Renamed:** `Tenant`→`Company` (with `Slug` for subdomain routing), `Job`→`JobPosting`
  (department-owned, nullable `RequisitionId` for Module 1), policy
  `AgencyStaff`→`RecruitmentStaff` (plus a new `InternalUser` policy for department-scoped roles).
- **Enums rewritten:** `UserRole` → Admin/HrDirector/Recruiter/HiringManager/Approver;
  `PipelineStatus` → Sourced/Applied/Screening/Shortlisted/Interview/Offer/Hired/Rejected.
- **New:** `Department`, `UserDepartment` (many-to-many, per ADR-0003 — a manager may own
  several departments), `DepartmentsController` + service + DTO.
- **`PortalLink` repurposed** to the public applicant job page (ADR-0001).
- **All 6 projects → `net10.0`** with matching package versions (ADR-0010).
- **Tests:** isolation tests rewritten against `Department` (same 4 assertions), role and
  pipeline-vocabulary guards updated, new frontend api-client tests (3 passing).
- ⚠️ **Still never compiled** — no .NET SDK in the environment. Static verification was
  done instead (no dangling refs, namespaces resolve, DbSets map to entities, backend↔
  frontend enums identical, StatusPill exhaustive). **`dotnet build` locally is the next step.**
- Caught by static check: `User.Role` still defaulted to the deleted `JuniorRecruiter`; fixed.

### 🔀 Reconciled with v2.0 strategy document
**Why:** a v2.0 master knowledge-base draft introduced market strategy, a different
commercial model and several architecture changes. Reconciled against existing ADRs;
three direct contradictions resolved by decision.

- **[ADR-0010](../decisions/ADR-0010-dotnet-10-lts.md)** — target **.NET 10 LTS**.
  Verified: .NET 8 LTS ends **10 Nov 2026** (~4 months away) and **.NET 9 STS support
  already ended (~May 2026)**, so the draft's ".NET 8 / 9" is stale. .NET 10 is supported
  to **14 Nov 2028**. Doing it now is near-free — nothing has ever been compiled.
- **[ADR-0011](../decisions/ADR-0011-commercial-model-v2.md)** — **supersedes ADR-0005**.
  Annual **MMK subscription** (Mid-Tier 20–35 lakh, Enterprise 60–80 lakh), 100% upfront,
  20% FX renegotiation clause, partner commission 20–30% of year 1.
  ✅ This **removes the year-2 revenue cliff** flagged in ADR-0005.
  ⚠️ But **churn becomes fatal** and year-1 margin is thin after commission + cloud —
  the profit is in renewals, so retention is now the key metric.
- **[ADR-0012](../decisions/ADR-0012-frontend-split.md)** — **two frontends**: Vite+React
  SPA (internal) + Next.js SSR (public job pages, for Open Graph previews on social shares).
  Requires a shared `packages/ui` + `packages/types` workspace or the design system will
  drift. Existing single Next.js app must be reorganised.
- **[ADR-0013](../decisions/ADR-0013-infrastructure-and-storage.md)** — PostgreSQL on
  **AWS RDS**, **JSONB** endorsed for customer-defined fields (it is the mechanism that
  keeps customization in the "configuration" bucket of ADR-0007), **Cloudflare R2** for
  storage behind an **S3-compatible abstraction** so on-prem can use MinIO.
  ⚠️ **Postgres "Native Full-Text Search" does not work for Burmese** — English only;
  Module 2.6 needs `pg_trgm` or segmentation (per ADR-0009).
- **[ADR-0014](../decisions/ADR-0014-multi-channel-sourcing.md)** — Viber/Telegram/Facebook
  bots become **Module 8**, built **immediately after** the MVP, not inside it: bots are an
  intake channel into Module 2's pipeline, so building them first inverts the dependency.
  New spec: [module 08](../product/modules/08-multi-channel-sourcing.md).
  ⚠️ Webhooks need a publicly reachable endpoint — on-prem behind a firewall may not
  qualify, so this may be hosted-tier only.
- **Confirmed:** *every* tier gets a dedicated database; "Dedicated DB" in the Enterprise
  tier means a dedicated RDS instance (Multi-AZ). ADR-0004 stands; tenant plumbing stays dormant.
- ⚠️ **Findings on the draft's proposed parsing stack:** **PaddleOCR has no official Burmese
  support** ("call for contribution") — worse than Tesseract for this market; and **Apache
  Tika is Java**, so a .NET backend needs a JVM sidecar or an in-image JVM on every install.
- ⚠️ **Banks as a target segment** make **SSO / AD / Entra ID** a likely procurement
  requirement, which would supersede ADR-0002 (JWT).
- Updated: ADR-0004/0005/0006/0008/0009, architecture overview, product overview,
  feature status, README.

### 🇲🇲 Myanmar script handling decided
**Why:** the product ships into a Burmese-language market; "Burmese support" turned out to
hide two separate problems, and the less obvious one lands in the MVP.

- **[ADR-0009](../decisions/ADR-0009-myanmar-script-handling.md)** —
  **(a) Zawgyi→Unicode normalization at ingest is mandatory and in scope for the MVP.**
  Myanmar text exists in two incompatible encodings sharing the same Unicode block, so a
  Word/PDF authored in Zawgyi extracts as garbage **with no OCR involved at all** — it
  breaks display, search and matching. Decision: detect and convert at every ingest
  boundary; store normalized text **plus** the raw original and detected encoding.
  **(b) Burmese OCR is deferred** with a defined evaluation plan (real-CV sample, separate
  measurement of encoding vs. OCR error, WER thresholds for ship / assist / park). The
  image+scanned path must be **parkable** without affecting digital extraction or manual entry.
- Verified tooling: Google `myanmar-tools` is **Apache-licensed** (permissive → fine for
  closed-source commercial), but ⚠️ **has no official .NET client** — integration approach
  must be settled before Module 2 ingest.
- Verified that Tesseract's default `mya.traineddata` is weak; better community/research
  models exist, so the OCR engine + model must be **swappable via configuration**.
- ⚠️ **New finding for Module 2.6:** Burmese lacks consistent word spacing and Postgres has
  no Burmese FTS configuration — keyword search needs trigram (`pg_trgm`) or segmentation.
- Updated ADR-0008, module 02 spec, feature status.

### 🧩 Productization strategy + OCR/AI approach decided
**Why:** product is sold as one generic offering with paid customization; OCR needs to
work on customer-owned servers with no guaranteed internet.

- **[ADR-0007](../decisions/ADR-0007-productization-and-addons.md)** — generic core
  product; customization, deferred modules and integrations sold as **add-ons**.
  **Hard rule: a customization must never become a per-customer code branch** — it must
  land in configuration, a feature flag, an extension point, or shared core code.
  Forking per customer would break the maintenance economics of N separate installs.
  Noted that much of the spec (custom fields, approval chains, scorecards, report builder,
  RBAC) is *already* configuration-shaped and must be **built configurable from day one**.
- **[ADR-0008](../decisions/ADR-0008-document-extraction-and-ai-profiling.md)** —
  two-phase CV parsing. **Phase 1 (MVP):** local text extraction from PDF/Word/images with
  an OCR fallback for scanned PDFs — no network, nothing leaves the install. **Phase 2:**
  optional AI structuring into a form payload, behind a per-install API key; no key ⇒
  feature off, Phase 1 unaffected. **Key ownership is tiered** — our key (paid add-on) for
  hosted, customer's own key for on-prem.
  Guardrails: human confirmation before any AI-parsed PII is saved; provenance stored;
  bulk upload asynchronous; Smart Match ships an explainable local baseline first.
- ✅ **Blocking constraint cleared** — Module 2 is unblocked; ADR-0004, ADR-0006,
  module 02 spec and feature status updated accordingly.
- ⚠️ **New risks logged:** PDF/OCR library licences must be permissive (copyleft would be
  disqualifying for closed-source commercial software); Burmese-script OCR accuracy needs
  testing against real CVs; token cost needs metering if we supply the key.

### 📐 Delivery model, scoping and MVP decided
**Why:** clarified go-to-market — not a shared SaaS; each company gets its own server
install, reached by subdomain, sold as one-time licence + server + maintenance.

- **[ADR-0003](../decisions/ADR-0003-department-scoping.md)** — Hiring Managers see only
  their own department. Implemented as an explicit authorization predicate, *not* a global
  query filter (it's conditional per role, and `Candidate` is only indirectly departmental).
  Access modelled as a set — a manager may own multiple departments.
- **[ADR-0004](../decisions/ADR-0004-single-tenant-deployment.md)** — one instance +
  one database per company; subdomain routing; on-prem or vendor-hosted. Tenant plumbing
  **kept but demoted** to a misconfiguration safety net; department scoping is now the
  security-critical filter. Recorded the operational prerequisites (Docker, automated
  migrations, version endpoint, sizing guide, runbooks) — none exist yet.
- **[ADR-0005](../decisions/ADR-0005-commercial-model.md)** — recorded the commercial
  model and its engineering implications: the 10%/20% maintenance split is sound, but the
  automation in ADR-0004 is what makes it viable, and "maintenance" needs a contractual
  definition.
- **[ADR-0006](../decisions/ADR-0006-mvp-scope.md)** — **MVP = Modules 1, 2, 3, 5.**
  Build order 1→2→3→5; Module 5 last because it consumes the others' data.
  ⚠️ `ApplicationStageHistory` must be written from day one of Module 2 or analytics can
  never be back-filled.
- Added [architecture/deployment.md](../architecture/deployment.md)
- ⚠️ **New blocking constraint found:** OCR + Smart Match can't assume cloud APIs under
  on-premise deployment — customers who chose on-prem did so to keep CVs in-house.
  Needs its own ADR before Module 2.
- Updated auth doc, module 02 spec, product overview, feature status, README

### 🔄 Product pivot: agency → in-house
**Why:** new product overview redefines the product for a company's own talent
acquisition department. See [ADR-0001](../decisions/ADR-0001-pivot-to-inhouse.md).

- Established this `docs/` knowledge base as the project's single source of truth
- Wrote specs for all 7 in-house modules (`docs/product/modules/`)
- Defined the target in-house data model and marked agency entities for deletion
- Documented the required role-set revision (`Client` role out, `HiringManager`/`Approver` in)
- Documented pipeline vocabulary change: `SentToClient` removed, `Placed` → `Hired`
- Assessed design-system impact (tier badge + client feedback bar out; job page needed)
- Recorded ADR-0001 (pivot) and ADR-0002 (JWT, documented retroactively)
- Archived both source specs to `docs/reference/`
- **Code not yet touched** — removal plan in [MIGRATION-PLAN.md](MIGRATION-PLAN.md)

### ✅ Login & token issuance
**Why:** RBAC needs a way to actually issue tokens.

- `POST /api/auth/login`; `AuthService` (lookup + verify) and `JwtTokenService` (HS256, 8h)
- Password hashing via `IPasswordHasher<User>`; dev-only seeded admin gated on config
- Unknown email and wrong password both return 401 (no user enumeration)
- Added 6 tests (login paths + token claims)
- Packages added: `Microsoft.Extensions.Identity.Core`, `System.IdentityModel.Tokens.Jwt`

### ✅ Secure-by-default authorization
**Why:** security review found stub controllers with no `[Authorize]`.

- `FallbackPolicy = RequireAuthenticatedUser` — endpoints now require auth unless opted out
- `[Authorize]` added to Jobs/Candidates/Contracts; `[AllowAnonymous]` on Portal (by design)

### ✅ RBAC + real tenant isolation
**Why:** security review flagged 🔴 — `StubCurrentTenant` returned `Guid.Empty`, so
isolation wasn't real.

- JWT bearer authentication with full token validation
- Real `CurrentTenant` reading the `tenant_id` claim; **stub removed**
- Policies `AgencyStaff` / `AdminOnly`
- New `RecruitOps.Api.Tests` project: 4 tenant-isolation/authorization tests
- `CLAUDE.md` auth placeholder filled in
- Package added: `Microsoft.AspNetCore.Authentication.JwtBearer`

### 🐛 Fixed blank page on `/clients`
**Why:** Server Component fetched a **relative** URL; relative URLs have no origin in
Node, so `fetch` threw `TypeError: Failed to parse URL` and the render died silently.

- `lib/api.ts` made server-aware (absolute URL server-side, rewrite path in browser)
- Added `app/error.tsx` route error boundary so failures show a message, not a blank page
- Documented `API_INTERNAL_URL`
- Verified with a test asserting the server-side URL is absolute

### ✅ Client CRM list slice *(now superseded by the pivot)*
Demonstrated the `/feature` agentic workflow end-to-end: `Client`/`Contract` fields,
`ContractStatusCalculator`, `ClientListItemDto`, `ClientService`, wired `ClientsController`,
frontend clients table with tier badges + expiry countdown, 6 frontend + 3 domain tests.
**Status: to be removed** — see MIGRATION-PLAN.md.

### 🔐 Security: leaked GitHub token
**Why:** `.mcp.json` contained a real-looking PAT in plaintext (also malformed —
wrapped in `${}` as if it were a variable name).

- Replaced with a proper `${GITHUB_PERSONAL_ACCESS_TOKEN}` env-var reference
- ⚠️ **The original token must be revoked** — treat as compromised

### ✅ Initial scaffold
- .NET 8 Clean Architecture backend (`backend/src/{Domain,Application,Infrastructure,Api}` + tests)
- Next.js App Router frontend with the "Clear Pipeline" Tailwind theme
- Entity + enum stubs, `.gitignore`, `.env.example`, first architecture doc
