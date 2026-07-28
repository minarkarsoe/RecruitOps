# Next Session — pickup guide

**Last updated:** 2026-07-28 · **Next task: push, read the count off CI (expect 167), get the
ADR-0019 change reviewed, then run the stack**

> Purpose: let a **fresh session** start work without re-reading the whole repo. Sessions are
> deliberately short-lived — one feature each — because conversation history is re-sent on
> every turn, so a long session gets expensive for no benefit once its feature has shipped.
>
> Read this, then [FEATURE-STATUS.md](FEATURE-STATUS.md). Nothing else, until you know which
> task you're on.

## Where the product is

The governance loop, the hiring loop **and now the interview loop** are connected:

```
Hiring Manager raises a requisition
  → sequential approval chain (snapshotted on submit)
  → Approved
  → Recruiter creates a job posting FROM that requisition
  → publishes it, getting an unguessable public link
  → a stranger applies on the public page (custom questions supported)
  → the application lands in the pipeline at "Applied"
  → recruiter moves it through stages, every move recorded in append-only history
  → recruiter schedules an interview          ← Module 3, NEW
  → the panel scores it blind, and debriefs in notes with @mentions
```

All of it is **built and tested on the API**, Module 3 has been **security reviewed**
(ADR-0018), and Module 3 now **has a UI** — five screens, type-checked, never run.

## ✅ The backend compiles. CI is live.

Pushed to `github.com/minarkarsoe/RecruitOps` on 2026-07-28 as ten commits, and **CI's first
run was green on both jobs** — so the ADR-0018 fix and the ADR-0019 endpoint, written across
three SDK-less sessions, do compile. The "written but never compiled" pattern is over: every
push now runs `docker build --target test ./backend`.

### 🚨 One thing still unverified: the test *count*

Green means the build and the test command both exited 0. It does **not** yet mean the new
tests ran — that has been this repo's recurring trap, and the first run finished in 51s, which
is fast. A `Test counts` step now lifts the `Passed!` lines out of the BuildKit output into
the job summary.

**Domain is now real: 39/39, read off run #3's raw log.** The 15 mention-parser tests
demonstrably executed. **The Api count has still never been read** — `dotnet test` on a `.sln`
emits one summary *per test project*, and only the Domain one has been looked at.

🐛 **The `Test counts` step itself was broken** and rendered run #3 as an *empty* code block:
it grepped for `Passed!` (Microsoft Testing Platform) while this solution runs on **VSTest**
(`Test Run Successful.` / `Total tests: N` / `Passed: N`), and its `|| echo` fallback hung off a
pipeline ending in `sed`, so it exited 0 and never fired. An empty report looks like a report.

🧨 **And the count could not be attributed to an assembly.** Run #3 reads
`Starting: RecruitOps.Api.Tests` and then, 48ms later, `Total tests: 39`. That is *Domain's*
summary — a `.sln` run spawns one vstest run per project and interleaves their stdout (two
`A total of 1 test files matched` lines give it away) — but it is indistinguishable at a glance
from the Api project contributing zero. Fixed three ways on 2026-07-28: **one `RUN` per test
project** in the Dockerfile (two summaries, two exit codes),
**`RunConfiguration.TreatNoTestsAsError=true`** on each (a zero-test project no longer exits 0),
and `ci.yml` now **counts cases per assembly itself** off the `Passed RecruitOps.<X>.Tests.`
lines and **fails the job** if either is zero, naming which one. Replaying run #3's log through
the new script reports `Api=0` and exits 1 — the harness was proved to fail first, as always.

**Push and read the Api number off the next run.** It will be a two-row table at the top of the
job summary now, not a number you have to find.

⚠️ **Do not count with Ctrl+F in the Actions log viewer.** It is virtualised — it searches only
the portion currently rendered, so its match count is a lower bound unrelated to the real
number. Searching run #3 for `Passed RecruitOps.Api.Tests.` returned "about 40" for something
that should occur 117 times, and *neither* number could be trusted from that box. To settle a
log by hand, download the archive (run → **…** → *Download log archive*) and run
**`.\count-tests.ps1 -Path <log>`** at the repo root: it reads the whole file, counts per
assembly, compares against what FEATURE-STATUS.md claims, and warns when a log has fewer than
two runner summaries and is therefore truncated.

> If the next run still reports `Api=0`, that is no longer a reporting bug — the Api assembly
> genuinely executes nothing, and *that* is the session. The suspects, in order: the test host
> aborting during `CustomWebAppFactory`'s host build (which would take every class fixture with
> it), and `WebApplicationFactory<Program>` failing to locate the app's content root inside the
> container, where the source layout differs from a local checkout.

- **128 API / 167 total** → Module 3, ADR-0018 *and* the new ADR-0019 tests all executed;
  delete the "unrun" warnings
- **117 API / 156 total** → the ADR-0019 file did not compile in
- **68 API / 92 total** → nothing new executed, and the cache flags are not doing their job

### ✅ Done 2026-07-28 (this session): ADR-0019 has tests

`backend/tests/RecruitOps.Api.Tests/UserDirectoryTests.cs`, **11 cases**. The one that matters
asserts **both halves in one test** — a Recruiter gets 200 on `/api/users/selectable` and 403 on
`/api/users` — because split in two, a later edit that widened the full directory would leave a
green test named "a recruiter can read selectable" standing over the hole. The no-email check
runs against the **raw JSON**, not a deserialised `SelectableUserDto`: reading into the DTO would
drop an email property silently and report green. Also pinned: an Approver **is** selectable
(ADR-0018 removed standing reach, not panel eligibility), the picker is not department-scoped,
`Role` survives as a string, and the tenant filter still empties the list for another tenant.

