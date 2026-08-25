# Changelog

Track record of every meaningful change. Newest first.
Format: what changed · why · what it touched.

## 2026-08-25 (latest)

### 🗂 `features/pipeline` on the design kit — and a contrast failure we shipped
**Why:** ADR-0025 step 3e. `features/pipeline` is at **0** compat tokens; the honest repo-wide
count is 334 → **215**. Frontend **352/352**, typecheck clean.

**Badge failed AA on three of its variants, and this change fixed it.** `packages/ui`'s rebuild
(2026-08-21) checked `StatusPill` and never opened `Badge`, so `success`, `warning` and `danger`
kept a **-500 step as text on their own -50 tint** — the exact failure the preset's own comment
warns about, three files above where it happened. Measured 2026-08-25:

| | before | | after | |
|---|---|---|---|---|
| `success` | positive-500 on positive-50 | **2.41:1 FAIL** | positive-700 | 5.21:1 PASS |
| `warning` | warn-500 on warn-50 | **2.07:1 FAIL** | warn-700 | 4.84:1 PASS |
| `danger` | critical-500 on critical-50 | **3.44:1 FAIL** | critical-700 | 5.91:1 PASS |

The colours were right and the *steps* were wrong, which is why review missed it twice and a
five-line script caught it immediately.

Beyond the rename, four things changed shape, each read off `design/internal/board.html`:

- **The board's columns are white on the canvas ground**, not grey fills holding white cards. The
  kit's board is cards floating on the page; a grey column inverts that and makes the container
  louder than its contents on a screen that is nothing but contents.
- **Column counts are `font-mono tnum text-ink-500`, not eight coloured Badges.** A badge is a
  status; a column count is a number that changes every time a card moves. Tabular figures stop
  it jittering, and eight tinted pills across the top stop competing with the stage names.
- **Loading is a skeleton, empty is a sentence.** `Loading…` tells the user to wait; a skeleton
  tells them what is coming. And the two terminal columns now say what they cost — the kit
  writes the Hired one out in full because "this closes the requisition" is a bad thing to
  learn by doing.
- **Stage history uses the approval-chain rail** (`.rail-step`), which the kit reuses for exactly
  this. The numbered circles it replaces encoded nothing the order didn't already say.

Two smaller ones: `ExecutiveSummaryPanel`'s two hand-rolled toggle groups became one segmented
control on the kit's detented-filter pattern — they had used **brand for "selected" in one group
and ink in the other**, two meanings for one idea on one row, and brand is the *action* colour, so
a filter painted brand looked like a button that would go and do something. And the AI panels'
402 banners moved off raw Tailwind `amber` onto `warn` tokens.

**A correction to the last entry.** It reported "repo-wide 525 → 340". That 340 counted only
`frontend/public/app`; pointed at `frontend/public`, the same grep also reads `.next/` build
output — 78 hits there, of which **66 are compiled artifacts that can never reach zero**. The
preset's exit-condition command now carries `--include` filters and points at `public/app`, with
the reason written next to it. A count that cannot reach zero is not an exit condition.

### 📄 `pages/` on the design kit
**Why:** ADR-0025 step 3d continued. `pages/` is at **0** compat tokens; repo-wide 525 → 340.
Frontend **352/352**, typecheck clean.

Three changes beyond the colour rename, each taken from the kit rather than from taste:

- **190 hard-coded text sizes are gone.** `text-[13px]`, `text-[15px]`, `text-[11px]` and friends
  bypassed the V1.0 type scale entirely. They map onto it exactly — 13→`text-sm`, 15→`text-md`,
  11→`text-2xs` — so nothing moved visually, and the scale is now something the app actually uses
  rather than something the preset merely declares.
- **Four hand-rolled tables now share one treatment.** `UsersPage`, `RequisitionsPage`,
  `InboxPage` and `JobPostingsPage` each wrote their own `<table>` and each had drifted
  differently: three padding schemes, three type sizes, and all four carrying the **uppercase
  micro-caps header** the kit does not use anywhere. Now `bg-canvas` header row,
  `px-4 py-2.5 font-medium text-ink-600` cells, no micro-caps.
- **Headings are on the kit's scale.** Section `<h2>`s were 13px grey uppercase micro-caps — which
  reads as a label for the thing beside it, not a heading for the block below it. The kit uses
  `text-base font-semibold` in 19 places and puts uppercase on a heading **nowhere**. Page `<h1>`s
  moved from `text-2xl font-bold` to the kit's in-app title, `text-xl font-semibold
  tracking-tight`; its `text-3xl font-bold` headings belong to the kit's own *spec* pages and were
  deliberately not copied into the app.

Verified in the browser on `/requisitions`: h1 18px/600 with -0.45px tracking · header cell
13px/500 ink-600 and `text-transform: none` · header row `#F8FAFC` · body cell 14px ink-900 · the
migrated `StatusPill` rendering warn-700 on warn-50 in situ.

### 🧭 The app shell on the design kit — and a contrast failure in the kit itself
**Why:** every screen sits inside it, so after the preset this is the change with the widest
reach. `components/` is at **0** compat tokens; repo-wide 595 → 525. Frontend **352/352**,
typecheck clean.

**The nav rail is dark now** — `bg-ink-900`, 224px, per `design/internal/board.html`. That is the
kit's central layout decision rather than a colour preference: it is the second neutral layer, so
the content surface reads as the workspace and navigation recedes. A white sidebar beside a white
content pane makes the two compete, and on a screen that is mostly table it is the table that
should win.

Also adopted: an icon per nav item, the active item as a filled `bg-white/10` pill — **the
`border-l-2` is gone**, because a border *and* a fill are two devices saying one thing and on a
dark rail the border reads as a seam — and the user block in the rail's footer.

**Identity now renders once.** The signed-in name appeared in both the header and the sidebar, and
a test asserted that duplication (`getAllByText(...).length === 2`). Two avatars on one screen is
two places to check who you are signed in as, and they can disagree while a session is being
replaced. The header keeps place, search and the primary action; the rail keeps who you are.

> 🔴 **The kit had a contrast failure, found by measuring instead of trusting it.** Nav group
> labels were `text-white/40`, which on `ink-900` is **3.81:1** — below AA for 11px text. Raised
> to `white/50` (5.23:1) in the code **and in all 19 kit screens**: `design/` is the source of
> truth, so the fix belongs there, not only downstream. Rail contrast now measures: active
> `white` 17.85 · idle `white/70` 9.10 · role line `white/50` 5.23 · group label `white/50` 5.23 ·
> avatar white on `brand-700` 5.47.

Two smaller corrections: the meaningless "CRM" badge next to the wordmark is gone, and the
super-admin 👑 emoji is now a plain "Super admin" role line — an emoji in an enterprise nav is
decoration standing where a fact belongs.

**Deliberately not changed: the nav group names and membership.** The kit's rail shows "Work" and
"Configure"; the app has Recruitment / Insights / Team / Governance. Which items sit under which
heading is product information architecture, not a design-system decision, and renaming the whole
nav inside a token migration would break tests for reasons unrelated to tokens. Flagged for the
product owner instead.

### 🔐 The login screen, rebuilt from the design — and two defects it was hiding
**Why:** the first screen taken end to end against `design/internal/login.html`, as the pattern
for the rest. Frontend **352/352** (44 files), typecheck clean.

**Two functional defects, both invisible in the source and found by using the screen:**

1. **A failed login said "Your session has expired. Please sign in again."** `apiFetch` mapped
   every 401 to that copy — the silent-refresh branch excluded `/auth/login`, the fallthrough did
   not. Someone who mistyped a password was told their session expired and sent looking for a
   problem that did not exist. Now "Email or password is incorrect.", which is also the only
   thing ADR-0016 permits: naming the field would tell a stranger whether an address belongs to a
   real employee.
2. **`Retry-After` was being thrown away.** ADR-0016's 429 carries the remaining lockout in
   seconds; `ApiError` had nowhere to put it, so the countdown the design draws was unrenderable
   and the page showed a generic error. `ApiError.retryAfterSeconds` now carries it. The screen
   counts down from the server's number, disables the form while locked, re-enables itself at
   zero, and falls back to the full 15 minutes only when the header is absent — never a shorter
   guess, which would send someone back to a door still locked.

**A third defect, introduced during the rebuild and caught only by looking at the browser:** the
password field's error outline appended `border-critical-500` to a class string that already had
`border-line`. Both are border-color utilities of equal specificity, so **Tailwind's output order
decided the winner** and the grey border won. It read correctly in the source and rendered wrong.
Fixed by building the class so the default is never emitted, and pinned by a test that asserts the
*absence* of `border-line` — a `toContain` assertion alone would have passed either way.

From the kit and now present: the logo mark and wordmark, the error as a tinted `role="alert"`
block, the spinner in the button (the one screen where a spinner is right — nothing is arriving
whose shape could be skeletoned), the "no self-signup" line, and no workspace field, because under
ADR-0004 the URL already is the company.

8 tests added, each mutation-checked. One of those mutations showed a `setLockedFor(null)` branch
was **redundant** — removing it changed no test and no behaviour — so it was deleted rather than
pinned.

> **Measurement note for anyone verifying UI this way:** the Browser pane does not composite, so
> CSS transitions never advance and `getComputedStyle` returns the *starting* value of anything
> under `transition-colors`. Two apparent bugs on 2026-08-21 were that. Disable the transition and
> force a reflow before reading, or measure a freshly created element.

### 🎨 ADR-0025 step 3c — `packages/ui` rebuilt against the kit
**Why:** both apps import it, so it is the one place where a fix reaches everything. Frontend
**344/344**, typecheck clean. **0 compat tokens left in `packages/ui/src`** — 133 migrated across
13 components; repo-wide 732 → 599.

**Built against `design/internal/components.html`, not renamed.** The colour aliases were a
mechanical pass; the shape was not, and the kit changes more than colour:

- `StatusPill` — tint `-100`→`-50`, type 13px/semibold→12px/medium, and the neutral statuses gain
  a border because they are the only ones with no tint to sit on.
- `Button` — `h-10`→`h-9`, 15px/semibold→14px/medium, primary base `brand-600`→`brand-700` with
  real `active:` steps (on a touch screen there is no hover, so press feedback is the only
  confirmation the tap landed), disabled `bg-ink-400/40`.
- `Input`/`Select` — `h-10`→`h-9`, `rounded-sm`→`rounded-md`, soft `/20` focus ring, and the error
  message moved from `text-xs text-critical-500` to `text-sm text-critical-700` (the -500 step on
  white is 3.76:1 — it failed).
- `Table` — **the uppercase micro-caps header is gone**, rows are 44px per the kit, one rule per
  row instead of `divide-y` *and* `border-b`, selected row `bg-brand-50/60`.
- `Card` — title dropped from 19px in the display face to 14px semibold. A card heading labels the
  thing below it; it is not a headline.

**Every pill contrast pair was re-measured rather than assumed**, because the `-100`→`-50` move
changes all of them: ink-600/canvas 7.24 · brand-800/brand-50 7.27 · info-700/info-50 6.16 ·
critical-700/critical-50 5.91 · positive-700/positive-50 5.21 · warn-700/warn-50 **4.84**. All
pass AA; warn has the least headroom and is the pair that breaks first.

**Verified by reading computed styles in a browser**, not by reading the diff — button
`#0F766E`/36px/14px/500/r10, pill `#FFFBEB` on `#B45309`/24px/12px/500, card white/r12/20px.

12 design-system test assertions were updated to the V1.0 names and 2 tests added. They were doing
their job: they pinned the old system and failed the moment it changed.

> **Also fixed in `design/` itself:** `components.html` said "radius 8" in prose while its markup
> used `rounded-md` in 157 places — 10px in V1.0. The prose was the outlier. A source of truth
> that contradicts itself is not one.

