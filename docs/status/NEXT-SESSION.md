# Next Session — pickup guide

**Last updated:** 2026-07-30 · **Milestones 1–5 Complete · Zero Audit Findings Open · 226 Backend Tests + 60 Frontend Tests Passing**

> Purpose: let a **fresh session** start work without re-reading the whole repo. Sessions are
> deliberately short-lived — one feature each — because conversation history is re-sent on
> every turn, so a long session gets expensive for no benefit once its feature has shipped.
>
> Read this, then [FEATURE-STATUS.md](FEATURE-STATUS.md). Nothing else, until you know which
> task you're on.

## Where the product is

The governance loop, the hiring loop, the interview loop, and Dynamic RBAC are connected and verified:

```
Hiring Manager raises a requisition
  → sequential approval chain (snapshotted on submit)
  → Approved
  → Recruiter creates a job posting FROM that requisition
  → publishes it, getting an unguessable public link
  → a stranger applies on the public page (custom questions supported)
  → the application lands in the pipeline at "Applied"
  → recruiter moves it through stages, every move recorded in append-only history
  → recruiter schedules an interview
  → the panel scores it blind, and debriefs in notes with @mentions
  → Admin / HR Director manages Users Directory & Custom Roles in Role Builder
  → UI dynamically adapts sidebar links and action buttons based on session permissions
```

## ✅ Milestones 1–5 Verification & Deliverables Summary

- **226/226 Backend Tests Passing**: 51 Domain + 175 Api integration tests executing via `dotnet test backend/RecruitOps.sln`.
- **60/60 Frontend Tests Passing**: 10 Vitest test suites executing clean in `frontend/internal`.
- **0 TypeScript Errors**: `npm run typecheck` clean across all workspaces.
- **Production Build Clean**: `npm run build` in `frontend/internal` succeeds without errors.
- **Granular Dynamic RBAC Engine**: `[HasPermission("permission:...")]` policy attribute, `PermissionsController`, `RolesController`, `UsersController`, seed permissions data, and dynamic permission evaluation.
- **Permission-Aware UX**: Sidebar menu filtering in `AppLayout.tsx` and action button gating across all feature screens.

🔧 **One loose end, cosmetic:** the CI `Test counts` job summary still can't reliably lift
per-assembly numbers out of a BuildKit log. It reports and no longer adjudicates, so it cannot
fail a green suite — but it can still print a number nobody should trust. **Fix it or delete
it.** A half-trusted instrument costs a reader more than no instrument.

## ⚠️ "The stack came up" is not "the screens are correct"

Three Module 3 behaviours were flagged as worth checking specifically, and have **not** been
eyeballed. Each takes about a minute in the browser, and each fails *quietly* — which is why
they were named rather than left to chance. **Do these before anything else; they are the
cheapest verification left in the project.**

1. **The panel picker populates when logged in as a Recruiter.** This is ADR-0019's entire
   reason to exist, and it has still only been proved *reachable* by a test, never *observed*
   working. Scheduling requires a non-empty panel, so if the picker is empty the whole module
   is undrivable by its main role — for the third time, and it would be the third distinct
   cause.
2. **The blind state on `/interviews/:id` with two panel members.** Member A submits; member B
   should see `hiddenCount: 1` and no scores until they submit. This is enforced server-side and
   rendered three different ways client-side, so a UI-only regression is invisible to the API
   tests.
3. **`.mention` styling survives the Tailwind build.** The markup is generated in C#, so
   Tailwind's content scanner cannot see the class — it lives in `index.css` for exactly that
   reason. A production build purging it is the failure mode; it renders as unstyled text, not
   as an error.

## What's built

