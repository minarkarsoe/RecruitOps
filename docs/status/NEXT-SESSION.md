# Next Session — pickup guide

**Last updated:** 2026-08-21 · **Backend 612/612 · Frontend 342/342 · typecheck clean**
· **ADR-0026 is complete** — steps 1–4 built, steps 1–3 security-reviewed · the product sends email
(Module 3.2 invitations) and no longer keeps a recruiter's fifty CVs in process memory
· All seven modules have a drawn UI (25 screens)

> Purpose: let a **fresh session** start work without re-reading the whole repo. Sessions are
> deliberately short-lived — one feature each — because conversation history is re-sent on
> every turn, so a long session gets expensive for no benefit once its feature has shipped.
>
> Read this, then [FEATURE-STATUS.md](FEATURE-STATUS.md). Nothing else, until you know which
> task you're on.

> ⚠️ **This file and FEATURE-STATUS.md contradicted each other for five days**, and both
> contradicted the code — this one said Module 2.6 search and 2.3 OCR were unbuilt while
> `SearchService` and `BulkResumeService` were in the tree and under passing tests; the other
> said Module 5 had not started while `AnalyticsController` shipped. A session that trusted
> either would have rebuilt working software. Both were re-derived from the code on 2026-08-18.
> **When you finish a feature, update these two together, from the code.**

## Where the product is

The governance loop, the hiring loop, the interview loop, CV ingestion + AI profiling,
reporting, trigram search, dynamic RBAC and feature flags are connected and verified:

```
Hiring Manager raises a requisition
  → sequential approval chain (snapshotted on submit)
  → rejected rounds send it back to revise and resubmit (ADR-0023)
  → a later approver may approve forward, never reject forward (ADR-0024)
  → Approved
  → Recruiter creates a job posting FROM that requisition
  → publishes it, getting an unguessable public link (Sqid, ADR-0020)
  → a stranger applies on the public page (custom questions supported)
  → recruiter bulk-uploads PDF/Word CVs onto a durable queue; local extraction, Zawgyi normalised
  → AI profiles skills and writes a Burmese/English summary — IF a key is configured
  → recruiter searches candidates via pg_trgm
  → application lands in the pipeline at "Applied"
  → recruiter moves it through stages, every move in append-only history
  → recruiter schedules an interview
  → the candidate is emailed the slot in the company's own time zone (ADR-0026)
  → panel scores it blind, and debriefs in notes with @mentions
  → Analytics renders time-to-hire, funnel and bottleneck metrics
  → Admin manages users, custom roles, feature flags, /api/version
```

**Where it stops.** One message leaves the building — the interview invitation. Everything else
a candidate should hear about (an offer, a reminder, a rejection) still has no handler, and
**nothing in the UI shows whether a message actually arrived**: the delivery log is drawn in
`design/internal/channels.html` and reads from a table no screen queries yet. So "was this
candidate told?" is answerable by the database and by nobody using the product.

## What's built

| | State |
|---|---|
| Module 1 — Requisition & Approval | ✅ API + UI + tests, end to end. Rounds (ADR-0023) and senior skip-ahead (ADR-0024) landed 2026-08-16 |
| Module 2 — ATS & Sourcing | ✅ 2.1–2.7 all built. Postings, custom forms, pipeline, dedup, bulk CV ingestion (**rewritten 2026-08-21 onto the durable queue**), AI profiling behind a key, pg_trgm search |
| Module 3 — Interview & Assessment | 🚧 3.3 scorecards + 3.4 notes ✅ API + UI + tests · security-reviewed (ADR-0018) · **3.2 invitations ✅ built 2026-08-20** (schedule and reschedule queue an email; cancel suppresses it) · **3.1 calendar free/busy still unbuilt** — no calendar client exists |
| Module 5 — Reporting & Analytics | ✅ API + UI. ⚠️ Built to the *old* metric definitions — the 2026-08-18 spec moved both clocks |
| Modules 4, 6, 8 | ⬜ code · ✅ spec + design (13 screens drawn 2026-08-18) |
| Module 7 — Settings | 🚧 RBAC ✅ · integrations ⬜ |
| Auth | ✅ JWT, dynamic RBAC, department scoping, candidate-data exclusion (ADR-0018), brute-force protection (ADR-0016), panel-picker directory (ADR-0019) |
| Multi-tenancy | ✅ Query filters + claim resolver, isolation-tested |
| Delivery (ADR-0004) | ✅ compose prod, `/api/version`, feature flags, sizing guide, runbook · ✅ in-process job runner (ADR-0026) · ⚠️ **every install now needs `Smtp:*` configured** or nothing is delivered |
| Background jobs (ADR-0026) | ✅ **all four steps built.** Queue + tenant seam + mail worker + SMTP + invitation handler (security-reviewed, nothing found) + bulk CV worker · ⬜ no delivery-log UI · ⬜ Module 4/5/8 handlers · ⬜ step 4 not yet security-reviewed |
| Tests | ✅ backend **612/612** (62 domain + 550 api) · frontend **342/342** across 43 files |
| Design | ✅ 25 static screens, all seven modules — `design/internal/index.html` |