> ⚠️ **Found while verifying, and it changes the size of what is left:** most screens do not use
> these components at all. `frontend/internal/src` hand-rolls **50 `<input>`, 63 `<button>` and 24
> `<select>`**, against 24 files importing `Button` and 9 importing `Input` — the login page's
> field is 40px/r6, not the `Input` component. Migrating the shared package moved the tokens but
> reached a minority of the surface.

### 🎨 ADR-0025 step 3a+3b — the apps are on V1.0 tokens
**Why:** `design/` is now the declared source of truth for UI, and that rule was inert while the
kit and the apps used different *class names* — a screen copied from the kit rendered unstyled,
not off-brand. Frontend **342/342**, typecheck clean, both apps build.

`packages/ui/tailwind-preset.js` is now a copy of `design/internal/ds.js` plus a **fenced
compatibility block** aliasing every old name onto its V1.0 equivalent (`primary→brand`,
`success→positive`, `warning→warn`, `danger→critical`, `accent→warn`, `surface→canvas/slate`,
`zinc→slate`, `cyan`/`teal`→brand). So the apps already render V1.0 colours; only the names are
old, and they migrate an area at a time without the app breaking in between. **The block carries
its own exit condition** — the grep that has to reach zero is in the file.

Also landed: the V1.0 type scale (`text-base` is 14px, not 16 — that density is why the kit reads
as an operations tool), radii 6/8/10/12/16/20, and `ds.css`'s base layer in both apps — focus
ring, `::selection`, `.mm` Burmese line box, `.tnum`, mono ligature suppression, the
approval-chain rail, skeletons.

**Two live defects fixed on the way, neither of them a migration cost:**

- **163 classes in the shipped apps emitted no CSS at all.** Found by building `frontend/internal`
  and grepping `dist/assets/*.css`: `text-ink-500` (57 uses), `text-ink-700` (39), `text-ink-800`
  (20), `border-line-300` (28), `bg-surface-100` (13), `border-line-100` (5), `bg-surface-200` (1)
  were ABSENT — the old preset defined `ink` at 900/600/400, `surface` at 0/50 and `line` at 200
  only, so those elements silently inherited their parent's colour. All seven now present.
- **Bricolage Grotesque and IBM Plex Mono were still downloading** after the "fix". Removing the
  `@import` from the CSS was not enough: the real request is a `<link>` in
  `frontend/internal/index.html` and `frontend/public/app/layout.tsx`. Caught by reading
  `document.fonts` in a browser rather than trusting the diff. Now one request, three families.

Body type was also wrong in a way nobody had measured: `line-height: 1.7` was set globally "for
Burmese", which made every English row 27px tall. Burmese gets `.mm` instead, and the body is
14px/20px in the internal app, 15px/22px on the public one.

**Baseline for what remains (source only, `.next`/`dist`/`node_modules` excluded — an earlier
count of ~1,120 included build artifacts and was wrong): 732 compat usages** — `features` 324,
`pages` 189, `packages/ui` 116, `components` 86, `public/app` 17.

**Also deleted:** the seven `design-prototypes*` folders (untracked, superseded by `design/`).
`design-prototypes-7/RESEARCH-competitive-ux-feedback.md` was **not** deleted — it is real
competitive research with no copy elsewhere, and it moved to
`docs/product/competitive-ux-research.md`.


### 📦 ADR-0026 step 4 — bulk CV upload comes off the static dictionary
**Why:** the last of the two job mechanisms ADR-0026 exists to collapse into one. Backend
**612/612** (62 domain + 550 api), up from 599; **+13 tests**, and the 20 existing API tests pass
**unchanged** against the new implementation — which is the signal that the contract survived.

**What it replaced**, quoted here because the status file recorded this as "asynchronous ✅" for
weeks: `private static readonly ConcurrentDictionary<Guid, BatchStateHolder> Batches`, holding the
**raw uploaded bytes**, populated by `_ = Task.Run(() => ProcessBatchAsync(batchId))`.

| Was | Is |
|---|---|
| A restart **erased** the batch — not "the status goes stale"; the entry was gone, `GetBatchStatusAsync` returned null, and a recruiter's 50 files 404'd with no way to learn whether any candidate had been created | `BulkUploadBatch` + `BulkUploadFile`, written before the response returns. A claim only pushes a due time forward, so a process that dies mid-batch leaves work that becomes due again by itself |
| 50 CVs of several MB each in RAM per concurrent upload | Bytes in object storage at upload (ADR-0013); the row keeps a key. That same object becomes the application's résumé — uploaded once, referenced, **never copied** |
| An unobserved exception inside `Task.Run` | Backoff, an attempt cap of 3, and the between-claim-and-record wrapper the 2026-08-20 security review forced onto the mail worker — here from the start |
| Candidate dedup via `IgnoreQueryFilters()` + a hand-written `c.TenantId == …` | An ordinary filtered query. The worker enters the tenant, so there is no predicate left to forget (ADR-0026 §4) |
| Four counters maintained by hand under a `lock` | Status and every count **derived** from the file rows. A computed count cannot disagree with the rows it counts |

**Two decisions worth naming, not transcription:**

- **It is a second `BackgroundService`, and §3 said "a single" one.** The claim loop is reproduced
  rather than shared: a generic base class over two entities with different status enums and
  different terminal states is more coupling than two queues justify, and `OutboundMessageWorker`
  is security-reviewed, so refactoring it to serve a second caller would put that behind a
  re-review. **A third queue is the point at which to extract it** — written into the class.
- **The storage key carries no part of the uploaded file name**, only ids and a validated
  extension. The old key was `{Guid}_{item.FileName}`; a candidate-supplied name in an object key
  is how a stray `../` becomes somebody else's problem in whichever backend a customer runs.

**Found by writing a test that asserted the opposite:** cleanup of a failed file's stored bytes
ran only on the in-scope path, but a CV that cannot be parsed reaches terminal-Failed through the
*exception* path — so the common failure would have left a candidate's CV in storage every time,
with nothing pointing at it. Fixed; it is a Module 7.4 retention concern, not a tidiness one.

**Also gone:** a second, identical `IBulkResumeService` in `Application.Common.Interfaces`,
registered in DI alongside the real one and consumed by nothing.

**Tests stopped sleeping.** The three suites used `await Task.Delay(300)` and then asserted that a
background task had finished — two of them inside twenty-round retry loops that turn a real
failure into six seconds of "still processing". Replaced by `BulkResumeQueue.DrainAsync`, which
runs passes until the queue is empty and throws if it never is.

⚠️ **Not security-reviewed.** Migration `AddBulkUploadPersistence` is additive and applies on
container start.

## 2026-08-20

### 🔐 Security review of the invitation handler and SMTP transport — nothing found
**Why:** step 3 is the first code in the product that reads candidate data with **no user behind
it**, and it puts a candidate's name and interview details on the wire. CLAUDE.md requires the
pass; the previous review covered only the tenant seam.

Six claims were put up to be **disproved**. All six held, and each was established by a code path
rather than by inspection of intent:

- **Cross-tenant reads.** The one unfiltered lookup — `Companies`, which is not `ITenantScoped` —
  is safe because `message.TenantId` is itself read from a tenant-filtered re-read inside the
  worker's scope, so it cannot diverge from the tenant governing every other query.
- **Recipient steering.** `OutboundMessages` has exactly one write site repo-wide, and neither
  `ScheduleInterviewRequest` nor `RescheduleInterviewRequest` carries an address field. Nobody can
  get another candidate's interview details emailed to an address they control.
- **The absent department scoping** (deliberate — ADR-0026 §4, "a job is not a user") is sound
  because `SubjectId` is only ever set inside an already-authorised write, so the handler's
  foreign-key chain is the chain that was authorised, not a query an attacker can parameterise.
  `RescheduleAsync` re-reads the application only *after* `LoadWritableAsync`.
- **Header injection.** Tested rather than read: `MailAddress` was driven against four CRLF
  variants, including a quoted local part, and rejected all of them.
- **Secrets and PII.** No credential or candidate data reaches any log or `PayloadJson`; nothing
  real was committed to either `appsettings` file.
- **The test-only worker change** is inside `ConfigureTestServices`; `Program.cs` still registers
  the real hosted service.

**One Low observation, recorded rather than fixed.** A pass drains its batch sequentially, so its
worst case is `BatchSize × Smtp:TimeoutSeconds` — 20 × 30 s = 10 minutes, against a 5-minute
visibility timeout. It causes **no duplicate sends today** (one worker, passes never overlap) and
throttles only that company's own queue, but it becomes a duplicate-send bug the moment anyone
runs two replicas — the fourth item now riding on that assumption. Written into
`OutboundDeliveryOptions.BatchSize` where somebody tuning these will read it. Bounded parallelism
is a throughput decision ADR-0026 chose not to take, not a defect to patch quietly.

**Separately, one copy fix from actually reading a rendered invitation** rather than only
asserting substrings on it: "Thank you for your interest in Collections Officer (Field)" read as
though a word had gone missing. The subject wants the bare title; a sentence wants "the … role".
Two forms now, and the three-mode render was eyeballed end to end.

Frontend re-verified unchanged: **342/342** across 43 files, typecheck clean both apps.

### 📧 ADR-0026 step 3 — the product sends its first email
**Why:** steps 1 and 2 built a queue and a worker with nothing to deliver. This is the transport
and the first handler, and it closes Module 3.2 — the oldest of the five gaps ADR-0026 was
written for. Backend **599/599** (62 domain + 537 api), up from 555; **+44 tests**.

**`IEmailSender` + `SmtpEmailSender`, on `System.Net.Mail`, no new package.** SMTP is the floor
rather than the fallback (ADR-0026 §1): it is the only transport that works in every deployment
we sell, including the air-gapped on-premise bank whose one mail path is an internal relay.

Two judgements the transport makes on its own, both pinned by tests:

- **Unconfigured is retryable, not permanent.** An install with no `Smtp:Host` fails loudly and
  keeps the message. An administrator fixes that in two minutes; marking it permanent would mean
  every invitation queued in the meantime had already been given up on.
- **Permanent means the *address* is wrong, and nothing else.** 550/551/553/554 are terminal;
  a rejected password, a required STARTTLS, a busy relay, a refused connection are all retried.
  The known imprecision is written down rather than hidden: some relays return 550 for "relaying
  denied", which is a config fault and will land in the wrong bucket — visibly, in the log.

**`InterviewInvitationHandler`** — the first `IOutboundMessageHandler`, and deliberately the
worked example of §4: no `IgnoreQueryFilters()`, no hand-written tenant predicate, no
`ICurrentUser`. The worker enters the tenant, so the handler's queries are ordinary queries.

**`InterviewService` now writes the invitation in the same `SaveChangesAsync` as the interview.**
That is what makes this an outbox rather than a send: there is no state in which the round exists
and the intention to tell the candidate does not, and no request waits on a mail server.

Because nothing is rendered until send time, three behaviours fall out rather than being coded:

- **Cancelling a round suppresses a queued invitation.** `CancelAsync` writes one row and touches
  the queue not at all; the handler reads the round's status at send time. `Suppressed` is not a
  failure and the delivery log must not colour it red.
- **Rescheduling before the invitation goes queues nothing new** — the pending row renders the
  new time by itself. A second row would tell the candidate their time had "changed" from one
  they were never given. After it has gone, a second message is queued and reads as a change.
- **A slot that has already passed is suppressed.** Inviting somebody to an interview that
  already happened is worse than saying nothing.

**New: `Companies.TimeZoneId`** (migration `20260820081448_AddCompanyTimeZone`, additive and
nullable). Npgsql stores `DateTimeOffset` as `timestamptz` and normalises to UTC — the instant
survives a round-trip and the recruiter's *o'clock* does not. At UTC+06:30 that turns a Monday
morning interview into Sunday evening. The zone is frozen into the message payload at enqueue;
null falls back to UTC and the email labels itself UTC rather than quietly lying.