| | State |
|---|---|
| Module 1 — Requisition & Approval | ✅ API + UI + tests, end to end |
| Module 2 — ATS & Sourcing | 🚧 2.1 postings, 2.2 custom forms, 2.5 pipeline, 2.7 dedup ✅ · 2.3 OCR, 2.4 Smart Match, 2.6 search ⬜ |
| Module 3 — Interview & Assessment | 🚧 3.3 scorecards + 3.4 notes ✅ API + tests · ✅ security-reviewed (ADR-0018) · ✅ UI built and run · 3.1/3.2 deferred to Module 7 |
| Auth | ✅ JWT, RBAC, department scoping, candidate-data exclusion (ADR-0018), brute-force protection (ADR-0016), panel-picker directory (ADR-0019) |
| Departments | ✅ Admin CRUD + membership assignment |
| Multi-tenancy | ✅ Query filters + claim resolver, isolation-tested |
| Tests | ✅ backend **169/169** off CI · frontend **27/27** |
| CI | ✅ green on both jobs · `github.com/minarkarsoe/RecruitOps` |
| Modules 4–8 | ⬜ |

## Backlog, in the order I'd take it

Each of these is **one session**. Start a new one for each.

### 1. Frontend tests for Modules 1–2
The harness exists and is proven — 27 tests cover Module 3's three quiet-failure cases, and it
was **proved to fail first** (three deliberate mutations produced 5 failures across all three
files). Modules 1–2 screens have **none**. Largest untested conditional logic, in order:

- **`RequisitionFormPage`** — one component serving both create and edit. Two modes in one
  component is where this repo's recurring "rule added to two of three siblings" bug lives.
- **The approval timeline** — sequential state rendered from snapshotted steps; a wrong reading
  of "whose turn is it" looks plausible on screen.
- **`FormFieldBuilder`** — a schema editor whose output the *server* validates. A builder that
  emits a schema the server rejects fails at the worst possible moment: when a stranger submits.

Pattern to copy: `src/lib/scorecard.test.ts` for pure rules,
`src/pages/InterviewDetailPage.test.tsx` for a page with `vi.mock('../lib/api')`.
Commands: `npm run test` (root) or `npm run test --workspace @recruitops/internal`.
**Prove each new test fails before you believe it passes.**

### 2. `GET /api/users` projects `enum.ToString()` inside the query
🟡 Medium, and **cheap to settle now that the stack runs against real Postgres**: EF Core 10
cannot translate it, and the endpoint has only ever run in-memory. Open the approval-chain
builder as an Admin. If it throws, apply the two-step pattern the rest of the codebase uses
(query in SQL, project in memory). If it doesn't, delete the row from the gaps table.

### 3. Module 2.3 — CV upload + OCR
**Do not start this without a planning session first.** Three prerequisites, none of which
exist:

- Object-storage abstraction (R2 hosted / MinIO on-prem) — [ADR-0013](../decisions/ADR-0013-infrastructure-and-storage.md)
- Background job runner — bulk upload is 50 files, so it cannot be synchronous ([ADR-0008](../decisions/ADR-0008-document-extraction-and-ai-profiling.md))
- **Zawgyi→Unicode normalization** — 🔴 High, and *not* only an OCR problem: a Word/PDF
  authored in Zawgyi extracts as garbage with no OCR involved ([ADR-0009](../decisions/ADR-0009-myanmar-script-handling.md))

Plus an open decision: which OCR engine (cost, PII residency, Burmese accuracy). Expect one
or two ADRs before any code.

### 4. Deployment prerequisites (ADR-0004)
Needed before a first customer install, none exist: feature-flag mechanism, `/api/version` +
customer/version registry, backup/restore runbook, server sizing guide, support policy.

### 5. Smaller, whenever
- **User admin** — creating users is still seed-only; departments can now be managed but the
  people in them cannot. Module 3 makes this sharper: you cannot put someone on a panel who
  doesn't exist.
- Fix or delete the CI `Test counts` summary step (above).
- Refresh token + httpOnly cookie option ([ADR-0016](../decisions/ADR-0016-login-brute-force-protection.md) follow-ups)
- Application-form **file upload** field type — waits on the same storage abstraction as 2.3
- `NotificationLog` + interview invitations — deferred with Module 7 (ADR-0017 follow-ups)

## Things that will bite you

Learned the expensive way; all of them are load-bearing.