## ⚠️ "The stack came up" is not "the screens are correct"

Three Module 3 behaviours were flagged as worth checking specifically, and **still have not
been eyeballed** — carried across four updates of this file now. Each takes about a minute in
the browser, and each fails *quietly*. **These remain the cheapest verification left in the
project.**

1. **The panel picker populates when logged in as a Recruiter.** This is ADR-0019's entire
   reason to exist, and it has only ever been proved *reachable* by a test, never *observed*
   working. Scheduling requires a non-empty panel, so if the picker is empty the whole module
   is undrivable by its main role.
2. **The blind state on `/interviews/:id` with two panel members.** Member A submits; member B
   should see `hiddenCount: 1` and no scores until they submit. Enforced server-side and
   rendered three ways client-side, so a UI-only regression is invisible to the API tests.
3. **`.mention` styling survives the Tailwind build.** The markup is generated in C#, so
   Tailwind's content scanner cannot see the class — it lives in `index.css` for that reason.
   A production build purging it renders unstyled text, not an error.

## Backlog, in the order I'd take it

Each of these is **one session**. Start a new one for each.

### 0. 🔴 ADR-0025 step 3 — put the apps on the design kit's tokens

**Decided 2026-08-21: `design/` is the source of truth for UI** (now written into CLAUDE.md). That
rule is inert until this lands, because the kit and the running apps do not share a token
vocabulary — the *class names* differ, so a screen copied from the kit renders unstyled rather
than off-brand.

| | Design kit (`design/internal/ds.js`, V1.0) | Shipped (`packages/ui/tailwind-preset.js`, Clear Pipeline) |
|---|---|---|
| brand | `brand-700` `#0F766E` | `primary-700` `#0B5654` |
| neutral | `ink-900…400`, `canvas`, `line`/`line-strong` | `ink-900/600/400`, `surface-0/50`, `line-200`, a full `zinc` ramp |
| status | `positive` `warn` `critical` `info` | `success` `warning` `danger` `info` `accent` |
| type | Inter + JetBrains Mono, **no display face** | Inter + IBM Plex Mono + Bricolage Grotesque |
| radius | 6 / 8 / 10 / 12 / 16 / 20 | 8 / 12 / 16 / full |

### ✅ 3a — the preset (done 2026-08-21)

`packages/ui/tailwind-preset.js` is V1.0: it is now a copy of `design/internal/ds.js` plus a
**fenced compatibility block** that aliases every old name onto its V1.0 equivalent. So the apps
are already **on V1.0 colours** — only the class *names* are old, and they can be migrated an area
at a time without the app being unstyled in between.

It also **fixed the 163 dead classes**. Before: `text-ink-500` (57 uses), `text-ink-700` (39),
`text-ink-800` (20), `border-line-300` (28), `bg-surface-100` (13), `border-line-100` (5),
`bg-surface-200` (1) were **ABSENT** from the built stylesheet — the old preset defined `ink` at
900/600/400, `surface` at 0/50 and `line` at 200 only, so those elements silently inherited their
parent's colour. Re-verified after the change: all seven **PRESENT**.

### ✅ 3b — base CSS and fonts (done 2026-08-21)