**Two things found by writing the tests, both fixed:**

- `MailAddress` does **not** reject `"a@x.test, b@y.test"` — it parses the first and carries on.
  A two-address recipient would have delivered to one while the log claimed both. Now refused.
- `WebApplicationFactory` starts hosted services, so the delivery worker had been polling
  through the whole integration suite since step 1, racing any test that asserts on a queued
  row. It is now removed from `IHostedService` in `CustomWebAppFactory` and driven deliberately.

**What this does not do, stated plainly:** no XOAUTH2 and no implicit TLS, so **Microsoft 365 and
Google Workspace cannot actually be used** even though the integrations design draws all three as
first-class — that needs MailKit, which is a package decision ADR-0026 declined to take. Mail is
English only. And **no screen reads `OutboundMessages`**, so a `Failed` invitation is recorded
faithfully and shown to nobody. ⚠️ **Not yet security-reviewed** — this is the first code that
reads candidate data with no user behind it.

### 🔐 Security review of the tenant seam — clean on isolation, one defect found and fixed
**Why:** CLAUDE.md requires a `security-reviewer` pass on authorization changes, and step 2 made
tenant resolution settable. Reviewed against `a2de09c`. Backend **555/555**, up from 553.

**No tenant-isolation finding.** Each of the four claims was checked against the code rather than
taken on trust:

- An ambient tenant **cannot** redirect an authenticated request. Middleware order was walked
  (`UseAuthentication` precedes everything that touches `AppDbContext`), and `EnterTenant` has
  exactly one caller in `src/` — the worker.
- A scope carries at most one tenant, and `CurrentTenant` resolves the **same** scoped instance
  the worker set. Both are `AddScoped`, and the worker enters the tenant before resolving
  `AppDbContext` from that scope.
- The cross-tenant claim is contained: the claimed entity is never reattached to a later scope —
  the message is re-queried by id through a fresh filtered context.
- `PublicJobService` and the startup seed paths are unchanged.

**One Low-severity defect, in code from step 2, now fixed.** Anything thrown *between* claiming a
message and `Record()` — `EnterTenant` rejecting a malformed `TenantId`, or the row being
unreadable inside its own tenant — escaped to the pass-level catch in `ExecuteAsync`. Two
consequences, both real:

1. The row never reached `Record()`, so **its attempt cap was never checked**. It would be
   reclaimed every visibility window indefinitely — precisely the poison message `MaxAttempts`
   exists to stop, dodging the cap.
2. The rest of that pass's claimed batch was abandoned.

Fixed by wrapping the per-message work: a failure outside the handler is now a counted, capped
retry recorded through a contained `IgnoreQueryFilters()` path that touches only queue
bookkeeping, and the batch continues. Two tests added, **proved to fail first** — removing the
wrapper produced exactly those 2 failures.

Not exploitable today (nothing yet inserts an `OutboundMessage` outside tests), but step 3 adds
the first real producer, which is why it was fixed now rather than logged.

**Also noted by the review, and worth stating precisely:** "`ICurrentTenant` is now settable" is
loose — the interface is still get-only. What changed is that it gained a second, settable
*input*. The earlier entry below keeps the loose phrasing; this is the accurate version.

### ⚙️ ADR-0026 step 2 — the tenant seam and the delivery worker
**Why:** the second of four sessions. No email sender and no real handler yet — this is the
machinery they will plug into. Backend **553/553** (62 domain + 491 api), up from 533.

**🔐 `ICurrentTenant` is now settable, and that is an authorization change.**
`CurrentTenant` gained a second source: `IAmbientTenantScope`, a scoped holder the delivery
worker fills from the message row it claimed. Without it a background job sees
`TenantId == Guid.Empty`, every query filter matches nothing, and the queue silently never drains.

**The order is the security property.** The request claim is read first and wins whenever present,
so resolving `IAmbientTenantScope` inside an authenticated request and entering a tenant is
**inert** — nothing reachable from a request can redirect that request at another company's data.
`CurrentTenantResolutionTests` asserts the order, including that an anonymous request still
resolves to `Guid.Empty` so `PublicJobService` keeps working unchanged. A failure there is a
security finding, not a test to update.

`EnterTenant` refuses a second call — even with the same tenant. A worker that recycled one DI
scope across two messages would run the second as the first one's tenant, read its data, and look
entirely successful; this makes that a crash instead.

**`OutboundMessageWorker`** claims due rows (the one place that legitimately calls
`IgnoreQueryFilters()`), then handles each in its own scope with the tenant established, so
handlers query normally. Outcomes: `Sent`, `Suppressed` (terminal, *not* an error — an honoured
opt-out is the system working), `Retry` (exponential backoff to a cap), `Failed` (terminal). A
missing handler retries rather than failing, because the usual cause is a deployment that has not
registered it yet and burning the queue for a wiring mistake is worse than waiting. A handler that
throws is retryable; a handler that knows better returns `Failed`.

**A guarantee was narrowed, and the ADR now says so.** §3 originally specified claiming with
`FOR UPDATE SKIP LOCKED`. It is implemented as a read-then-update through EF. Crash safety is
unchanged — rows are pushed into the future, never marked in-flight — but with **two** workers
against one database both could claim the same batch and send twice. Accepted because ADR-0004
ships one instance per company, and because provider-specific SQL would mean the suite exercises a
different claim path from production. This is now the **third** in-process assumption riding on
"one replica", after `LoginThrottle` and the bulk-upload dictionary; they should be audited
together.

**Tests — 20, all proved to fail first.** Reversing the two lines in `CurrentTenant` produced
exactly 1 failure (`The_Request_Claim_Beats_An_Ambient_Tenant`); removing `EnterTenant` from the
worker produced 9 of 10. Includes the two-tenant isolation test ADR-0026 asked for by name: two
messages, two tenants, one pass, each handler seeing only its own tenant's rows.

⚠️ **Still needs a `security-reviewer` pass before step 3** — per CLAUDE.md, this touched an
authorization surface.

**Touched:** `backend/src/Application/Common/IAmbientTenantScope.cs`,
`Application/Interfaces/IOutboundMessageHandler.cs`, `Infrastructure/Tenancy/AmbientTenantScope.cs`,
`Infrastructure/Services/Delivery/`, `Api/Auth/CurrentTenant.cs`, `Api/Program.cs`,
`Infrastructure/DependencyInjection.cs`, and two new test files.

### 🧱 ADR-0026 step 1 — `OutboundMessage` and `ScheduledJob` entities + migration
**Why:** the first of the four sessions in the ADR's build order. Schema only — no worker, no
sender, no handler. Backend **533/533** (57 domain + 476 api), up from 527.

**Domain** — `OutboundMessage`, `ScheduledJob`, and four enums in
`OutboundDeliveryEnums.cs`. Decisions worth knowing, all documented on the members themselves:

- **No `Sending` status.** The worker claims a row by pushing `NextAttemptAt` forward by a
  visibility timeout inside the claiming transaction, so a process that dies mid-send leaves the
  row `Pending` and it becomes due again. An in-flight state would need a reaper to clean up
  after crashes — a second mechanism doing the first one's job.
- **`Suppressed` is a status, not a failure.** An honoured opt-out is a correct outcome. Module 8
  requires opt-out, and rendering it red teaches recruiters to ignore the failure colour.
- **`PayloadJson` holds the data to render, not the rendered body.** A body frozen at enqueue
  goes stale; a reminder queued for next week would carry last week's figures.
- **`ScheduledJob.TimeZoneId` is required with no default.** Storing UTC alone would be quietly
  wrong — a customer asking for "every Monday at 9" means 9 in their office, and UTC+6:30 turns
  that into Sunday evening. There is no default because guessing a company's timezone is the same
  bug with fewer symptoms. A company-level timezone setting does not exist yet; when it lands it
  becomes this field's default, not its replacement.
- **`DayOfMonth` is capped at 28**, in a check constraint. "The 31st" does not exist in February,
  and both alternatives — skip the month, or silently slide — are surprises.

**Infrastructure** — DbSets, configuration (string-converted enums, `jsonb` payloads, three
check constraints), tenant query filters, and two indexes shaped for the worker's claim query.

**Migration `20260820072400_AddOutboundDeliveryAndScheduledJobs`** is generated and committed.
It applies itself: Postgres exists only inside Docker in this project, and
`DatabaseStartup.MigrateAsync` runs pending migrations when the API container starts. Nobody runs
`dotnet ef database update` here — an earlier version of this entry said to, which was wrong, and
the correction is now recorded in NEXT-SESSION's "Things that will bite you".

**A near-miss worth recording.** The first `migrations add` used `--no-build` and produced an
**empty** migration — EF loaded a stale Api build that predated the new entities. It would have
committed clean: entities in code, DbSets registered, in-memory tests all green, and no tables in
any real database. `dotnet ef migrations remove` then failed because it wanted a live DB
connection, so the empty files had to be deleted by hand and regenerated with a real build.
**Never pass `--no-build` to `migrations add`, and read the generated `Up()` before trusting it.**

**Tests** — six new cases in `OutboundDeliveryPersistenceTests`, **proved to fail first**:
deleting the `OutboundMessage` query filter produced exactly the 2 expected failures, then the
file was restored. The load-bearing one is
`Worker_Without_A_Request_Sees_An_Empty_Queue_Until_It_Ignores_The_Filter` — ADR-0026 §4 written
as an executable assertion rather than a comment. A worker running outside any request sees
`TenantId == Guid.Empty`, so the queue looks empty however full it is; nothing throws and the
product just silently stops sending.

**Also logged, not fixed** (separate changes): two migrations directories exist, with one stray
duplicate under `Persistence/Migrations/`; and `ITenantScoped`'s doc comment still says a tenant
is an "agency", missed by the 2026-07-27 pivot.

**Touched:** `backend/src/Domain/Entities/OutboundMessage.cs`, `ScheduledJob.cs`,
`backend/src/Domain/Enums/OutboundDeliveryEnums.cs`,
`backend/src/Infrastructure/Persistence/AppDbContext.cs`,
`backend/src/Infrastructure/Migrations/`, `backend/tests/RecruitOps.Domain.Tests/`.

## 2026-08-18

### ✅ ADR-0026 accepted — hand-rolled queue, and the tenant problem it exposed
**Why:** the dependency question left open below was put to the product owner and answered:
**hand-rolled, no new NuGet package.** Two product-specific grounds decided it, not general
preference — `OutboundMessage` is needed either way (so Hangfire would add a second source of
truth about the same send, and they would eventually disagree), and Hangfire's `Enqueue` writes
in its own transaction, breaking the atomicity that is the entire point of an outbox.

Recorded honestly in the ADR: retry correctness, backoff, poison-message handling and
observability are now **ours to get right**, and the conditions that should reopen the question
are named — many job types, a second replica, or an ops requirement that would make us build a
dashboard anyway.

**The ADR gained a section it was missing, and it is the one with teeth.** §4: *a job carries
its own tenant, and the query filters stay on.* `CurrentTenant` reads `IHttpContextAccessor`, so
a background job sees `TenantId == Guid.Empty` — every one of the twenty-odd global query
filters matches nothing, and `AppDbContext` would stamp new rows with tenant `Guid.Empty`.

The repo's existing answer is `IgnoreQueryFilters()` plus a hand-carried tenant, used by
`PublicJobService` and `BulkResumeService`. **That pattern is deliberately not extended to job
handlers.** It is exactly the shape ADR-0003 warns about — a filter applied explicitly and
therefore possible to forget — and one forgetful handler reads another company's data. Instead
the worker sets a scoped tenant from the message row before resolving anything, so handler code
looks like request code and no handler calls `IgnoreQueryFilters()`.