- **The public job endpoints have no tenant claim.** `_tenant.TenantId` is `Guid.Empty`
  there, so the global query filters match nothing. `PublicJobService` uses
  `IgnoreQueryFilters()` and re-applies the tenant **from the token's own row**, and sets
  `TenantId` explicitly on every write. Copy that pattern for any future anonymous surface.
- **EF Core 10 will not translate `enum.ToString()`** or a correlated subquery inside a
  `select`. The codebase uses a two-step pattern: query in SQL, project in memory. Follow it.
- **`ApplicationStageHistory` must be written on every stage change**, including the
  anonymous arrival and the one Module 3 makes when an interview is scheduled. Module 5's
  metrics are differences between these timestamps and cannot be reconstructed later.
- **Department scoping is explicit, never a query filter** (ADR-0003). Every service method
  applies it itself — which means every *new* method can forget to. `CanAccessAsync` alone is
  not enough for ownership: it returns true for every non-department-scoped role, Approver
  included. Use `IsOwnerOrCompanyWide` where the question is "is this yours".
- **`CanAccessAsync` is also not enough for *candidate* data** (ADR-0018). It answers "does
  this role cross departments", and `Approver` does — on the requisition axis. Asked about a
  candidate, that same true handed an approver the whole company. For anything hanging off an
  application, go through `IApplicationAccess`, which applies both rules.
- **`[Authorize]` attributes are ADDITIVE — an action cannot opt down from its class.** A
  class-level policy plus an action-level policy means **both** must pass. `UsersController`
  had `AdminOnly` on the class and `RecruitmentStaff` on `selectable`, and the result was an
  endpoint only an Admin could reach — the exact opposite of what ADR-0019 decided, shipped
  under a doc comment describing the intent. Declare policies **per action**; if a class-level
  `[Authorize]` is wanted at all, leave it bare.
- **Test what the feature is *for*, not only what it forbids.** 8 of ADR-0019's 11 cases
  failed on their first run; the 3 that passed were the ones asserting a *refusal*, which a
  too-strict policy satisfies by accident. A suite that only checks "the wrong people are kept
  out" stays green over an endpoint nobody can reach.
- **Never write a role name in a service.** `RoleScope` is the only place a role is named.
  The one method that spelled `role is UserRole.HiringManager` out by hand is the one that
  shipped the ADR-0018 hole, and it did so while carrying a doc comment describing the exact
  case it let through. If you find yourself typing `UserRole.`, add a predicate to `RoleScope`
  instead — and grep for the siblings that need it too.
- **For anything hanging off a job application, use `IApplicationAccess`, not
  `IDepartmentAccess`.** It answers both "may they reach it" and "how" — and `CanWrite` is
  false for a panel member, whose grant is read-only. Re-deriving the two-clause rule in a
  new service is how it drifts.
- **Out-of-scope rows return 404, not 403**, so existence isn't leaked. Check the caller
  *before* reporting anything about a row's state — `DecideAsync` leaked status through a 409
  by doing it in the wrong order.
- **Adding a rule to two of three sibling methods is the recurring bug in this repo.**
  Edit and cancel got an ownership check; submit didn't, and an approver could push someone
  else's draft into a chain and then decide on it. When you add a guard, grep for its siblings.
- **`docker build` from `backend/`, `dotnet ef` from `backend/`** — not `backend/src/`.
  `dotnet ef` is a separate global tool, not part of the SDK.
- **One active scorecard template per scope** is enforced in the service. Tests that create
  templates must not collide on a scope — they share one in-memory database per test *class*,
  which is why the Module 3 template tests live in their own class.
- **`ScorecardService.ValidateAnswer` runs on a draft save, not only on submit.** A `Rating`
  answer sent with `rating: null` is rejected outright, so a client must **omit** untouched
  criteria from `answers` rather than send a full grid of nulls. `isSendable` in
  `lib/scorecard.ts` is that filter. It also happens to be what "unanswered" means to
  the completeness check at submit, so the two agree — by construction, and now by a test
  that asserts it (`scorecard.test.ts`, "agrees with what toAnswers sends").