`frontend/internal/src/index.css` and `frontend/public/app/globals.css` ported from `ds.css`:
focus ring, `::selection`, `.mm` Burmese line box, `.tnum`, mono ligature suppression, the
approval-chain rail, skeletons. Body type is **14px/20px** in the internal app (was the browser's
16px with a global `line-height: 1.7`, which made every English row 27px tall) and 15px/22px on
the public surface, which is read on a phone.

**Bricolage Grotesque and IBM Plex Mono are gone**, and the `<link>` in `index.html` /
`layout.tsx` was the real source — the CSS `@import` edit alone had left both faces still
downloading. Verified in the browser: `document.fonts` now lists Inter, JetBrains Mono and Noto
Sans Myanmar only, from a single Google Fonts request.

### ✅ 3c — `packages/ui` (done 2026-08-21)

**0 compat usages left in `packages/ui/src`** — 133 migrated across 13 components, built against
`design/internal/components.html` rather than renamed. Verified by reading computed styles in the
browser, not by reading the diff:

| | Measured | Kit |
|---|---|---|
| `StatusPill` warn | `#FFFBEB` bg / `#B45309` text / 24px / 12px / 500 | warn-50, warn-700, h-6, text-xs, medium |
| `StatusPill` neutral | canvas + 1px line border + ink-600 | the only pill with a border, because it has no tint |
| `Button` primary | `#0F766E` / 36px / 14px / 500 / r10 | brand-700, h-9, text-base, medium |
| `Input` | 36px / r10 / 14px / line border | h-9, rounded-md, text-base |
| `Card` | white / r12 / 20px pad / line border | rounded-lg, p-5 |

Shape changes the rename would have missed, each from the kit: pill tint `-100`→`-50` and
13px/semibold→12px/medium; button `h-10`→`h-9`, 15px/semibold→14px/medium, primary base
`brand-600`→`brand-700` with `active:` states; input/select `h-10`→`h-9`, `rounded-sm`→`rounded-md`,
soft `/20` focus ring; **table header lost its uppercase micro-caps** and rows are 44px per the
kit; card title dropped from 19px display to 14px semibold.

**All six pill contrast pairs were re-measured, not assumed** — `-50` tints improve every one:
ink-600/canvas 7.24 · brand-800/brand-50 7.27 · info-700/info-50 6.16 · critical-700/critical-50
5.91 · positive-700/positive-50 5.21 · warn-700/warn-50 **4.84** ← least headroom.

12 design-system tests were updated to the V1.0 names and 2 added (the `Shortlisted` contrast pair,
and one asserting the neutral pill keeps its border). Frontend **344/344**.

> 🔎 Also fixed in `design/` itself: `components.html` said "radius 8" in prose while its markup
> used `rounded-md` in 157 places, which V1.0 maps to **10px**. The prose was the outlier and is
> now corrected — the source of truth has to be consistent with itself.

### ← You are here: 3d — the app's own screens

**599 compat usages left**: `features` 323 · `pages` 189 · `components` 70 · `public/app` 17.

> ⚠️ **Found while verifying: the shared components are not what most screens actually use.**
> `frontend/internal/src` hand-rolls **50 `<input>`, 63 `<button>` and 24 `<select>`** elements,
> against 24 files importing `Button` and 9 importing `Input`. The login page's field is 40px tall
> with a 6px radius — it is not the `Input` component at all. So migrating `packages/ui` moved the
> tokens but reached a minority of the surface. **Each screen is a decision: adopt the shared
> component, or restyle the local one.** Adopting is usually right and is what makes the next
> rebrand cheap, but it changes markup and can move focus/keyboard behaviour, so it is a per-screen
> call — not a sweep.

**Baseline measured 2026-08-21, source files only** (`.next`, `dist` and `node_modules` excluded —
an earlier count of "~1,120" included build artifacts and was wrong):

| Area | Usages |
|---|---|
| `frontend/internal/src/features` | 324 |
| `frontend/internal/src/pages` | 189 |
| `packages/ui/src` | 116 |
| `frontend/internal/src/components` | 86 |
| `frontend/public/app` | 17 |
| **Total** | **732** (706 colour + 26 `font-display`) |

By token: `primary` 219 · `surface` 155 · `zinc` 147 · `danger` 100 · `warning` 29 · `success` 28
· `teal` 13 · `cyan` 11 · `accent` 4.