That makes `ICurrentTenant` **settable**, which is a change to a security-critical seam and is
logged in FEATURE-STATUS as its own High row: it needs a dedicated review and a two-tenant
isolation test, because the failure mode is a scope retaining a previous job's tenant, which
reads as working until two companies' data cross.

Identity gets a separate answer in the same section: `ICurrentUser` is also null in a job, so
anything recording an actor must attribute it to an explicit **system actor**, and a job must
never call a department-scoped path — `IDepartmentAccess` answers "may *this user* reach it" and
there is no user. Treating absence-of-user as permission is how ADR-0018's hole was opened.

**Touched:** `docs/decisions/ADR-0026-outbound-delivery-and-background-jobs.md`,
`docs/status/FEATURE-STATUS.md`, `docs/status/NEXT-SESSION.md`.

### 📐 ADR-0026 — outbound delivery and background jobs, proposed as one capability
**Why:** four modules had been blocked for three weeks on an absence recorded as four separate
gaps. There is no email sender and no job runner anywhere in `backend/src`. Six features
(Module 3.1/3.2, 4.1/4.2/4.3, 5.3, 2.3, 8) all reduce to the same shape — *something happened,
work must run outside the request, and somebody needs to know whether it actually happened* —
so they are decided once.

**Proposed:**
- **SMTP behind `IEmailSender` is the floor**, not the fallback. Inverting the usual choice on
  purpose: ADR-0004 sells on-premise installs, some of them banks with no outbound internet at
  all. A product whose only send path is `api.sendgrid.com` does not deliver mail for them, and
  fails at the worst moment — the offer reads "sent" and the candidate never heard. API
  providers remain available as an adapter that nothing may depend on.
- **A transactional outbox.** `OutboundMessage` is written in the same transaction as the thing
  that caused it, then sent by the worker. Never fire-and-forget, because in Modules 4 and 8 the
  recruiter's next action depends on whether the candidate was told. `Suppressed` is a
  first-class status so an opt-out is not rendered as a failure.
- **One in-process `BackgroundService`** claiming due rows with `FOR UPDATE SKIP LOCKED`.
  ADR-0004's single instance removes the problem distributed schedulers exist to solve.
- **Scheduling is a due-time on a row**, not a cron container — no mechanism that exists only in
  the hosted deployment.

**Left open deliberately, and it blocks the start: Hangfire or hand-rolled.** CLAUDE.md requires
asking before adding a NuGet package. The ADR recommends hand-rolled and argues the opposite
case honestly rather than deciding silently.

**A finding that made the earlier entry below inaccurate, corrected in both status docs.**
`BulkResumeService` does not keep batch state in a database row — it holds it in a
`private static readonly ConcurrentDictionary`, **including the raw uploaded file bytes**. So a
restart does not leave a stale "in progress" row; it **erases the batch**, and
`GetBatchStatusAsync` 404s on a recruiter's 50 files with no way to tell whether any candidate
was created. Fifty CVs of several MB each also sit in RAM per concurrent upload, which the
sizing guide does not account for. ADR-0026 replaces that service rather than extending it.

**Touched:** `docs/decisions/ADR-0026-outbound-delivery-and-background-jobs.md`,
`docs/status/FEATURE-STATUS.md`, `docs/status/NEXT-SESSION.md`.

### 🔧 Status docs re-derived from the code — they were wrong about four modules
**Why:** `FEATURE-STATUS.md` and `NEXT-SESSION.md` contradicted each other *and* the code, and
CLAUDE.md makes them the entry point for every session. A fresh session trusting either would
have rebuilt working software.

**What was wrong**, all verified against the tree rather than against the previous version of
the file:

| Claim | Reality |
|---|---|
| Module 5 Reporting ⬜ not started | `AnalyticsController` + `AnalyticsService` + `AnalyticsPage.tsx` ship and are tested |
| Module 2.3 OCR / 2.4 Smart Match / 2.6 search ⬜ | `BulkResumeService`, `DocumentExtraction/`, `AiIntegrationService`, `SearchService` all ship |
| Zawgyi→Unicode normalization 🔴 High, not implemented | `MyanmarScriptNormalizer` ships and is applied at ingest |
| Burmese trigram search 🟡 outstanding | `AddPgTrgmAndSearchIndexes` migration applied |
| Feature flags ⬜, `/api/version` ⬜, sizing guide ⬜, runbooks ⬜ | All four exist |
| Backend 507 tests / frontend 318 | **527** (51 + 476) and **342** across 43 files, both re-run today |

`NEXT-SESSION.md`'s backlog was the sharpest problem: three of its five items had already
shipped, and item 3 told the reader to start Module 2.3 CV upload — which is built.

**Two gaps the check found that the docs had not recorded at all:**

- **There is no email sender anywhere in `backend/src`.** No `SmtpClient`, `IEmailSender`,
  `MailKit` or `SendGrid`. It blocks Module 3 invitations, Module 4 offer sends, reminders and
  the IT/Admin handoff, and Module 5 scheduled reports — one capability, four modules. Promoted
  to 🟠 High and made the top backlog item.
- **Bulk CV upload is fire-and-forget, not a job runner.** `BulkResumeService.EnqueueBatchAsync`
  runs `_ = Task.Run(() => ProcessBatchAsync(batchId))` over a `static ConcurrentDictionary`
  that also holds the raw uploaded bytes — nothing reaches the database, so a restart erases the
  batch rather than leaving it stale. There is no retry and exceptions are unobserved. `grep`
  for `BackgroundService|IHostedService|Hangfire|Quartz` returns nothing. ADR-0008 called for
  asynchronous processing and this is the shape of it, not the thing.
  *(This bullet originally said the batch row still reads "in progress". There is no row —
  corrected the same day, see the ADR-0026 entry above.)*

Also newly recorded: the orphaned `frontend/internal/src/features/requisitions/` tree (zero
importers, five files, one test that proves nothing about the shipped app), ADR-0025 steps 3–4
being unstarted (**two token systems are running in parallel again**, now in the other
direction), and build warning `CS8604` in `ApplicationFormSchema.cs:102`.

`NEXT-SESSION.md`'s "Things that will bite you" section is carried over unchanged — it is
hard-won and still accurate — with three new entries in "Working cheaply" for traps hit today:
the two GitHub accounts where only one can push, the PowerShell here-string that silently
corrupts a `git commit -m` under bash, and the headless-Chrome screenshot invocation.

**Touched:** `docs/status/FEATURE-STATUS.md`, `docs/status/NEXT-SESSION.md`.

### 🎨 The remaining thirteen screens — every module now has a drawn UI
**Why:** the design kit stopped at Modules 1–5, so the whole administration surface, both
sourcing modules, planning and the entire public app existed only as prose. The customer has
no designer, and a spec that has never been drawn hides its own gaps.

**Thirteen screens, taking the kit from 12 to 25.** Same V1.0 tokens from `ds.js`, so Tailwind
classes transfer straight into React. A link check across all 26 files found and closed the one
dangling reference — `offer-dashboard.html` had pointed at a `preboarding-review.html` that was
never drawn.

- **Module 7 · access & administration** — `login.html` (six states), `users-roles.html`
  (role builder + department scope), `settings-org.html` (departments + approval-chain
  builder), `settings-integrations.html` (HRMS, mail/calendar, retention purge).
- **Modules 2 & 8 · sourcing** — `postings.html` (public link + application-form builder),
  `talent-pool.html` (search, bulk CV upload, merge), `channels.html` (Viber/Telegram/Facebook).
- **Module 3 · configuration** — `scorecard-builder.html`.
- **Module 4.3 · pre-boarding** — `preboarding-review.html`, the recruiter's side of the
  document check. It holds the most sensitive data in the product (NRC scans, bank accounts),
  so: no thumbnail grid, account numbers masked until a recorded reveal, no Hiring Manager
  variant at all, and a department handoff that carries a name, a role and a date to IT and
  nothing else.
- **Module 6 · planning** — `planning-budget.html`.
- **Public app** — `design/public/jobs.html`, `job.html`, `apply.html`, joining the existing
  offer portal.

**Drawn against the code, not against intentions.** The role builder uses the real permission
codes from `RbacSeedData.cs` — including the two whose action segment is not a bare verb
(`applications:move_stage`, `scorecards:manage_templates`) — and renders the real service
rules: system roles immutable, a role with active users undeletable, `Admin` holding 32 of 33
permissions and bypassing the matrix entirely. The scorecard builder renders
`ScorecardTemplate`'s three-level resolution as a resolved path, and the three `CriterionType`
values, no more.

**One correction to a mid-build assumption, recorded because it nearly shipped.** The login
screen was first drawn with lockout as an open question, an "attempts remaining" hint, and an
"ask your administrator to unlock" line. All three are wrong:
[ADR-0016](../decisions/ADR-0016-login-brute-force-protection.md) is accepted and implemented,
failures are counted for **every** email real or not (so the lockout is not an existence
oracle), admin unlock was considered and **rejected** as a griefing weapon, and the 401 carries
no body so the page has nothing to count with. The screen now renders that decision and cites
it.

**Five findings the drawing produced**, each recorded on the screen where it bites and
summarised on `design/internal/index.html`:

1. **The threshold rule has nowhere to live.** Three existing screens show an approver "added
   by threshold rule"; `ApprovalChain` stores a name, an optional department and an active flag
   — no condition, no amount, no operator. Either the entity gains fields or those screens are
   wrong.
2. **`ApprovalChainStep.ApproverUserId` is a person, not a role.** Disabling a user on the
   Users screen can silently stall every requisition waiting at their step. The link is
   invisible in the data, so the disable flow has to name the chains it breaks.
3. **Module 8 may be unbuildable on-premise.** Viber/Telegram/Facebook deliver by webhook and
   an on-prem install behind a firewall has no reachable endpoint. Three exits (publish an
   endpoint, hosted-tier only, outbound polling) and none chosen — for the module positioned as
   the primary differentiator. The screen is drawn blocked-first rather than happy-path.
4. **Module 6 depends on `Requisition → HeadcountPlan`.** Every "raised" and "remaining" figure
   needs it; the module doc lists it as an open question. Without it the headcount table is
   hand-typed and wrong within a week.
5. **Age/gender filters are unconfirmed for this market.** Module 2 flags data-protection
   implications; the screen holds them behind a click with the question attached rather than
   sitting them in the filter row where they get used by reflex.

**Touched:** `design/internal/*.html` (9 new + index rewritten), `design/public/*.html`
(3 new), `.impeccable/review/`, `docs/status/FEATURE-STATUS.md`.

### 📋 Module 4 and Module 5 scope rewritten from a sales requirement
**Why:** the product owner received new requirement documents from sales
(`Module 4_Offer Management & Pre-boarding.pdf`, `Module 5_Reporting & Analytics.pdf`).
Neither module is built, so this is a **spec change only** — no code was touched, per
CLAUDE.md's rule that the module doc moves before the code.

**Reading the PDFs was itself a finding.** Their text layer is unusable for Myanmar: both
`pypdf` and `pdf.js` return mis-mapped codepoints (`ြ ာျူး` where `များ` belongs) because the
embedded `MyanmarText` subset ships a broken `ToUnicode` CMap. The English text extracts
correctly; the Burmese does not. The content was recovered by **rasterising the pages with
pdf.js and reading the glyphs**, not by trusting any extractor. This is
[ADR-0009](../decisions/ADR-0009-myanmar-script-handling.md)'s problem arriving from the
outside: a document that *looks* like valid Myanmar text to software and is not.

**Module 4 — restructured, not just extended:**
- Three sub-modules (Offer Dashboard / Offer Generation & Approval / Pre-boarding &
  E-Signature) replace the old 4.1–4.4 feature list.