⚠️ **Written in the same SDK-less environment, so never compiled.** And ADR-0019 is an
authorization change: **a human still has to read it** (CLAUDE.md). A test suite written by the
same author as the endpoint is not that review.

### What the ADR-0018 fix was, in one paragraph

An `Approver` is not department-scoped (ADR-0003 — an approval chain crosses departments). That
was one boolean, and every candidate-facing service asked it, so `CanAccessAsync` said yes for
every department and an Approver reached **every application in the company**: notes read *and*
write, every interview, every submitted scorecard (not a participant → not blinded), any
pipeline board, any stage history. Worse, `NoteService` re-derived the reach rule by hand as
`role is UserRole.HiringManager`, so `@finance.approver` resolved — the exact handle its own doc
comment named as the thing it prevented. [ADR-0018](../decisions/ADR-0018-approver-candidate-data-exclusion.md)
splits scoping and candidate reach into two questions answered in one place, `Domain/RoleScope.cs`.

### Then run the UI against it

`docker compose up --build`. The Module 3 screens have only ever been type-checked; the first
real test is whether the shapes in `packages/types` match what the API actually serialises.
Worth checking specifically: the blind state on `/interviews/:id` with two panel members, and
that `.mention` styling survives the Tailwind build (the markup comes from C#, so the scanner
cannot see the class — it is defined in `index.css` for that reason).

## ✅ Done 2026-07-28 (later): CI + frontend tests

Both were on this list; neither is any more.

- **`.github/workflows/ci.yml`** — backend `docker build --target test` (with
  `--progress=plain --no-cache-filter=build,test`, for the reason in FEATURE-STATUS) and
  frontend `npm ci` → typecheck → test → build, on every push and PR to `main`.
  ⚠️ **Inert until a git remote exists.** Creating one is now the highest-value ten minutes
  available: it is what ends the "written but never compiled" pattern for good.
- **Vitest in `frontend/internal`** — **27 tests, 27 passing**, over the three quiet-failure
  cases this file named: the blind rule's three renderings, the scorecard payload filter, and
  `NoteBody`'s HTML injection. The payload rules moved to `lib/scorecard.ts` so they could be
  asserted directly; `InterviewDetailPage` imports them and is otherwise unchanged.
- The harness was **proved to fail** first — three deliberate mutations produced 5 failures
  across all three files. Do the same to anything you add.

Commands: `npm run test` (root, all workspaces) or `npm run test --workspace @recruitops/internal`.
On the Windows mount the workspace symlinks don't survive — copy to a native path first, per
"Working cheaply" below.

## What's built

| | State |
|---|---|
| Module 1 — Requisition & Approval | ✅ API + UI + tests, end to end |
| Module 2 — ATS & Sourcing | 🚧 2.1 postings, 2.2 custom forms, 2.5 pipeline, 2.7 dedup ✅ · 2.3 OCR, 2.4 Smart Match, 2.6 search ⬜ |
| Module 3 — Interview & Assessment | 🚧 3.3 scorecards + 3.4 notes ✅ API + tests · ✅ security-reviewed (ADR-0018) · ✅ UI (type-checked, **never run**) · 3.1/3.2 deferred to Module 7 |
| Auth | ✅ JWT, RBAC, department scoping, candidate-data exclusion (ADR-0018), brute-force protection (ADR-0016) |
| Departments | ✅ Admin CRUD + membership assignment |
| Multi-tenancy | ✅ Query filters + claim resolver, isolation-tested |
| Tests | ⚠️ backend ≈167 counted from source — **count not yet read off a run; the 11 new ADR-0019 cases have never compiled** · frontend **27/27 passing** |
| CI | ✅ green on both jobs, first run 2026-07-28 · `github.com/minarkarsoe/RecruitOps` |
| Modules 4–8 | ⬜ |

## Backlog, in the order I'd take it

Each of these is **one session**. Start a new one for each.

### 1. Push, read the count off CI, review ADR-0019, then run the stack → **see above**
The test is written; what remains is verification, and none of it can happen in the sandbox.
Push from a Windows terminal, read the `Test counts` step (**expect 167**), get a human onto the
ADR-0019 diff, and then **run the stack** (`docker compose up --build`) — the Module 3 screens
have still only ever been type-checked, and the first real test is whether `packages/types`
matches what the API actually serialises. Worth checking specifically: the blind state on
`/interviews/:id` with two panel members, that `.mention` styling survives the Tailwind build,
and that the panel picker on the scheduling form is actually populated when logged in as a
Recruiter — that is ADR-0019's whole reason to exist, and it has never been seen working.

### 2. More frontend tests (the harness now exists)
27 tests cover Module 3's three quiet-failure cases. **Modules 1–2 screens have none** —
`RequisitionFormPage` (one component serving create and edit), the approval timeline, and
`FormFieldBuilder` (a schema editor whose output the server validates) are the next-largest
pieces of untested conditional logic. Pattern to copy: `src/lib/scorecard.test.ts` for pure
rules, `src/pages/InterviewDetailPage.test.tsx` for a page with `vi.mock('../lib/api')`.

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