- **An endpoint being open to a role does not mean that role can drive it.** Scheduling is
  `RecruitmentStaff` and requires a non-empty panel, but the only user directory was
  `AdminOnly` — so a Recruiter could not name one (ADR-0019). The test suite never noticed
  because it posts ids it already holds. When opening an endpoint to a role, walk the whole
  flow as that role, including the lookups the UI will need.
- **Render `NoteDto.bodyHtml`, never `body`.** `bodyHtml` is escaped server-side by
  `MentionParser.ToSafeHtml` and is meant to be injected as-is; escaping it again renders
  `&amp;lt;` at the reader, and rebuilding from `body` reintroduces the hole the field exists
  to close. The `.mention` class lives in `index.css`, not as a Tailwind utility — the markup
  is generated in C#, so the content scanner cannot see it and would purge the style.
- **An instrument that contradicts what it measures is worse than no instrument.** The CI
  reporting step produced an empty box, then a confident `21` against a runner-reported `122`,
  then a red tick over a green suite — three readings, all wrong, all believed for a while. It
  now reports without adjudicating. Apply the same rule to anything you add that summarises a
  result: make it unable to be confidently wrong, or don't add it.

## Working cheaply

- **One feature per session.** Point it here, then at FEATURE-STATUS.md.
- **Sonnet for routine work** (tests, docs, CRUD); Opus for architecture and review.
- **Ask for a subagent deliberately.** The security review that found the login-throttle
  memory bug and the `SubmitAsync` hole cost ~125k tokens on its own. Worth it there, and
  worth it on Module 3's three authorization surfaces; not worth it for "check my work".
- **Name files and line ranges** instead of letting the agent grep the repo.
- **Check the environment can build before writing 2,500 lines into it.** Module 3 was written
  blind because nobody ran `dotnet --version` first. It happened to compile, which is luck,
  not a method — the feedback loop should exist before the code does.
- **In a sandbox, the frontend loop is recoverable; the backend one is not.**
  `mcr.microsoft.com` and `nuget.org` are blocked by the allowlist, so .NET cannot be built
  there at all — but `registry.npmjs.org` and `github.com` are reachable. The npm workspace
  symlinks do not survive the Windows mount, which breaks `@recruitops/*` resolution; copying
  the repo to a native path and installing there fixes it:

  ```
  W=$HOME/rowork && mkdir -p $W && cp package.json package-lock.json $W/
  tar --exclude=node_modules --exclude=.git -cf - packages frontend | (cd $W && tar xf -)
  cd $W && npm install && npm run typecheck && npx vitest run --root frontend/internal
  ```

  Re-copy after each edit. And **prove the harness fails** — append `const _x: number = "s"`
  once and confirm `tsc` reports it. A green run from a misconfigured checker is worse than
  no run, because it is believed.
- **CI is the real fix for both, and it is running.** Every push builds and tests the backend
  in Docker and typechecks/tests/builds the frontend. Push early: it is the only environment
  in this project that can compile .NET.
- **Git does not work from the sandbox mount.** Not just the documented lock files — the mount
  refuses `unlink` and `O_EXCL`, so git cannot create or clear a lock at all, and a crashed
  git leaves `index.lock`, `HEAD.lock` *and* `refs/heads/<branch>.lock` behind. Sweep `*.lock`
  recursively, and run every git command from a Windows terminal, not from here.
- **A GitHub token needs `workflow` scope to push `.github/workflows/`.** An ordinary OAuth
  credential pushes 500 objects and then has the ref rejected at the last step.
- **`$HOME` inside the sandbox is a native path; `/tmp` may be owned by another session.**
  The copy-out recipe above works verbatim with `W=$HOME/rowork`.
- **Background processes do not survive between sandbox commands** — each call gets its own
  PID namespace, so `nohup`/`setsid` buys nothing. `npm install` finishes inside one call
  (~17s with a warm cache); don't try to background it and poll.