- **Status vocabulary changed:** `Pending Approval` added; `Signed`→`Accepted`,
  `Declined`→`Rejected`. ⚠️ `Rejected` now collides with `PipelineStatus.Rejected` — same
  label, different enum. `StatusPill` is deliberately strict about vocabulary, so
  `OfferStatus` joins as a fifth enum and the two must not be conflated.
- **Answers a standing open question:** offers *do* get their own approval chain — over
  budget or policy-driven, routed to HR Director / Finance.
- **New scope: HRMS sync via API on day one** (QHRM, BetterHR, GlobalTA, CityHR named).
  Flagged against ADR-0007: build one export contract as an extension point, not four
  vendor integrations in core.

**Module 5 — metric definitions changed:**
- Three sub-modules (Executive Dashboard / Pipeline & Source Analytics / Custom Report
  Builder).
- **Both clocks re-defined:** Time-to-Fill now runs from *requisition approved*, and both
  metrics end at *offer accepted*. Two consequences recorded: the approval wait is excluded
  by definition, so a requisition stuck twelve days in a chain reports a **shorter**
  Time-to-Fill; and neither metric is computable until Module 4 exists.
- **Answers "who may see whose numbers"** with a full per-sub-module permission matrix.
  Hiring Managers get **no access** to Pipeline & Source Analytics at all.
- **New: Recruiter Leaderboard** — staff performance ranking. Flagged as an
  employment-relations and personal-data question, not a neutral chart.
- **New: Schedule Email**, which forces server-side report generation since no browser is
  present when it runs.

**One blocker is now shared by three modules:** there is still no email sender and no job
scheduler in the codebase. That already blocked Module 3's interview invitations; it now
also blocks Module 4's `Remind Candidate`, `Send to Candidate` and IT/Admin handoff, and
Module 5's scheduled reports. Recorded as a gap in FEATURE-STATUS rather than four separate
per-module notes.

**Touched:** `docs/product/modules/04-offer-and-preboarding.md`,
`docs/product/modules/05-reporting-and-analytics.md`, `docs/status/FEATURE-STATUS.md`.

### 🎨 Screens drawn for Modules 4 and 5
**Why:** the revised specs needed to be seen, not just read, and the customer has no designer.
Static HTML in `design/internal/` (and `design/public/` for the one external surface), same
V1.0 tokens from `ds.js`, so Tailwind classes transfer straight into React.

**Six screens:** offer dashboard, offer generation & approval, candidate offer portal,
executive dashboard, pipeline & source analytics, custom report builder.

**The designs resolve things the specs only flagged:**
- The **`Rejected` collision** — the offer pill never appears in a pipeline list, and the row
  spells out *"Declined by candidate"* beneath it.
- **Hiring Manager salary hiding** renders as an **absent column**, not a blurred one. A
  column that is present but obscured advertises that a number exists.
- The **offer approval reuses Module 1's rail**, visually and structurally, rather than
  introducing a second approval mechanism.
- The **funnel band mapping** (4 bands ↔ 8 enum values) is drawn on the screen, including why
  `Rejected` gets no band.
- The **recruiter leaderboard is deliberately unsorted**, with the observation that the
  recruiter with the fewest CVs has the best conversion — any single-column sort would invert
  the truth.
- A **proposed fourth KPI tile, "time in approval"**, marked as *not in the requirement*:
  both required clocks start after approval, so the delay this product exists to remove is
  invisible on its own dashboard.

**Chart colour was computed, not chosen.** The categorical palette
`#0D9488 #7C3AED #D97706 #0369A1` was run through the dataviz validator and passes all six
checks (worst adjacent CVD ΔE 21.0 deutan / 13.6 tritan; normal-vision 30.2). The first
candidate used the brand teal `#0F766E` and **failed the chroma floor** — it reads as grey in
a chart — so it was re-stepped to `#0D9488`. Source share and source conversion are two
separate charts, never one dual-axis chart.

**Touched:** `design/internal/{offer-dashboard,offer-create,analytics-dashboard,analytics-pipeline,report-builder,index}.html`,
`design/public/offer-portal.html`.

## 2026-08-17

### 🧹 The design system finally went through the pivot, three weeks late
**Why:** `RecruitOps_Design_System.md` still opened with *"Design system for a B2B Recruitment
Agency Platform (RAaaS)"* and *"Your agency, running on rails."* — a product
[ADR-0001](../decisions/ADR-0001-pivot-to-inhouse.md) deleted on 2026-07-27. It specified a
client portal, Gold/Silver/Bronze client tiers, a client feedback bar, contract-expiry cards,
and a `Sent to Client` / `Placed` status vocabulary. **The doc and the token file had already
disagreed** — the preset carried no tier colours — and nothing caught it, because docs have no
compiler.

**The mechanism that kept the dead code alive.** `packages/ui` still *exported*
`ClientPortalCard`, `ClientFeedbackBar` and `ExpiryAttentionCard`. Nothing in either app
imported them; the only importers were **two test files**, `signatureComponents.test.tsx` and
`challenger_signature_edgecases.test.tsx`. So the suite was green *because* it exercised code
the product had removed — the tests were the last thing holding the agency model in the build.
`signatureComponents.test.tsx` opened with a suite literally named "StatusPill Extended
Vocabulary" asserting `Sent to Client`, `Placed`, `Accepted`, `Need More Info`, `Active`,
`Expiring Soon` and `Expired`.

**Shape:**
- `RecruitOps_Design_System.md` rewritten: thesis is now *"every decision has a record"*; three
  surfaces (internal app, public **applicant** job page, marketing) replace the agency's
  internal/client-portal split; status vocabulary is exactly the four backend enums; two new
  signature patterns replace the client ones — **Approval Chain Rail** (rounds stack rather than
  replace, senior skip-ahead names both parties, threshold breach renders amber) and **Blind
  Panel Scorecard** (withheld scores are *absent*, never blurred placeholders).
- `StatusPill`: `ExtendedStatusVocabulary` deleted — all ten labels were agency-era. The
  vocabulary is now the union of the four enums with **no free-form extension point**, on
  purpose: a label with no enum behind it is a status the product cannot be in.
- `PipelineStageRail`: defaults were `Sourced → Shortlisted → Sent to Client → Interview →
  Placed`, two of them deleted labels. Now the real funnel, `Sourced → Applied → Screening →
  Shortlisted → Interview → Offer → Hired`. `Rejected` is deliberately excluded — it is an exit
  from the funnel, and listing it implies candidates flow into it from `Hired`.
- `ClientPortalCard.tsx` and `ExpiryAttentionCard.tsx` deleted, with their exports.

**Contrast, fixed properly this time.** The doc claimed `-600` on `-100` was "WCAG AA
guaranteed" and that `ink-400` meta text was pre-checked. Measured at pill size, **five of those
claims were false**: warning 2.97:1, success 3.62, danger 4.08, info 4.23, `ink-400` on
`surface-50` 2.77 — against a 4.5:1 floor. Added `-700` text-on-tint steps to the preset
(success `#146B43`, warning/accent `#8A5A08`, danger `#A63423`, info `#22528F`) and moved
`StatusPill` onto them. The doc now says *verify, do not assert*.

**Proved to fail first.** The new contrast cases were mutated before being trusted — reverting
`Hired` to `text-success-600` and `Sourced` to `text-ink-400` produced exactly 2 failures, then
was restored. A guarantee nobody has seen fail is not a guarantee.

**Touched:** `RecruitOps_Design_System.md`, `packages/ui/tailwind-preset.js`,
`packages/ui/src/StatusPill.tsx`, `packages/ui/src/PipelineStageRail.tsx`,
`packages/ui/src/index.ts`, `packages/ui/src/ClientPortalCard.tsx` (deleted),
`packages/ui/src/ExpiryAttentionCard.tsx` (deleted),
`frontend/internal/src/components/ui/signatureComponents.test.tsx` (rewritten, 16 cases),
`frontend/internal/src/components/ui/challenger_signature_edgecases.test.tsx` (trimmed to 9).

**Verified:** `npm run typecheck` clean across both apps; `npm run test` in `frontend/internal`
**342/342 green across 43 files**.

### 🎨 Marketing landing page, and two contrast bugs it found in the design system
**Why:** the product needed a public sales surface. Built through the `impeccable` skill
(installed into this repo at `.claude/skills/impeccable/`), which routes a new surface through
`PRODUCT.md` → visual direction → build → review.

**Shape:** `marketing/landing.html` — a single self-contained file using the Tailwind CDN and
Lucide icons, opening in a browser with no build step. It is deliberately **not** a route in
`frontend/public`; promoting it into the Next.js app is a separate decision. The chosen structure
is "Before/After Desk": the page opens on the artifacts hiring actually runs on today (an Excel
headcount tracker, a `RE: RE: FW:` approval thread) and replaces each with the record that
supersedes it. It inherits the shipped "Clear Pipeline" tokens rather than forking them.

**What it is careful not to claim.** Confirmed with the product owner before writing: no named
customers or logos, no MMK pricing, and **no PDPA/GDPR/SOC badges** — none of those are real, and
a compliance badge implying certification would have been the worst thing on the page. Target
industries read as "built for". The one authorised claim is the **99.9% Enterprise uptime SLA**,
which is *not yet recorded in any ADR* — see the follow-up below.

**Two real contrast failures found, both inherited, both affecting the product:**
- `RecruitOps_Design_System.md` §9 states all token pairs are "pre-checked" at ≥4.5:1. They are
  not. `ink-400` (`#8A99A3`) on `surface-50` measures **2.77:1**, and it is used for meta text.
- §2 states "text on tint backgrounds always uses the matching `-600` color (WCAG AA
  guaranteed)". False for **all four** semantic pairs at pill sizes: warning **2.97**, success
  **3.62**, danger **4.08**, info **4.23**. `StatusPill` is the design system's signature
  component, so this ships in both frontends today.
  The landing page fixed both locally (meta text at `ink-600`; new `-700` tint-text steps).
  **The shared preset and `StatusPill` were fixed the same day** — see the design-system entry
  above, which adopted the same `-700` steps so the two surfaces agree.

**Also found:** `RecruitOps_Design_System.md` never went through the 2026-07-27 pivot — it still
describes "a B2B Recruitment Agency Platform (RAaaS)", client tiers, a client feedback bar and the
deleted `Sent to Client` / `Placed` vocabulary. `packages/ui` still exports `ClientPortalCard`,
`ClientFeedbackBar` and `ExpiryAttentionCard`, kept alive only by
`challenger_signature_edgecases.test.tsx` (and `signatureComponents.test.tsx`, which the first
sweep missed). **Fixed the same day** — see the design-system entry above.

**Touched:**
- `marketing/landing.html` (new)
- `PRODUCT.md`, `DESIGN.md` (new, repo root) — product truth and the built visual system
- `.claude/skills/impeccable/`, `.claude/agents/impeccable-*.md`, `.impeccable/config.json` (new)

**Follow-ups:** write the 99.9% SLA into an ADR and the commercial terms before the page goes
live; fix the tint-pill contrast in `packages/ui`; pivot the design-system doc.

## 2026-08-16

### ♻️ A rejected requisition can be revised and resubmitted, in rounds (ADR-0023)
**Why:** the product owner's request — *"if it gets rejected, let it be corrected and
resubmitted."* `Rejected` was terminal: edits and submits were both `Draft`-only, so the only way
forward was to raise a brand-new requisition, stranding the rejection comment — the one sentence
that says what to fix — on a dead record. Notably that rule was enforced by two `if` statements
and covered by **no test**, so nothing had to be inverted to change it, and nothing was guarding
it either way.

**Shape:** `Rejected → Draft` for the requester (`POST /api/requisitions/{id}/revise`), then edit,
then resubmit. Each submission is a **round**; resubmitting stamps out fresh steps at round *n+1*
and leaves round *n* verbatim. Each round is decided afresh from step 1 — carrying an earlier
`Approved` forward would credit an approval to a document nobody approved. `Approved` and
`Cancelled` stay terminal.

