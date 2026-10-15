# Next Session — pickup guide

**Last updated:** 2026-10-15 · **RBAC security fixes + operational foundations · backend 314/314 · frontend 64/64**

> Purpose: let a **fresh session** start work without re-reading the whole repo. Sessions are
> deliberately short-lived — one feature each — because conversation history is re-sent on
> every turn, so a long session gets expensive for no benefit once its feature has shipped.
>
> Read this, then [FEATURE-STATUS.md](FEATURE-STATUS.md). Nothing else, until you know which
> task you're on.

## Where the product is

The governance, hiring and interview loops work end to end against real Postgres, driven in a
browser as each role (2026-10-15):

```
Hiring Manager raises a requisition → sequential approval → Approved
  → Recruiter creates + publishes a posting → a stranger applies on the public page
  → pipeline (append-only stage history) → interview scheduled (panel picker works as a Recruiter)
  → blind scorecards (verified with two panel members) → debrief notes with @mentions
Admin manages users and roles; the UI shows each role exactly what the API lets it do
```

**MVP (ADR-0006) is 10 of 19 features built.** Module 1 ✅ 4/4 · Module 2 4/7 (no CV upload/OCR,
Smart Match, search) · Module 3 2/4 (no scheduling-with-calendar, no invitations — both need email)
· **Module 5 Reporting 0/4.**

## ⛔ Waiting on the product owner

1. **ADR-0021 — what a custom role means.** Custom roles can be built but **not assigned** until
   one of A (base role + extras — recommended), B (scope + matrix) or C (no custom roles) is chosen.
   A or B also needs the grantor ceiling listed in the ADR.
2. **GitHub write access for Claude.** Pushes from Claude sessions were refused (403: the Claude
   GitHub App lacks access to `minarkarsoe/RecruitOps`). The 2026-10-15 work was handed over as a
   git bundle; confirm it reached `claude/dreamy-babbage-0zfr37`.
3. Open from the roadmap: SSO for the first bank customers? Burmese UI? Team size and date?

## Backlog, in the order I'd take it

Each is **one session**.

### 1. Phase 1 platform: email + background jobs + object storage
Unblocks Module 3.1/3.2 (scheduling, invitations), Module 2.3 (CV upload), Module 4, notifications
and password reset. **Each needs a package decision first** (CLAUDE.md: ask before adding one):
SMTP client (MailKit vs `System.Net.Mail`), job runner (Postgres-backed hosted service vs Hangfire
vs Quartz), S3 client for R2/MinIO (ADR-0013). A Postgres-backed outbox + `BackgroundService` needs
no package and fits one-instance-per-company.

### 2. Public site: script CSP with a nonce
The SPA's CSP is done and verified under real nginx. Next.js renders inline hydration scripts, so
`frontend/public` needs middleware that mints a per-request nonce and sets `script-src 'nonce-…'`.
Verify the way the SPA was: drive the page (and submit an application) with a
`securitypolicyviolation` listener, expecting zero.

### 3. Audit log
Module 7's `AuditLog`: who did what, when. Start with role/user changes, approvals, stage moves,
offers. Needs a migration (propose it; a human applies it — CLAUDE.md).

### 4. Module 5 — Reporting
ADR-0006: the module that sells the product to the buyer. `ApplicationStageHistory` exists from day
one, so time-to-hire / conversion can be computed now.

### 5. Frontend tests for Modules 1–2
`RequisitionFormPage` (two modes), the approval timeline, `FormFieldBuilder`. Pattern:
`lib/scorecard.test.ts`, `pages/InterviewDetailPage.test.tsx`. Prove each test fails first.

### 6. Smaller, whenever
- Over-broad seed grants and `GET /api/users/{id}` width (FEATURE-STATUS gaps) — before ADR-0021 B
- `Interviewer` role: no policy admits it
- Role name > 200 / delete-with-inactive-holders return 500
- Refresh token + httpOnly cookie (ADR-0016 follow-ups)
- `/api/version` → an actual customer/version registry; backup/restore + upgrade runbooks; sizing guide

## Things that will bite you

Learned the expensive way; all of them are load-bearing.

- **A client check that fails open hides every mismatch behind it.** `hasPermission` returned
  true when the permission list was missing — and the API never sent one, so the permission-aware
  UI had never run against reality. Making it fail closed immediately exposed six places where the
  seed matrices denied what the API's role policies allow. **Default every "may they?" check to
  no**, and pin the UI's gate to the server's rule with a test that reads the live policy
  (`SeedMatrixMirrorsPoliciesTests`).
- **Never parse an authorization value with `Enum.TryParse`.** It accepts any member name, any
  numeric string (`"0"` was SuperAdmin), values no member has, and comma-joined names. Match exact
  names against an explicit allow-list. Grep for `TryParse<UserRole>` before adding a role path.
- **The test suite never validated a token.** `CustomWebAppFactory` swaps the JWT scheme for a
  header-based test handler, so token-lifetime bugs are invisible to every suite except
  `SessionRevalidationTests`, which switches the real scheme back on. Use that pattern for any
  test about what a token proves.
- **"Dev-only" seeding can hide a missing production path.** Reference data (permissions, roles)
  lived in the Development seeder, so a production install had none — and no way to create a user.
  Ask of every startup step: what does a fresh customer install get?
- **Run the stack as each role, not as Admin.** Admin sees everything, so it proves nothing about
  scoping. Every finding of 2026-10-15 started with logging in as a Hiring Manager.

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
- **Claude Code on the web (2026-10-15): run natively, not via Docker.** Docker Hub's CDN is
  blocked, so `docker compose` cannot pull `postgres`/`node`/`nginx` or the `# syntax=` frontend.
  `mcr.microsoft.com`, NuGet and npm work. The recipe: start `dockerd`, pull
  `mcr.microsoft.com/dotnet/sdk:10.0`, `docker cp` its `/usr/share/dotnet` to `/opt/dotnet`; start
  the preinstalled Postgres 16 (`pg_ctlcluster 16 main start`); `dotnet run` the API with
  `ConnectionStrings__Default`/`Jwt__*`/`Seed__*` env vars; `npm run dev:internal`, or
  `npm run build` + `npx vite preview` (it reuses the `/api` proxy) to test the production build.
  Note `pkill -f RecruitOps.Api` kills your own shell — use a pid file.
- **Older note — a Windows-mounted sandbox:** the npm workspace symlinks did not survive the
  mount; copying the repo to a native path fixed it:

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