**The number that has to reach zero**, and the command that reports it:

```bash
grep -rEo --include=*.tsx --include=*.ts --include=*.css "(primary|success|warning|danger|accent|surface|zinc|cyan|teal)-[0-9]+|font-display" frontend/internal/src frontend/public/app packages/ui/src | wc -l
```

Suggested order — `packages/ui/src` first, because both apps import it and `StatusPill` alone
carries the whole status vocabulary; then `components`, then `pages`, then `features` by module,
then `frontend/public/app`. **Delete the compat block in the preset when the count is zero.**

⚠️ **Do not do this as a find-and-replace of token names.** Open the matching screen in `design/`
and build against it: the kit also changes radii, drops the display face and re-cuts the status
vocabulary, so a rename alone leaves the apps looking like neither system. That is the whole
reason `design/` is the source of truth.

Then: `RecruitOps_Design_System.md`, and finally step 4 (`marketing/landing.html`, `DESIGN.md`).

### 1. ✅ ADR-0026 is built — all four steps. What is left is the *screen*.

[ADR-0026](../decisions/ADR-0026-outbound-delivery-and-background-jobs.md) is Accepted and, as of
2026-08-21, **implemented**. SMTP behind `IEmailSender` as the floor, a transactional
`OutboundMessage` outbox, in-process workers claiming due rows with a visibility timeout, and no
new NuGet package. (The ADR originally specified `FOR UPDATE SKIP LOCKED`; see its 2026-08-20
amendment for what that trade narrowed.)

**Two things it did NOT finish, and the first one is now the top item in this file:**

- 🔴 **Nothing renders `OutboundMessages`.** A `Failed` invitation — wrong address, dead relay —
  is recorded faithfully and shown to nobody, so "was this candidate told?" is answerable only by
  someone with a psql prompt. That is half of what the ADR was written to fix, still missing.
  `design/internal/channels.html` already draws the delivery log; it needs an endpoint and a page.
- 🟠 **Step 4 has not been security-reviewed.** Steps 1–3 have. Step 4 added a second worker that
  reads and writes candidate data with no user behind it, and a new object-storage key format.
  CLAUDE.md requires the pass.

**Read §4 of the ADR before writing any handler or worker.** A background job has no
`HttpContext`, so `CurrentTenant.TenantId` is `Guid.Empty`, every global query filter matches
nothing, and `AppDbContext` would stamp new rows with tenant `Guid.Empty`. The worker sets a
scoped tenant from the claimed row before resolving anything, so the work itself looks like
request code and **calls no `IgnoreQueryFilters()`**. `InterviewInvitationHandler` and
`BulkResumeWorker.ExtractAndCreateAsync` are the two worked examples; copy either.

How it was built, each step a session:
1. ✅ **Done 2026-08-20.** `OutboundMessage` + `ScheduledJob` entities, config, tenant filters and
   six tests, plus migration `20260820072400_AddOutboundDeliveryAndScheduledJobs`.
2. ✅ **Done 2026-08-20, security-reviewed.** `IAmbientTenantScope` + `OutboundMessageWorker` +
   `IOutboundMessageHandler`, with 22 tests including the two-tenant isolation test the ADR asked
   for by name. The review found no tenant-isolation defect and one Low robustness bug, since
   fixed: a failure between claiming and recording used to dodge the attempt cap.
3. ✅ **Done 2026-08-20.** `IEmailSender` + `SmtpEmailSender` (`System.Net.Mail`, no new package)
   and `InterviewInvitationHandler`, with 44 tests. `InterviewService` now writes the invitation
   **in the same `SaveChangesAsync`** as the interview — that is the outbox, and it is the whole
   point of §2. Migration `20260820081448_AddCompanyTimeZone` came with it.
   **Security-reviewed the same day — nothing found**, across six claims put up to be disproved.
   Copy this handler when writing the next one; it is the worked example of §4 (no
   `IgnoreQueryFilters`, no hand-written tenant predicate, no `ICurrentUser`).