**The part that was easy to get wrong:** five sites read `RequisitionApprovals` and all five
assumed one round. Unscoped, a second round does not throw — the completion check
(`approvals.All(a => a.Decision == Approved)`) can *never* be true once a rejected round is
preserved, so a fully-approved round 2 would sit in `PendingApproval` with no `Waiting` step,
invisible in every inbox, silently. All five are now round-scoped.

**Touched:**
- `RequisitionApproval.cs` — new `Round` (int, default 1); `AppDbContext.cs` unique index widened
  `(RequisitionId, Sequence)` → `(RequisitionId, Round, Sequence)`. Without that widening a second
  round violates the constraint on real Postgres and **would not fail the suite**, which runs on
  the in-memory provider.
- `RequisitionService.cs` — new `ReviseAsync`; `SubmitAsync` opens round *n+1*; `DecideAsync`,
  `GetInboxAsync`, `FetchListAsync` and `BuildDetailAsync` all round-scoped.
- `RequisitionsController.cs` — `POST /{id}/revise`, gated on `requisitions:update` (revising is
  the requester's authority, not the approver's — same reasoning as cancel, ADR-0022).
- `RequisitionDetailPage.tsx` — timeline grouped by round, "Attempt 1 — superseded" / "Attempt 2 —
  current"; React keys moved from `sequence` to `round-sequence`, which would otherwise collide.
- `RequisitionReviseAndResubmitTests.cs` (new, 6 tests).

### 🔐 A later approver may approve forward, but never reject forward (ADR-0024)
**Why:** the product owner's request — *"I'm number 2 and you're number 1: I can skip over you and
approve both 1 and 2. But it has to show the record of what I did."* The chain models an org
hierarchy, and a senior willing to sign off should not be blocked by a junior on leave.

**This reverses a deliberate decision.** `GetInboxAsync`'s comment said the lowest-sequence
`Waiting` step must be the caller's *"or a later approver could act early"*, and three tests
asserted it. Those tests were **re-scoped, not deleted** — deleting them would have removed the
only proof that the limits still hold.

**The limits, and why they are not symmetric:** approving forward removes a junior's step but not
their say — the requisition proceeds, which their approval would have caused anyway. Rejecting
forward would *end* it before the junior ever saw it, substituting the senior's opinion for a
review that never happened. So reject stays bound to the caller's own step. Skipping *forward*
(an earlier approver reaching a later step) also stays blocked — the rule reaches down only.

**Seniority is chain position and nothing else.** No rank attribute was added to users or roles;
`auth-and-tenancy.md:46` deliberately removed seniority from the role model, and reintroducing it
to serve one feature would contradict that.

**Touched:**
- `RequisitionApproval.cs` — new nullable `DecidedByUserId`; null means the assignee decided it
  themselves, so every pre-existing row stays correct with no backfill. `ApproverUserId` is
  deliberately *not* overwritten: that would make the row claim the senior was always the
  assignee, which is false and unfalsifiable afterwards.
- `RequisitionService.DecideAsync` — selects the caller's *own* waiting step, closes everything at
  or below it, stamps the real decider on steps that were not theirs.
- `GetInboxAsync` — second filtering pass dropped; not-yet-your-turn work is now surfaced and
  *marked* rather than hidden, or the feature would be undiscoverable.
- `RequisitionListItemDto` / `packages/types` — new `yourStepLabel` alongside `awaitingApprovalFrom`.
  The Inbox's "Your step" column previously showed `awaitingApprovalFrom`, which for a senior
  names *someone else entirely*.
- `RequisitionDetailPage.tsx` — names the steps being closed on others' behalf before the click,
  hides Reject when it would be forward, renders "Approved by Finance on behalf of HR".
- 3 tests re-scoped (`A_Later_Approver_Cannot_Jump_The_Queue` → an approve/reject pair,
  `Inbox_Only_Shows_Requisitions_Waiting_On_You`, `Sequential_Approval_Logic_Enforces_Step_Order`).

**Migration:** `20260815183136_AddApprovalRoundAndActualDecider` — one migration for both features,
additive, `Round` defaulting to 1 so existing rows backfill. **Proposed, not applied**, per
CLAUDE.md. Reworked after `db-schema-reviewer`: the generated version dropped the old index first
inside EF's single transaction, and because Postgres holds DDL locks until commit, that exclusive
lock covered the whole `CREATE INDEX` build — blocking reads as well as writes on
`RequisitionApprovals` for its duration. Now: add columns → `CREATE UNIQUE INDEX CONCURRENTLY`
outside a transaction → drop the old index. `Down()` was reordered so it fails *before* destroying
the audit columns on any database that has seen a second round, and carries a warning saying so.

**Reviews:** `security-reviewer` (mandatory for authorization changes per CLAUDE.md) found no
exploitable bypass, no oracle regression, and no way for a client to set `DecidedByUserId`. It
raised one real test gap — the existing no-oracle test probes with a caller who lacks `approve` and
is stopped at the policy layer, so it never reaches the guard ordering it claims to protect. Closed
by `An_Approver_Who_Holds_The_Permission_Still_Learns_Nothing_From_A_Requisition_Not_Theirs`.

**Mutation-checked, and it found a real hole.** Removing the skip-reject guard turned 3 tests red;
removing `DecideAsync`'s round scoping turned 1 red. But removing `GetInboxAsync`'s round scoping
left the **entire suite green** — every existing test uses a chain identical across rounds, so
nothing constructed the case the guard exists for. `An_Approver_Dropped_From_The_Chain_Between_Rounds_Loses_The_Inbox_Item`
was written to close that, and the mutation now fails as it should.

## 2026-08-15

### 🔐 Approval authority is now permission-driven, not a hardcoded role literal (ADR-0022)
**Why:** the user's actual request — *"We can config who can do the approval chain base on who
request or which department."* Module 1.3 already says the chain is "configurable per company, not
hard-coded," and `permission:requisitions:requisitions:*` / `permission:settings:settings:*` were
already seeded and displayed in the Role Builder — but `RequisitionsController` and
`ApprovalChainsController` gated on `RequireRole` role literals, so granting or revoking those
permissions through the Role Builder changed nothing. See `.agents/tw2/explorer_ac_1/analysis.md`
(Finding B) and `PROJECT.md` (teamwork run `tw2`, milestone M2).

**Touched:**
- `RequisitionsController.cs` — controller-level `[Authorize(Policy = Policies.InternalUser)]`
  replaced with bare `[Authorize]` + a per-action `[HasPermission("permission:requisitions:
  requisitions:*")]` (read / approve / create / update / approve / update on list / inbox / get /
  create / update / submit / decision / cancel respectively — see ADR-0022 for the full table).
- `ApprovalChainsController.cs` — controller-level `[Authorize(Policy = Policies.AdminOnly)]`
  replaced with bare `[Authorize]` + `[HasPermission("permission:settings:settings:read")]` on the
  reads and `...settings:update` on create.
- `backend/tests/RecruitOps.Api.Tests/RequisitionPermissionAuthorityTests.cs` (new) — proves an
  Approver can decide while a HiringManager 403s; a role without `approve` cannot reach `/inbox`
  while one that holds it sees real work; HrDirector can now read chains but not create one; **a
  brand-new custom role, created and granted only `requisitions:approve` through the Role Builder in
  the test itself, can actually decide a requisition** — the point of the milestone; and the
  permission gate does not bypass department scoping on create.
- `RequisitionApprovalFlowTests.cs` — 4 tests updated from `404` to `403` for Approver hitting
  `/submit`, `/decision` (via Recruiter), `PUT`, `/cancel`: those actions now require
  `requisitions:update` (which `Approver` never held), so the policy layer stops the call before
  `RequisitionService.IsOwnerOrCompanyWide` is reached. The guarantee itself — an Approver cannot
  submit, edit, or cancel someone else's requisition — is unchanged; it is enforced earlier.
- `docs/decisions/ADR-0022-permission-driven-requisition-authority.md` (new).
- `docs/status/FEATURE-STATUS.md` — Module 1 section updated with the permission-driven note and the
  corrected `approvalchains` access description.

**Behaviour changes (intended, all recorded in ADR-0022):** Recruiter **keeps** create and update on
requisitions — the first cut of this change would have removed them, because `Policies.InternalUser`
let recruiters raise requisitions while the seed granted them `requisitions:read` only. Making
permissions authoritative surfaced that contradiction, and it was resolved by correcting the seed
(`RbacSeedData.cs` now grants Recruiter `requisitions:create` and `requisitions:update`) rather than
by letting a capability quietly disappear. The two codes are granted as a **pair** on purpose: the
flow is create → edit the draft → submit, and both the edit and submit endpoints are gated on
`update`, so `create` alone would produce requisitions their author could neither fix nor submit.
`requisitions:approve` is still withheld — raising headcount is not authority to approve it.
Pinned by `Recruiter_Can_Raise_And_Submit_A_Requisition_But_Cannot_Approve_It`, and the Domain
seed test moved from 23 to 25 permissions for Recruiter with the two new codes asserted by name.
`GET /inbox` narrows to roles holding `requisitions:approve` (Recruiter/HiringManager's
inbox already returned `[]`, so nothing visible today disappears); `GET /approvalchains` widens to
`HrDirector` (fixes a real defect — `Sidebar.tsx` already showed the nav item to `settings:read`
holders while the API demanded `Admin`); creating a chain still effectively requires `Admin` (only
role seeded with `settings:update`) but is now expressed as a grantable permission.

**Verified:** `dotnet build backend/src/Api` clean; `dotnet test backend/RecruitOps.sln` — 51 Domain
+ 464 Api = 515/515 passing (up from the 507 recorded 2026-08-13; +8 net from the new file and the
updated assertions). Mutation-checked: removing `[HasPermission]` from `POST /{id}/decision` turns
`HiringManager_Without_Approve_Permission_Gets_403_On_Decision_While_Approver_Succeeds` red (expected
`Forbidden`, got `NotFound`), confirming the new test exercises the attribute rather than passing
regardless of it.

---

## 2026-08-13

### ✅ Delivery & Deployment Prerequisites Complete (ADR-0004 & ADR-0007)
**Why:** Provide the operational, versioning, feature-flag add-on gating, container security packaging, and documentation prerequisites required for per-company single-tenant installations.

**Touched:**
- **Feature Flags Engine**: Implemented `IFeatureFlagService`, `FeatureFlagService`, `[FeatureGate("FeatureName")]` attribute, and `FeatureGateFilter` returning 403 Forbidden with `FeatureDisabled` payload for disabled add-on features. Added `useFeatureFlags` hook, `<FeatureGate>` wrapper component, and dynamic navigation filtering in `Sidebar.tsx`.
- **System Versioning & Health**: Implemented `GET /api/version` (`VersionController`) returning version string, environment, timestamp, deployment tier, and active feature flags dictionary. Added `/health` alias endpoint to `HealthController`.
- **Production Container Packaging**: Created `docker-compose.prod.yml` and `infra/nginx/nginx.conf` reverse proxy topology, dropping API port publishing in production and configuring `ReverseProxy__TrustForwardedHeaders=true`.
- **Operational Documentation**: Created `docs/architecture/deployment-runbook.md` (automated EF Core startup migrations, Postgres backup/restore runbook, upgrade procedures) and `docs/architecture/server-sizing-guide.md` (vCPU/RAM/Storage matrix by company tier).
- **Test Suite**: Added `FeatureFlagAndVersionTests.cs` and `FeatureGate.test.tsx`, verifying 507/507 backend tests pass and 318/318 frontend tests pass across all workspaces with 0 TypeScript errors.

---

## 2026-08-12

### 🔴 Fixed: the AI endpoints invented candidates instead of reporting the feature was off
**Why:** `ClaudeOptions`/`GeminiOptions` shipped `EnableFallback = true` and `RequireApiKey = false`,
and `appsettings.json` carried **no `AI` section at all** — so those defaults were what a customer
install got. With no API key, `POST /api/ai/parse-resume` answered **200** with a hardcoded
"Aung Kyaw Thu / +959123456789 / Tech Myanmar Solutions", and `match-candidate` answered **88%
"Strong Fit"** for every candidate–job pair; the stub ignored the request entirely. Worse, both
clients wrapped the provider call in `catch (Exception)` and returned the same stub, so an invalid
key (401), a rate limit (429), a timeout or a network fault also produced a fabricated analysis
under a 200. ADR-0008 makes AI optional and key-gated; "optional" has to mean the feature reports
itself off, not that it makes something up. The CV pipeline writes a confirmed parsed profile to the
candidate record, so this path put invented PII in the database — and a recruiter has no way to tell
a stub from a real answer.

**Now:**
- `EnableFallback` defaults to **false**, and `appsettings.json` declares the `AI` section
  explicitly with a note on why. No key → **402**, as the contract in `PROJECT.md` always said.
- The stubs survive only as a local-development convenience (`appsettings.Development.json`), and
  every stubbed response is stamped **`X-Ai-Simulated: true`** via the new scoped
  `IAiSimulationScope`, set by the provider clients and read by `AiController`.
- A configured key never falls back. Any unusable provider outcome — non-success status, timeout,
  transport fault, unexpected body shape — raises the new `AiProviderUnavailableException` and
  surfaces as **502**. A caller-cancelled request stays a cancellation.
- `RequireApiKey` and the `X-Require-Api-Key` request header are **gone**. A client-supplied header
  deciding server-side gating was the only reason the 402 tests passed, and nothing in the frontend
  ever sent it — the default that actually shipped had no test at all.
- `AiController`'s five copies of the same `try/catch` are one `RunAsync` helper, so the sibling
  rule this repo keeps breaking cannot be applied to four endpoints out of five.

**Tests:** 468 → **484**. `AiApiKeyGatingDefaultsTests` boots the API through the new
`NoAiFallbackWebAppFactory` — the configuration a customer install actually gets — and asserts 402
plus "the body never contains `Aung Kyaw Thu` or `Strong Fit`". `AiStressAndResilienceTests` had
four tests *asserting the fabrication was correct behaviour* ("Handles_Http_Errors_Gracefully"
expected `Aung Kyaw Thu`); they now assert `AiProviderUnavailableException`. Both directions were
proved by mutation: dropping the `EnableFallback` check fails 6 gating tests, and restoring the
`catch → stub` line fails 6 resilience tests.

**Touched:** `Infrastructure/Options/{Claude,Gemini}Options.cs`,
`Infrastructure/Services/{Claude,Gemini}ApiClient.cs`, `Infrastructure/Services/AiSimulationScope.cs`
(new), `Infrastructure/DependencyInjection.cs`, `Application/Interfaces/IAiSimulationScope.cs` (new),
`Application/Common/Exceptions/AiProviderUnavailableException.cs` (new),
`Api/Controllers/AiController.cs`, `Api/appsettings.json`, `Api/appsettings.Development.json`,
and four test files.

**Still open:** the internal SPA renders a 502 through its existing non-402 error banner, which is
correct but generic, and it ignores `X-Ai-Simulated` — so a demo environment running with the
development fallback still shows "88% Match" with no marker on screen. Surfacing that header in
`SmartMatchBreakdown` and `ExecutiveSummaryPanel` is the obvious follow-up.

## 2026-08-03

### 🔴 Fixed: permission-aware UX was fail-open — every user saw the full admin UI
**Why:** `hasPermission()` in `frontend/internal/src/lib/auth.ts` returned `true` for a null
session and for a session whose `permissions` array was absent, the latter commented as a
"legacy" fallback. It was not legacy. `LoginResponse` on the backend had **no `Permissions`
member at all** — the record was `(AccessToken, ExpiresAtUtc, Role, DisplayName, UserId)` —
so `session.permissions` was permanently `undefined` and that branch was the *only* one
non-admin users ever reached. Every permission-gated control across 12 files — sidebar
filtering, `RequirePermission` route guards, and the create/update/delete/approve/publish
buttons on requisitions, postings, interviews, scorecards, users and roles — rendered for
everyone. The Milestone 5 "Permission-Aware UX Adaptivity (R5)" work was inert from the day
it shipped.

**Not a privilege escalation.** `PermissionAuthorizationHandler` re-derives permissions
server-side from the signed JWT and never trusts client state, so the API always enforced
correctly. This was UI disclosure: users saw controls that would have 403'd on click.

**Touched:**
- `backend/src/Application/DTOs/LoginResponse.cs` — added required
  `IReadOnlyCollection<string> Permissions`.
- `backend/src/Infrastructure/Services/AuthService.cs` — injects `IPermissionEvaluator`,
  populates the set via `GetUserPermissionsAsync(user.Id, user.TenantId, ct)`.
- `packages/types/src/index.ts` — `permissions` on `LoginResponse` optional → **required**,
  so the compiler flags anything constructing the old shape.
- `frontend/internal/src/lib/auth.ts` — `hasPermission()` fails closed on null session and on
  absent/empty permissions. Admin/SuperAdmin bypass retained, mirroring the server handler.
- Tests: new `frontend/internal/src/lib/auth.test.ts` (9), regressions added to
  `RequirePermission.test.tsx` and `AppLayout.test.tsx`, two new API tests in
  `AuthLoginTests.cs` asserting the permission set reaches the wire.

⚠️ **Why the existing tests missed it:** every test constructed its session with an explicit
`permissions` array — a shape the API never produced. The suites exercised a fiction. Making
the field required surfaced **8 more call sites** doing the same thing, and flipping the
default broke **8 tests across 4 files** that had been passing only because of the fail-open
(three suites rendered permission-gated pages with *no session at all*). Those now establish
the session their scenario requires.

**Verified:** backend 228/228 (51 domain + 177 api), frontend 189/189 across 22 files,
`npm run typecheck` clean. `security-reviewer` found no exploitable issue and confirmed the
server-side boundary independently re-derives permissions from the validated token.

**Carried forward (pre-existing, not introduced here):** `AuthService.LoginAsync` resolves the
account with `FirstOrDefaultAsync` on email across all tenants, so a duplicate email in two
tenants resolves an arbitrary one. Already noted in the code's own comment; needs a tenant
selector or a DB-level uniqueness guarantee.

### 🔧 LSP code intelligence wired up (TypeScript + C#)
**Why:** Symbol-level navigation (`goToDefinition` / `findReferences` / `hover`) is more
reliable than text search for tracing shared DTOs in `packages/types` to their backend
counterparts, and for following Application interfaces to their Infrastructure
implementations. `grep`/`glob` are now demoted to text and non-code files.

**Touched:**
- `.mcp.json` — added the `lsp` MCP server (`lsp-mcp-server`, 29 `lsp_*` tools) alongside
  the existing `github` server.
- `.lsp-mcp.json` (new) — declares two language servers: `typescript` (overrides the
  built-in default to use the repo-local `typescript-language-server` rather than requiring
  a global install) and `csharp` (custom; `csharp-ls` is not one of the bridge's ten
  built-ins). `requestTimeout` raised to 120 s for Roslyn's solution load.
- `package.json` — `lsp-mcp-server` and `typescript-language-server` added as exact-pinned
  root devDependencies. `csharp-ls` 0.26.0 installed as a **global .NET tool** — the one
  manual per-developer step.
- `CLAUDE.md` — new "Code Intelligence & LSP Guidelines" section with setup + the warm-up
  gotcha below.

**Verified end-to-end**, not just configured: both servers start and answer over stdio.
C# `HasPermissionAttribute` → 18 references across 6 files (controllers + tests).

**All five workspace roots verified live** after restart: `backend/` (csharp-ls, including
the test projects), `frontend/internal`, `frontend/public`, `packages/ui`, `packages/types`.
`packages/ui` and `packages/types` have no `tsconfig.json` and resolve via `package.json`
correctly; Next.js bracket routes (`app/jobs/[token]/page.tsx`) work.

⚠️ **Two gotchas found during verification**, both now in CLAUDE.md:

1. **Cold `find_references` under-reports on TypeScript.** `tsserver` only searches files it
   has opened. `hasPermission` returned **1 reference / 1 file** cold and **16 references /
   7 files** after `lsp_index_files`. A one-file answer means "not indexed", not "unused".
   C# is unaffected (Roslyn loads the whole solution up front).

2. **One tsserver per workspace — query shared types from the consumer side.** Asking for
   `LoginResponse` references *from* `packages/types` (where it is defined) returns **1**,
   the declaration alone; asking from `frontend/internal` returns **5**, including the
   declaration. References resolve outward through the import, never inward. Since
   `@recruitops/types` is consumed by `frontend/internal`, `frontend/public` and
   `packages/ui`, a complete contract-consumer sweep means one query per consuming
   workspace — running it from `packages/types` returns a confident, empty-looking answer.

---

## 2026-07-30

### ✅ Granular Dynamic RBAC, User Management, Permission-Aware UX & Full E2E Verification
**Why:** Transition RecruitOps from static role-based checks to a fine-grained, dynamic Role-Based Access Control (RBAC) architecture with user directory management, custom role builder matrix, and permission-aware UX adaptivity across the entire internal web application.

**Touched:**
- **Audit Remediation (R1)**: Fixed PostgreSQL LINQ translation error in `UsersController`, resolved `AuthLoginTests` async/await timing issue, and upgraded `System.Security.Cryptography.Xml` to `10.0.6` to eliminate security vulnerabilities.
- **RBAC Data Model (R2)**: Implemented domain entities (`Role`, `Permission`, `RolePermission`, `UserRoleAssignment`), database migration (`20260730000000_AddGranularRbac`), and seed data (`RbacSeedData`) mapping system roles to canonical permission strings (`permission:module:feature:action`).
- **Backend Authorization Engine & REST APIs (R3)**: Built `[HasPermission]` policy attribute, `PermissionRequirement`, and `PermissionAuthorizationHandler` supporting SuperAdmin/Admin bypass and cached evaluation. Created `PermissionsController` (`/api/permissions`), `RolesController` (`/api/roles`), and expanded `UsersController` (`/api/users`).
- **Frontend UI & Components (R4)**: Implemented `UserDirectoryService`, `RoleService`, `UsersPage` (with pagination, role/status filtering, user creation/editing, self-deactivation & last-admin safeguards), `RolesPage`, and `PermissionMatrixGrid`.
- **Permission-Aware UX Adaptivity (R5)**: Updated `hasPermission` helper in `auth.ts`, dynamically filtered sidebar links in `AppLayout.tsx`, added action button permission gates across all pages (`RequisitionsPage`, `RequisitionDetailPage`, `JobPostingsPage`, `JobPostingDetailPage`, `InterviewDetailPage`, `UsersPage`, `RolesPage`), and integrated `RequirePermission` route guards.
- **Test Suite Expansions & E2E Verification (R6)**: Expanded backend tests (`DynamicAuthorizationEngineTests.cs`), verifying 226/226 backend tests pass (51 Domain + 175 Api). Added frontend test suites (`AppLayout.test.tsx`, `RequirePermission.test.tsx`), verifying 60/60 frontend tests pass across 10 test files. Verified 0 TypeScript errors and Vite production build success.

---

## 2026-07-29

### ✅ ADR-0019 closed — 169/169 green, and a human has read the diff
**Why:** `GET /api/users/selectable` is an authorization change, and CLAUDE.md requires explicit
human sign-off on those. A test suite written by the same author as the endpoint is not that
review. Both halves are now done: CI is green on 39 domain + 130 api, and the
`UsersController` diff (bare `[Authorize]` on the class, `AdminOnly` on `Get`,
`RecruitmentStaff` on `selectable`) has been reviewed.