4. ✅ **Done 2026-08-21.** `BulkResumeService` rewritten off its `static ConcurrentDictionary` onto
   `BulkUploadBatch` + `BulkUploadFile` with the bytes in object storage, drained by
   `BulkResumeWorker`; migration `AddBulkUploadPersistence`; +13 tests, and the 20 existing API
   tests pass unchanged against the new implementation. Their `Task.Delay(300)`-and-hope polling
   was replaced by `BulkResumeQueue.DrainAsync`. **Not security-reviewed.**

← **you are here: the delivery-log screen, then the step-4 security review.** See the two bullets
above the step list.

### 2. Frontend tests for Modules 1–2's largest untested logic
Still open. The harness is proven and the suite is at 342, but these two components have no
test file at all:

- **`RequisitionFormPage`** — one component serving both create and edit. Two modes in one
  component is where this repo's recurring "rule added to two of three siblings" bug lives.
- **`FormFieldBuilder`** — a schema editor whose output the *server* validates. A builder that
  emits a schema the server rejects fails at the worst possible moment: when a stranger submits.

Pattern to copy: `src/lib/scorecard.test.ts` for pure rules,
`src/pages/InterviewDetailPage.test.tsx` for a page with `vi.mock('../lib/api')`.
**Prove each new test fails before you believe it passes.**

### 3. Finish ADR-0025 — move the code onto the V1.0 tokens
The 25 design screens are on V1.0; `packages/ui/tailwind-preset.js` and both frontends are
still on the Clear Pipeline preset. **Two token systems running in parallel is the exact
condition ADR-0025 was written to end**, and it now exists again in the other direction.
Sequence from the ADR: preset → both frontends → `RecruitOps_Design_System.md` →
`marketing/landing.html` and `DESIGN.md` last.

### 4. Answer the five questions the design kit surfaced
All five are recorded in FEATURE-STATUS under Known gaps, and three are business decisions
rather than engineering ones:

- The **threshold rule** is drawn on three screens and modelled nowhere — `ApprovalChain` has
  no condition field. Either it gains one or those screens are wrong.
- **`ApprovalChainStep.ApproverUserId` is a person, not a role** — disabling a user silently
  stalls every requisition at their step.
- **Module 8 may be unbuildable on-premise** (webhooks need a public endpoint). Commercial
  decision as much as technical, and it is the headline differentiator.
- **Module 6 needs `Requisition → HeadcountPlan`** or its headline number is uncomputable.
- **Age/gender filtering** is unconfirmed for this market.

### 5. Smaller, whenever
- **Delete or wire up `frontend/internal/src/features/requisitions/`** — zero importers
  repo-wide, five files, and a test that passes while proving nothing about the shipped app.
- Re-run Module 5's metrics against the **new** definitions once Module 4 exists; the shipped
  ones end at a different event.
- Fix or delete the CI `Test counts` summary step — it reports a number nobody should trust.
- Build warning `CS8604` in `ApplicationFormSchema.cs:102`.
- Write the **99.9% SLA ADR** before `marketing/landing.html` goes live — it is asserted
  publicly and recorded nowhere.
- Application-form **file upload** field type — waits on the storage abstraction (ADR-0013).
- The **merge-two-existing-candidates** UI — drawn in `design/internal/talent-pool.html`,
  not built.
- Verify trigram search against a corpus of **real Burmese CVs**. It runs; nobody has measured
  whether its results are good.

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
- **Nobody runs `dotnet ef database update` in this project, and telling someone to is wrong.**
  Postgres exists only inside Docker, so there is no local database to point it at.
  `DatabaseStartup.MigrateAsync` applies pending migrations when the API container starts —
  rebuilding the stack *is* the procedure. What a session actually does is `dotnet ef migrations
  add`, which needs no database, and then commit the files.
  - Corollary: **`dotnet ef migrations remove` does need a database** and will fail here with
    `28P01: password authentication failed`. To undo an unwanted migration, delete the two
    generated files by hand and re-run `migrations add`.
  - Never reach for `docker compose down -v` to "reset" the schema. That deletes the volume and
    the dev data with it; migrations are additive and apply on their own.
- **Never pass `--no-build` to `dotnet ef migrations add`.** It reads whatever is already
  compiled in `src/Api/bin`, so a new entity added moments earlier is invisible and you get an
  **empty** migration — which commits perfectly cleanly: entities in code, DbSets registered,
  in-memory tests green, and no tables in any real database. Read the generated `Up()` before
  trusting it. This happened on 2026-08-20 and was caught only by looking.