**Touched:** nothing in code — this entry records verification, which is the point. The 🔴/🟡
rows in FEATURE-STATUS.md are closed.

### ✅ The stack came up — Module 3's UI is no longer "never run"
`docker compose up --build` brings up Postgres + API + both frontends with migrations applying
on startup, and the five Module 3 screens have been driven for the first time. That retires the
"written but never run" pattern for the frontend the way CI retired it for the backend.

⚠️ **"The stack came up" is not "the screens are correct."** Three behaviours were named as
worth checking specifically and have *not* been eyeballed yet — they are carried forward in
NEXT-SESSION.md rather than quietly dropped:

- the **panel picker populated as a Recruiter** — ADR-0019's whole reason to exist, and it has
  still never been *observed* working, only proved reachable by a test
- the **blind state** on `/interviews/:id` with two panel members
- **`.mention` styling surviving the Tailwind build** — the markup is generated in C#, so the
  content scanner cannot see the class and would purge it; it lives in `index.css` for that
  reason, which is exactly the kind of arrangement a build quietly breaks

### 🔧 The `Test counts` step is now the only thing left in CI that can lie
The suite is green and the **build's exit code is the authority** — one `RUN` per test project
plus `RunConfiguration.TreatNoTestsAsError` makes a green `docker build --target test`
impossible unless both projects ran and every case passed. What remains broken is the *reporting*
step, which still cannot reliably lift per-assembly counts out of a BuildKit log.

It can no longer fail a passing suite (that vote was taken away from it after run #5), so this is
cosmetic. But it is the fourth appearance of one mistake — empty box, a confident `21` against a
runner-reported `122`, a red tick over a green suite, and now a summary that sometimes just
isn't there. **Fix it or delete it.** A half-trusted instrument is worse than either a working
one or none, because it costs a reader time to decide which reading this one is.

---

## 2026-07-28

### 🔴 `GET /api/users/selectable` was unreachable by the role it was written for
**Why:** `UsersController` carried `[Authorize(Policy = AdminOnly)]` at the **class** level and
`[Authorize(Policy = RecruitmentStaff)]` on the new action, intending the action to opt *down*.
**ASP.NET Core authorization attributes are additive** — an action-level attribute is evaluated
*in addition to* a class-level one, never instead of it. The effective requirement was
`AdminOnly` **AND** `RecruitmentStaff`: only an Admin could call it, and a **Recruiter got 403**.

So the endpoint shipped reproducing the exact condition ADR-0019 exists to remove. A Recruiter
who cannot list users cannot name an interview panel, and the panel is required and non-empty —
**Module 3 scheduling was undrivable by the role it was opened to for the second time, for the
same underlying reason.** First the endpoint was missing; then it existed and was walled off.

Fixed: bare `[Authorize]` on the class, `AdminOnly` declared on `Get` itself. There is no way to
widen a policy from an action, so a future endpoint needing something weaker gets its own
attribute rather than a class-level policy someone tries to override.

**Found by the tests written earlier the same day, on their first run** (CI #4): 8 of the 11 new
cases failed, and it was exactly the 8 that require a 200 from `selectable`. The three that
passed assert a *refusal* — HiringManager 403, Approver 403, unauthenticated 401 — every one of
which a too-strict policy satisfies by accident. **A suite that only tested "the wrong people are
kept out" would have been green over an endpoint nobody could reach.** ⚠️ The fix itself has not
been through a run yet.

### 🩺 The reporting step failed a green suite, so it no longer gets a vote
**Why:** run #5 (the auth fix) built and tested clean in 47s — and the job summary announced
*"No tests executed for: RecruitOps.Api.Tests"* about 130 tests that had just passed.

BuildKit truncates a step's log at **1MiB** and what it drops is the **end** — precisely where a
test run puts its summary. The reporting step lost the Api counts, and then adjudicated on their
absence.

**An instrument that contradicts the thing it measures is worse than no instrument.** This is the
third variation on one mistake in a single day: an empty box (run #3), a confidently wrong `21`
against a runner-reported `122` (run #4), and now a red tick over a green suite. Each previous
fix corrected the *pattern*; this one removes the possibility:

- **`BUILDKIT_STEP_LOG_MAX_SIZE: -1`** (and `..._MAX_SPEED`) on the build step. The log is read
  by a script, so a partial log is a wrong answer rather than a slow one.
- **The step reports; it does not adjudicate.** One `RUN` per project plus
  `TreatNoTestsAsError` means a green `docker build --target test` is *impossible* unless both
  projects ran and every case passed — so the build's exit code is the authority and there is
  nothing left to decide. A count it cannot read is now reported as a count it cannot read.
- The Api project's `--logger console;verbosity=detailed` and `--blame-crash` are gone. They
  answered the MSB4181 question (8 real failures, unsummarised) and their output volume was
  feeding the truncation. The triage recipe stays in a Dockerfile comment for next time.

### 📏 Backend counts, finally read off a run
`RecruitOps.Domain.Tests` **39/39**; `RecruitOps.Api.Tests` **130 total, 122 passed, 8 failed**
(CI #4). **169 backend cases**, and the source-counted figure this repo carried for three
sessions was wrong: the existing Api suite was **119**, not 117.

### 🐛 The `Test counts` step was reporting an empty box, and that is worse than nothing
**Why:** run #3 was green with a blank "Backend test run" summary. Two bugs stacked, and each
one alone would have been visible:

1. It grepped for **`Passed!`** — the *Microsoft Testing Platform* summary line. This solution
   runs on **VSTest** (`Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`), which ends with
   `Test Run Successful.` / `Total tests: 39` / `Passed: 39`. No exclamation mark anywhere, so
   the pattern matched nothing.
2. The `|| echo 'no test summary found'` fallback was attached to a **pipeline** ending in
   `sed`. A pipeline's exit status is its *last* command's, and `sed` exits 0 — so the fallback
   never fired and the failure printed as silence.

The result was a code block containing nothing, under a heading claiming to be a test report.
That is the same class of problem as a green tick nobody read, one layer up: **an empty report
looks like a report.** Now the pattern matches VSTest, the `Test run for …dll` lines are kept
so each count is attributable to an assembly (a `.sln` run emits one summary *per project*,
which is why "the" count was never one number), and the step **fails the job** if tests
demonstrably ran but their counts could not be extracted.

**First real figure: `RecruitOps.Domain.Tests` — 39/39, Test Run Successful.** Read off run #3's
raw log, not counted from source.

### 🧨 …and the count could not be attributed to an assembly at all
**Why:** run #3's log reads `Starting: RecruitOps.Api.Tests` and then, 48ms later,
`Test Run Successful. / Total tests: 39`. That is Domain's summary — `dotnet test` on a `.sln`
spawns **one vstest run per project on parallel MSBuild nodes** and interleaves their stdout
(the giveaway is two `A total of 1 test files matched` lines). But it is *indistinguishable at
a glance* from the Api project contributing zero. **A count you cannot attribute is not a
count**, and this repo has now been burned by that ambiguity three times.

Three changes, so the question cannot be asked again:

- **`Dockerfile`: one `RUN` per test project**, not `dotnet test RecruitOps.sln`. Two
  unambiguous summaries, and two independent exit codes — an Api failure can no longer be read
  as a Domain success. Both stay inside the `test` stage, so `--no-cache-filter=test` still
  busts both.
- **`RunConfiguration.TreatNoTestsAsError=true`** on each. By default a project that discovers
  **zero** tests exits 0 and the build stays green. That is precisely how a whole assembly
  could stop running and nobody would learn about it.
- **`ci.yml` counts cases per assembly itself**, off the `Passed RecruitOps.<X>.Tests.` lines
  rather than trusting any summary, renders them as a two-row table at the top of the job
  summary, and **fails the job** if either assembly executed nothing — naming which one went
  quiet. Verified by replaying run #3's log through the script: it reports `Api=0` and exits 1.

### 🔐 The ADR-0019 authorization change finally has a test
**Why:** `GET /api/users/selectable` was the last 🔴 in FEATURE-STATUS — an authorization
change with zero tests. It was written in a session with no .NET SDK, shipped alongside
Module 3's UI, and the existing suite could never have noticed the problem it solves: the
Module 3 tests post user ids they already hold, so nothing ever asked whether the role the
scheduling endpoint was opened to could *obtain* one.

**`backend/tests/RecruitOps.Api.Tests/UserDirectoryTests.cs` — 11 cases.** The one that matters
asserts **both halves in a single test**: a Recruiter gets 200 on `/api/users/selectable` and
403 on `/api/users`. Split in two, a later edit that widened the full directory would leave a
green test named "a recruiter can read selectable" standing over the hole. HrDirector gets its
own case, because `RecruitmentStaff` is three roles and "a rule reaching two of three siblings"
is the bug this repo keeps shipping.

The no-email assertion runs against the **raw JSON**, not a deserialised `SelectableUserDto` —
reading into the DTO would drop an email property silently and report green, and what crosses
the wire is the whole argument of ADR-0019. Also pinned: an Approver **is** on the list (ADR-0018
removed their standing reach, not their eligibility for a panel — ADR-0017 §4), the picker is not
department-scoped, `Role` survives as a string (the in-memory projection EF Core 10 requires),
Admin still reads both, HiringManager and Approver get 403 on both, unauthenticated gets 401,
and the tenant filter empties the list for another tenant.

⚠️ **Written, not verified.** No SDK in the authoring environment and `nuget.org` blocked, so
this file has never been compiled. It is the same "written but never run" state CI exists to
end — it just needs a push. **The count to look for is 167** (39 domain + 128 API); 156 means
the new file did not compile in.

⚠️ **Still needs human review.** Per CLAUDE.md an authorization change is not done until a
person has read it; a test suite the author also wrote is not that review.

### 🚀 The repo has a history, a remote, and a green build
**Why:** everything since the scaffold commit — the pivot, Modules 1–3, both frontends, the
whole docs/ knowledge base, 301 files — was sitting uncommitted in a single working tree, on
`master`, with no remote. "Unbuilt" was a property of the repo rather than something CI could
report.

Replayed as **nine area-based commits** plus one cleanup: build/packaging → the pivot → auth,
tenancy and departments → Module 1 → Module 2 → Module 3 → the frontend split → tests and CI →
docs. Readable, not bisectable: the tree had never been compiled, and shared files
(`AppDbContext`, `Program.cs`, `DependencyInjection`) can only be committed once, so
intermediate commits do not build. Only the tip is meant to.

`master` → `main`; the agency-era `feat/client-crm-list` branch is gone (ADR-0001 superseded it
months of work ago); the duplicate reference `.docx` at the repo root is gone; `.gitignore`
paths are unanchored so they still match after the ADR-0012 split.

**CI's first run was green on both jobs — the backend compiles.** The ADR-0018 security fix and
the ADR-0019 endpoint, written across three sessions with no .NET SDK, have now been through a
compiler. Actions moved off the deprecated Node 20 runtime (`checkout@v5`, `setup-node@v5`,
`setup-buildx-action@v4`) and the app is built on Node 22, since 20 went EOL in April 2026.

⚠️ **Green is not the same as "the new tests ran"** — the recurring trap in this repo. A
`Test counts` step now lifts the `Passed!` lines out of BuildKit's output into the job summary.
Nobody has read that number yet, so the ≈156 in FEATURE-STATUS is still counted from source.

**Three things learned the expensive way, all now in NEXT-SESSION.md:** git cannot run from the
sandbox mount at all (it refuses `unlink` and `O_EXCL`, so locks can be neither created nor
cleared); a crashed git leaves `refs/heads/<branch>.lock` as well as the well-known two; and a
GitHub token needs `workflow` scope or it pushes 500 objects and has the ref rejected at the
last step.

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