- **A test that sleeps and then asserts on background work is a bet, re-placed every run.** The
  three bulk-upload suites did `await Task.Delay(300)` and then checked whether a `Task.Run` had
  finished; two of them wrapped it in a twenty-round retry loop, which turns a genuine failure
  into "still processing" for six seconds before failing anyway. Once work is claimable, a test
  can *drive* it — `BulkResumeQueue.DrainAsync` runs passes until the queue is empty and throws if
  it never is. No sleeps left in those suites.
- **`AddDbContext`'s options lambda runs once per scope, not once per host.** Putting
  `Guid.NewGuid()` inside `o.UseInMemoryDatabase(...)` gives every DI scope its own database, so
  a test seeds one store and the code under test reads an empty one — and every assertion fails
  in a way that looks like a missing row rather than a wiring mistake. Name the database once,
  outside the lambda, and capture it. Cost 20 minutes on 2026-08-20.
- **`WebApplicationFactory` starts hosted services**, so `OutboundMessageWorker` was polling
  every ten seconds through the entire integration suite — racing any test that asserts on a
  queued message. `CustomWebAppFactory` now removes it from `IHostedService` and registers it as
  a plain singleton, so tests drive `RunOnceAsync()` deliberately. Anything else added as a
  hosted service needs the same treatment, or the suite acquires a timing dependency nobody can
  see.
- **`MailAddress` does not reject `"a@x.test, b@y.test"`.** It parses the first address and
  carries on, so a two-address recipient field would deliver an offer to one of them while the
  delivery log claimed both. `SmtpEmailSender` refuses a comma or semicolon outright. Found by a
  test written expecting the opposite, on 2026-08-20.
- **An install with no `Smtp:*` configuration delivers nothing**, and that is now a real
  deployment failure rather than a hypothetical: interview invitations queue, retry to the cap
  and land `Failed`. The base `appsettings.json` ships it empty on purpose; Development writes
  `.eml` files to `./tmp/mail` instead. **Add SMTP to the install checklist before the next
  customer deployment** — `docs/architecture/deployment-runbook.md` does not mention it yet.
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
- **Two GitHub accounts are authenticated here, and only one can write.** `gh auth status`
  lists `minarkarsoe` (owns the repo) and `minarkarsoe-backend` (cannot push to it). If a push
  dies with `403 ... denied to minarkarsoe-backend`, nothing is wrong with the commits —
  `gh auth switch --user minarkarsoe` and push again. Check which one is active *before* a long
  commit sequence rather than after.
- **The Bash tool is bash, not PowerShell — `@'...'@` here-strings silently corrupt a commit
  message.** `git commit -m @'...'@` produces a commit whose subject line is a literal `@`.
  Use a real heredoc: `git commit -F - <<'MSG' ... MSG`. Both shells are available in this
  project and each takes its own syntax; the failure is silent in exactly this case.
- **Headless Chrome screenshots need an absolute `file:///` URL and forward slashes out.**
  `chrome.exe --headless=new --screenshot=<out> <relative-path>` writes nothing and reports
  nothing. Escaping `\\` inside a double-quoted bash string also eats the `$` of a loop
  variable, producing one file literally named `$f.png`. Use forward slashes on both sides:

  ```
  CHROME="/c/Program Files/Google/Chrome/Application/chrome.exe"
  "$CHROME" --headless=new --disable-gpu --hide-scrollbars --virtual-time-budget=5000 \
    --window-size=1440,3200 --screenshot="C:/abs/path/out.png" "file:///C:/abs/path/in.html"
  ```

  Note the window height *is* the capture height: a page shorter than it gets tiled, and a
  page using `min-h-screen` grows to fill it, pushing later sections out of frame.
- **`$HOME` inside the sandbox is a native path; `/tmp` may be owned by another session.**
  The copy-out recipe above works verbatim with `W=$HOME/rowork`.
- **Background processes do not survive between sandbox commands** — each call gets its own
  PID namespace, so `nohup`/`setsid` buys nothing. `npm install` finishes inside one call
  (~17s with a warm cache); don't try to background it and poll.
