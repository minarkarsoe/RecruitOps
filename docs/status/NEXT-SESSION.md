# Next Session — pickup guide

**Last updated:** 2026-08-26 · **Backend 644/644 · Frontend 375/375 · typecheck clean · builds clean**
· **ADR-0026 is complete and fully security-reviewed** — all four steps built, the delivery log
reads the outbox, and the review found and fixed one HIGH (an Approver could put candidates in any
pipeline — [SECURITY-REVIEW-ADR-0026.md](SECURITY-REVIEW-ADR-0026.md))
· All seven modules have a drawn UI (25 screens)
· **ADR-0025 step 3: everything that reaches a screen is on V1.0.** The 43 remaining compat tokens
are 5 comments plus two orphaned folders the owner has parked — see 3e(iv). `dark:` is at zero.

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

**Where it stops.** One message leaves the building — the interview invitation. Everything else a
candidate should hear about (an offer, a reminder, a rejection) still has no handler. **Whether it
arrived is now visible**: the delivery log shipped 2026-08-25 (`/delivery`), so a failed or
suppressed message reaches the recruiter instead of only the database.

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
| Multi-tenancy | ✅ Query filters + claim resolver, isolation-tested · **super-admin `X-Tenant-Id` switching (2026-08-26)** — honoured only for a token carrying `is_super_admin`; see the warning in SECURITY-REVIEW-ADR-0026.md before touching `CurrentTenant` |
| Delivery (ADR-0004) | ✅ compose prod, `/api/version`, feature flags, sizing guide, runbook · ✅ in-process job runner (ADR-0026) · ⚠️ **every install now needs `Smtp:*` configured** or nothing is delivered |
| Background jobs (ADR-0026) | ✅ **complete and security-reviewed.** Queue + tenant seam + mail worker + SMTP + invitation handler + bulk CV worker + **delivery log** (`GET /api/delivery`, `/delivery`) · review 2026-08-26 found one HIGH in step 4, fixed · ⬜ Module 4/5/8 handlers |
| Tests | ✅ backend **644/644** (62 domain + 582 api) · frontend **375/375** across 46 files |
| Design | ✅ 25 static screens, all seven modules — `design/internal/index.html` |

## 🔴 CI was red for three days and nobody noticed — check it first

**Fixed 2026-08-28.** `gh run list --workflow=ci.yml` showed **fifteen consecutive failures**
going back to 2026-08-25, every one of them the same ten tests in
`ChallengerM12ConfigPrecedenceTests`, and every one of them passing locally. Full account in
`CHANGELOG.md`.

Two habits worth keeping from it:

1. **Check `gh run list` at the start of a session.** A red build that has been red for a while
   stops looking like news. Three days of pushes went on top of it.
2. **"Passes locally, fails in Docker" means your machine has something the image does not.**
   Twice in one day: the type-check leaned on a hoisted `@types/node` the image never installs,
   and these tests looked for a `backend/` directory that only exists in a checkout. Reach for
   `docker build --target test ./backend` to reproduce before theorising — it took one run.

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

### ✅ 3d(i) — the login screen, end to end (done 2026-08-21)

The pattern for every screen after it: open `design/internal/login.html`, build against it, then
**look at the result in a browser** rather than trusting the diff.

What the kit specifies that the shipped page did not have: the logo mark and wordmark, the error
as a tinted `role="alert"` block instead of loose red text, the password field outlined on
rejection while the email is not, a spinner in the button, the "no self-signup" line, and — the
one that is not styling — **the locked state with a real countdown**.

**Two functional defects fixed on the way, both invisible in the source:**

1. **A failed login said "Your session has expired. Please sign in again."** `apiFetch` mapped
   every 401 to that copy, and the refresh branch excluded `/auth/login` while the fallthrough
   did not. Someone mistyping a password was sent looking for a problem that did not exist. Now
   "Email or password is incorrect." — which is also the only thing ADR-0016 permits it to say,
   since naming the field tells a stranger whether an address belongs to a real employee.
2. **`Retry-After` was being thrown away.** The 429 carries the lockout in seconds and `ApiError`
   had nowhere to put it, so no countdown was renderable. `ApiError.retryAfterSeconds` now carries
   it, and the page counts down from the server's number rather than inventing one.

8 tests added, each mutation-checked. Frontend **352/352**.

### ✅ 3d(ii) — the app shell (done 2026-08-21)

`components/` is at **0**. The shell is what every screen sits inside, so this is the change with
the widest reach after the preset itself.

**The rail is dark now** — `bg-ink-900`, 224px — which is the kit's central layout decision, not a
colour preference: it is the second neutral layer, so the content surface reads as the workspace
and navigation recedes. A white sidebar beside a white content pane makes the two compete, and on
a screen that is mostly table it is the table that should win.

Also from the kit: an icon per nav item, the active item as a filled `bg-white/10` pill (the
`border-l-2` is gone — a border *and* a fill is two devices saying one thing), and the user block
in the rail footer.

**Identity now renders once.** It used to appear in both the header and the sidebar, and a test
asserted that duplication (`getAllByText(...).length === 2`). Two avatars is two places to check
who you are signed in as, and they can disagree while a session is being replaced.

> 🔴 **A contrast failure in the kit itself, found by measuring rather than trusting it.** The nav
> group labels are `text-white/40`, which on `ink-900` is **3.81:1** — below AA for 11px text.
> Raised to `white/50` (5.23:1) in the code **and in all 19 kit screens**, because `design/` is the
> source of truth and must not carry the defect. Rail contrast now: active `white` 17.85 · idle
> `white/70` 9.10 · role line `white/50` 5.23 · group label `white/50` 5.23 · avatar white on
> `brand-700` 5.47.

**What was deliberately NOT changed: the nav group names and membership.** The kit's rail shows
"Work" and "Configure"; the app has Recruitment / Insights / Team / Governance. Which items live
under which heading is product information architecture, not a design-system decision — worth
asking the product owner, not worth deciding inside a token migration.

### ✅ 3d(iii) — `pages/` (done 2026-08-21)

`pages/` is at **0**. Repo-wide 525 → 340.

Three things beyond the colour rename, each from the kit rather than from taste:

- **190 hard-coded text sizes are gone.** `text-[13px]`, `text-[15px]`, `text-[11px]` and friends
  bypassed the V1.0 scale entirely. They map onto it exactly — 13→`text-sm`, 15→`text-md`,
  11→`text-2xs` — so nothing moved visually and the scale is now real rather than decorative.
- **Four hand-rolled tables were put on the kit's treatment.** `UsersPage`, `RequisitionsPage`,
  `InboxPage` and `JobPostingsPage` each wrote their own `<table>` and each had drifted
  differently — three padding schemes, three type sizes, and all four with the **uppercase
  micro-caps header** the kit does not use. Now one treatment: `bg-canvas` header row,
  `px-4 py-2.5 font-medium text-ink-600` cells, no micro-caps.
- **Section headings and page titles are on the kit's scale.** Section `<h2>`s were 13px grey
  uppercase micro-caps, which reads as a label for the thing beside it rather than a heading for
  the block below it; the kit uses `text-base font-semibold` in 19 places and **never** puts
  uppercase on a heading. Page `<h1>`s went from `text-2xl font-bold` to the kit's in-app title,
  `text-xl font-semibold tracking-tight`. (The kit's own `text-3xl font-bold` headings are on its
  *spec* pages — do not copy those into the app.)

Verified in the browser on `/requisitions`: h1 18px/600/-0.45px tracking · header cell 13px/500
ink-600 with `text-transform: none` · header row `#F8FAFC` · body cell 14px ink-900 · the migrated
`StatusPill` rendering warn-700 on warn-50 in situ.

### ✅ 3e(i) — `features/pipeline/` (done 2026-08-25)

`features/pipeline` is at **0**. Honest repo-wide count 334 → **215**.

**Badge was failing AA and this change fixed it.** The `packages/ui` rebuild checked `StatusPill`
and never opened `Badge`, so three variants kept a **-500 as text on their own -50 tint** — the
failure the preset's comment warns about, in the same package. Measured 2026-08-25: `success`
2.41:1, `warning` 2.07:1, `danger` 3.44:1, all FAIL; on the -700 step, 5.21 / 4.84 / 5.91, all
PASS. **If you touch a component in `packages/ui`, run the contrast script on it — reading the
diff did not catch this twice.**

Four things beyond the rename, all read off `design/internal/board.html`:

- **Board columns are white on canvas**, not grey fills holding white cards — the kit's board is
  cards floating on the page, and a grey column makes the container louder than its contents.
- **Column counts are `font-mono tnum text-ink-500`, not eight coloured Badges.** A badge is a
  status; a count is a number that changes on every move.
- **Loading is a skeleton, empty is a sentence** — and the two terminal columns say what they
  cost, because "this closes the requisition" is a bad thing to learn by doing.
- **Stage history is the `.rail-step` rail**, which the kit reuses for exactly this.

`ExecutiveSummaryPanel`'s two hand-rolled toggle groups became one segmented control on the kit's
detented-filter pattern. They had used **brand for "selected" in one group and ink in the other**
— and brand is the action colour, so a filter painted brand reads as a button that will go and do
something.

> ⚠️ **Copy is not tokens.** Four test failures in this step were all me rewording strings a test
> asserts on ("No contact specified", "Zawgyi → Unicode Normalized", "CV Document Preview", the
> trailing `!`). Every one was reverted rather than the test edited. **Change presentation; leave
> the words alone unless the words are the task.** A fifth was a `tnum` span splitting a text node
> that a regex matched across — wrapping part of a sentence in a span is a functional change.

### ✅ 3e(ii) — `features/analytics/` (done 2026-08-25)

Analytics is at **0** real usages (5 remaining hits are comments quoting what was removed).
Repo-wide 215 → **55**. Frontend **358/358**.

**Two bugs, not restyling.**

**The analytics page rendered in dark mode on any dark-mode machine.** 97 `dark:` utilities in
this one folder, none anywhere else, and Tailwind's default `darkMode: 'media'` needs no opt-in.
Measured live with the OS in dark: body still light canvas, chart labels ink-400 on white at
**2.45:1**, skeletons `bg-ink-800`, the error banner near-black translucent red. The preset now
sets **`darkMode: 'class'`** — a stray `dark:` is inert until someone puts `.dark` on an ancestor.

**The eight-colour source palette failed the dataviz validator** — `indigo-500 ↔ purple-500`
ΔE **0.9** (protan) and `emerald-500 ↔ teal-500` ΔE **5.4** normal-vision, i.e. two pairs nobody
could separate. Replaced by one hue, because the chart is one measure across directly-labelled
categories. The kit's validated four-colour set is for the case that carries identity (one channel
in two charts) — do not reach for it on a single-series bar.

Bars now come from `.bar-track` / `.bar-fill` in `index.css`, ported from the kit. `ChartMarks.test.tsx`
pins one-hue, `aria-pressed`, `role="img"` and no-`dark:`; every assertion was proved against a
mutation, and **one initially passed** (a per-row colour set via inline `style` rather than a
class), so the check now covers both routes.

### ⚠️ Before 3e(iii): most of `features/` never reaches a screen

Measured 2026-08-25 — grep for each symbol outside its own file and its barrel:

| Component | Reaches a screen? |
|---|---|
| `features/analytics/*` | ✅ `AnalyticsPage` renders all five |
| `features/search/*` | ✅ `AppLayout` |
| `features/pipeline/BulkCvUploadModal` | ✅ `JobPostingDetailPage` |
| `features/pipeline/PipelineKanbanBoard` | ❌ barrel export only |
| `features/pipeline/CandidateSlideOver` | ❌ barrel export only |
| `features/pipeline/SmartMatchBreakdown` | ❌ only via `CandidateSlideOver` |
| `features/pipeline/ExecutiveSummaryPanel` | ❌ only via `CandidateSlideOver` |
| `features/interviews/BlindScorecardDrawer` | ❌ barrel export only |
| `features/requisitions/RequisitionTable` | ❌ barrel export only |
| `features/requisitions/RequisitionDrawer` | ❌ barrel export only |

`JobPostingDetailPage` hand-rolls its own pipeline list rather than using `PipelineKanbanBoard`,
and there is no Candidate 360 route at all — so the AI Smart Match and executive-summary panels,
which have 402-gating, skeletons and eleven tests, are **unreachable in the product**. The
`features/requisitions` orphan CLAUDE.md already warns about is not the exception; it is the rule.

**This was found only after `features/pipeline` had been migrated**, so four of those files were
restyled while orphaned. That work is not wasted if the components get wired up, and is wasted if
they get deleted — which is exactly why the question comes before 3e(iii) rather than after.

> **Decide before migrating `interviews` (24) and `requisitions` (14): wire up or delete.**
> Styling dead code is the one kind of effort here that cannot pay off either way.

### ✅ 3e(iii) — `frontend/public/app` (done 2026-08-25)

The public app is at **0**. Repo-wide 55 → **43**, and every one of those 43 is either a comment
quoting what was removed or inside the two orphaned feature folders below.

Built against `design/public/job.html` and `apply.html`, which are **not** the internal spec:
fields are `h-12`/15px against the internal `h-9`/14px, because this form is filled once, by a
stranger, on a phone. Reaching for the internal `Input` here would be consistency in the wrong
direction.

**`.tnum` did not exist in the public app** — `ds.css` and the internal `index.css` have it, the
port to `globals.css` dropped it, so `tnum` on the phone field and the salary figure did nothing.
Found by grepping the built stylesheet, not by looking. Added, with the mono ligature rule that
was missing for the same reason.

> ⚠️ **Verified from the built CSS, not a running page.** The job page needs the API and Docker
> was not up. The form has **never been eyeballed with real data**, and the public app has **no
> tests at all** — that is the least-covered surface in the repo and a stranger's only view of the
> product.

> ⚠️ **There is no `app/not-found.tsx`.** `notFound()` fires for an unknown or withdrawn token, so
> a candidate following a dead link gets Next's built-in 404: no layout, no fonts, no company
> name. `design/public/` does not draw this screen — it needs designing, not just building.

### ⏸ 3e(iv) — the last 38, PARKED by the product owner (2026-08-25)

`features/interviews` (24) and `features/requisitions` (14). **Both folders are orphaned** — see
the table above. Nothing else in either frontend carries a compat token, and `dark:` is at zero
repo-wide.

**The owner was asked and chose to leave them as they are for now, and to revisit later.** So:

- **Do not migrate, delete, or wire up these two folders** without asking again. Leaving them
  untouched is the current decision, not an oversight.
- **The compat block stays in `packages/ui/tailwind-preset.js`** until they are resolved. Its exit
  condition is a count of zero and the count is 43 — 5 comments plus these 38. Deleting the block
  early would leave both folders rendering unstyled, which is worse than leaving them off-brand.
- Everything that reaches a screen **is** on V1.0. The remaining 38 are the only exception, and
  they are invisible to a user because the code is unreachable.

Whoever picks this up: the question is still "wire up or delete", not "which classes". Restyling
them while they stay orphaned is the one option that finishes nothing.

> ⚠️ **The old exit-condition grep counted build output.** Pointed at `frontend/public` it also
> reads `.next/` — 78 hits there, **66 of them compiled artifacts**, so the number could never
> reach zero. The last entry's "340" was wrong for this reason. The preset now carries the
> corrected command (`--include` filters, `frontend/public/app`); use that one:
>
> ```
> grep -rEo "(primary|success|warning|danger|accent|surface|zinc|cyan|teal)-[0-9]+" \
>   --include=*.ts --include=*.tsx --include=*.css \
>   frontend/internal/src frontend/public/app packages/ui/src | wc -l
> ```

> ⚠️ **`features/requisitions/` (14) has zero importers repo-wide** and carries a test file.
> Re-verified 2026-08-25. Migrating it means styling dead code — decide whether to delete or wire
> it up *before* spending the effort.

> ⚠️ **`features/analytics` (165) is charts**, and charts are the one place raw Tailwind ramps are
> still in use (`indigo`, `sky`, `purple`, `blue`, `emerald`). Those are a categorical series
> palette, not a token gap — do not just rename them to `brand`/`positive`, which would make
> adjacent series indistinguishable. Pick the series order deliberately and check it for
> colour-vision separation.

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

✅ **All of it is done.** `RecruitOps_Design_System.md` landed 2026-08-27; step 4
(`marketing/landing.html`, `DESIGN.md`) landed 2026-08-26. See §3.

What is left of ADR-0025 is not a document but the **compat block** in
`packages/ui/tailwind-preset.js`, and it is now down to one blocker.

- ✅ `font-display` and `shadow-pop` — **deleted 2026-08-27**, both at 0 usages. Proved inert:
  the built stylesheet hashes to `index-DJXXbcKM.css` before *and* after, i.e. byte-identical.
- `rounded-full` — 38 usages (pills, avatars, rail nodes), still load-bearing.
- The 38 old-name colour usages sit **entirely inside the two parked orphan folders**. That is
  the **only** thing still holding the block open.

⚠️ Its exit-condition grep reports **43**, not 38 — five hits are comment prose in
`features/analytics/`, which is itself fully migrated. The comment in the preset now says so and
gives a per-file check; a count that cannot reach zero is not an exit condition.

### 1. ✅ ADR-0026 is built — all four steps. What is left is the *screen*.

[ADR-0026](../decisions/ADR-0026-outbound-delivery-and-background-jobs.md) is Accepted and, as of
2026-08-21, **implemented**. SMTP behind `IEmailSender` as the floor, a transactional
`OutboundMessage` outbox, in-process workers claiming due rows with a visibility timeout, and no
new NuGet package. (The ADR originally specified `FOR UPDATE SKIP LOCKED`; see its 2026-08-20
amendment for what that trade narrowed.)

**One thing it did NOT finish, and it is now the top item in this file:**

- ✅ ~~Nothing renders `OutboundMessages`.~~ **Built 2026-08-25** — `GET /api/delivery` +
  `/delivery`, from the Delivery log section of `design/internal/channels.html`. 11 API tests,
  10 component tests, all mutation-proven. See the entry below.
- ✅ ~~Step 4 has not been security-reviewed.~~ **Done 2026-08-26**, together with the delivery
  log — [SECURITY-REVIEW-ADR-0026.md](SECURITY-REVIEW-ADR-0026.md). **One HIGH finding, fixed:**
  an Approver could POST 50 CVs into any posting in the tenant and read the batch back, because
  `BulkResumeService` gated on `CanAccessAsync` alone. Reproduced against the running API (200 OK),
  fixed, and pinned by two mutation-proved tests in `ApproverReachTests`.

  > ⚠️ **That was the third instance of one mistake.** `IDepartmentAccess.CanAccessAsync` answers
  > "does this role work across departments" — which an Approver does — and is almost never the
  > whole question for candidate data. The report's recommendation is still open: make the
  > candidate-facing helper the only exported way to ask, so the next service cannot get half of
  > the rule. It changes a shared interface, so it wants its own change.

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

5. ✅ **The delivery log, done 2026-08-25.** `IDeliveryLogService` + `DeliveryController` +
   `DeliveryLogPage`, built from `design/internal/channels.html`. No migration, no new permission,
   no new package. Backend **623/623**, frontend **368/368**.

   **The whole risk in it is one indirection, and it is worth knowing before you touch the file.**
   Every other candidate-facing service reaches a department off a row it already holds — an
   application has a posting, a posting has a department. `OutboundMessage` has neither: it has
   `SubjectType` + `SubjectId`, a deliberately loose pointer, and a department is four joins away
   through it. So ADR-0003's filter is hand-written here, and it **fails closed**: a row whose
   subject cannot be resolved to a department is hidden from a department-scoped user rather than
   shown. The cost is that a scoped user's log goes quiet when a new kind ships without its join;
   the alternative is that every Hiring Manager silently gains the company's outbox the first time
   somebody forgets one. A missing row gets reported. A leak does not.

   > ⚠️ **A mutation survived the first ten tests.** Flipping that filter from fail-closed to
   > fail-open passed everything, because no test produced a row the log could not resolve.
   > `A_Message_Whose_Subject_Cannot_Be_Resolved_Is_Hidden_From_A_Scoped_User` exists because of
   > it. **If you add an `OutboundMessageKind`, add its subject join to `DeliveryLogService` and a
   > case to that test**, or scoped users simply stop seeing that kind.

   Authorization is `Policies.InternalUser` + explicit scoping in the service, exactly like
   `InterviewsController` — no new permission was invented. Note what that policy already does:
   `Interviewer` is not in it, so a panel member never reaches the log (their legitimate reach is
   one application, not the outbox), while `Approver` is in it and is turned away by the service
   instead, because ADR-0018 makes that a candidate-data decision rather than a routing one.

   There is deliberately **no retry button**: the worker already retries with backoff to the
   attempt cap, and a button that re-queues a row a human is looking at would race it for that row.

← **you are here: nothing outstanding in ADR-0026.** The open follow-up is the `IDepartmentAccess` refactor the security review recommends — see the bullet above.

### 2. ✅ Frontend tests for Modules 1–2's largest untested logic — done 2026-08-28

33 tests added (`FormFieldBuilder` 20, `RequisitionFormPage` 13). Frontend total **432**
(408 internal + 24 public). All mutation-proved.

**`FormFieldBuilder` found a real bug, now fixed.** The Dropdown type could not be configured by
typing: the choices input derived its value from `options.join(', ')`, so each keystroke was
split, filtered and re-joined before the next — a typed comma was parsed into a separator and
**erased under the cursor**. Typing `Yangon, Mandalay` produced the one option `YangonMandalay`;
pasting the same string worked. The field type was silently limited to a single choice. Fixed by
holding the raw text in local state (`OptionsInput`).

**Three defects are pinned rather than fixed**, each needing a decision rather than a tidy-up:

- ⚠️ **Duplicate keys.** The key is `field_${Date.now().toString(36)}`, so two questions added in
  the same millisecond collide and the server rejects the **whole schema** ("used more than
  once") — the recruiter loses the save, not one field. Changing the key format touches keys
  already persisted in JSONB answers.
- **A freshly added question has a blank label**, which `ApplicationFormSchema.TryParse` rejects.
- **Switching a question to Dropdown leaves it with no options**, which `TryParse` also rejects.
  Both are two clicks away and both fail only on save.
- ⚠️ **`RequisitionFormPage:49` claims a behaviour it does not have.** The comment says "Bounce
  rather than let someone fill in a form the API will reject with 409", but on a non-Draft
  requisition it only sets an error string — no redirect, no disabling. The form stays live and
  submittable, and the test drives it all the way to the real 409. Whether it should redirect,
  disable the fields, or hide the form is a product decision.

Pattern to copy: `src/lib/scorecard.test.ts` for pure rules,
`src/pages/InterviewDetailPage.test.tsx` for a page with `vi.mock('../lib/api')`.
**Prove each new test fails before you believe it passes.**

### 3. Finish ADR-0025 — step 4, the documents and the marketing page

**Step 3 is done for everything that reaches a screen** (see §0 above); this entry used to say
"both frontends are still on the Clear Pipeline preset" and that has been false since 2026-08-25.

What is left is step 4 — the surfaces that *describe* the design system rather than use it, and
they now describe a system the code no longer runs. Measured 2026-08-25, counting
`primary-*` / `Bricolage` / `IBM Plex` / `#0B5654` / "Clear Pipeline":

| File | Stale references | State |
|---|---|---|
| `marketing/landing.html` | ~~65~~ | ✅ **done 2026-08-26** — on V1.0, 0 stale names |
| `DESIGN.md` | ~~47~~ | ✅ **done 2026-08-26** — rewritten from the retokened artifact |
| `RecruitOps_Design_System.md` | ~~27~~ | ✅ **done 2026-08-27** — retitled "RecruitOps V1.0" |

**✅ ADR-0025 is fully adopted. Every document and surface is on V1.0.**

`RecruitOps_Design_System.md` was the last one, and it was a **step-3** loose end wearing a
step-4 label: step 3's scope is "move the preset, the two frontends, **and
`RecruitOps_Design_System.md`**", and step 3 closed without it. For two days the product's own
design document told an engineer to reach for `primary-600` — a class that resolves nowhere.

What it now records, beyond renamed tokens: the button is `h-9 rounded-md bg-brand-700` with
hover→`800`, active→`900` (height and radius both shrank); pills are `-50` tint + `-700` text;
`overline` is gone and used nowhere; the type scale is Tailwind's own utility names where
**`text-base` is 14px, not 16**; and every contrast pair is re-measured rather than asserted.

⚠️ **`Offer` and `Interview` are now the same pill colour.** `Offer` was `accent`, `Interview` was
`warning`, and those two families had **identical hexes** under Clear Pipeline — so they always
looked the same; V1.0 merging them into `warn` just makes it visible. Telling them apart at a
glance is a **product decision needing a new colour**, not a token rename.

⚠️ **Separately, and not a design-system problem:** `marketing/landing.html` **overflows
horizontally at narrow widths**. Measured live at a 440px viewport: `scrollWidth` 602 before the
retokening, 601 after — so it predates the V1.0 work and is not a regression from it. The primary
source is the tier table's `overflow-x-auto` wrapper leaking width through its `relative`
parent up to `<main>` (~152px); a second ~9px source remains after that. It has never been caught
because the page's finish review never rendered anything below 505px. Filed as its own change.

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

### 4b. 🎨 Two nav questions the product owner raised — one built, one deferred by them
Raised 2026-08-28 alongside the sidebar-scroll bug. Neither was drawn in `design/internal/`
(confirmed by grep: no collapsed-rail variant, no `aria-expanded`/`<details>` nav anywhere).

**✅ Collapsible rail — done 2026-08-28.** Drawn into `design/internal/components.html` first
("Nav rail — collapsed state"), then built in `Sidebar.tsx`. 224px ↔ 64px, persisted in
`localStorage`, `aria-label` + `title` on every icon, group headings replaced by hairlines,
toggle fixed in the pinned footer. 10 tests. Details in `CHANGELOG.md`.

**✅ Parent/child groups — done 2026-08-28.** Headings are buttons that fold their children away.
Shut set persisted (not the open set, so groups added later arrive open); a group holding the
active route never folds; no folding at 64px, where there is no heading to fold.

⚠️ I initially recorded this as deferred, reading "ဒါက ငါနောက်ပိုင်း ထပ်တိုးလာရင်အတွက်ပါ" as
*defer it* when it was the **reason** for wanting it. Worth remembering: a sentence explaining
why someone wants a thing is not a sentence postponing it.

### 5. Smaller, whenever
- **Delete or wire up the orphaned feature folders** — `features/requisitions/`,
  `features/interviews/`, and four of the five files in `features/pipeline/`. Zero importers
  repo-wide; tests that pass while proving nothing about the shipped app. Re-measured 2026-08-25,
  and it is wider than this entry used to claim — the table in §0 lists every one.
  **Parked by the product owner on 2026-08-25**; ask before acting on it.
- **`frontend/public` has no `app/not-found.tsx`** — a candidate following a withdrawn job link
  gets Next.js's built-in 404: no layout, no fonts, no company name. `design/public/` does not
  draw this screen, so it needs designing before building.
- ~~**`frontend/public` has no tests at all**~~ ✅ **done 2026-08-27 — 24 tests.** `lib/api.test.ts`
  pins the server-vs-browser base-URL branch (the class of bug behind the Docker rewrite failure,
  including one test asserting the two **must not collapse**); `ApplicationForm.test.tsx` covers
  the contact rule, `customFieldsJson` null-vs-`{}`, every custom field type, and a **leak guard**
  asserting no server wording reaches a public page. All mutation-proved (branch collapse kills 3,
  echoing the error kills 1, removing the contact rule kills 2).
  > **The first test file found a latent bug**: `api()` merged `Content-Type` into headers and
  > then spread `...init` *after* it, replacing the whole `headers` key. Nothing passes headers
  > today so it never fired. Fixed — `headers` comes last.
  >
  > CI needed no change: the root `test` script is `--workspaces --if-present`, so a `test` script
  > in the workspace is enough. Frontend total is now **399** (375 internal + 24 public).
- ~~**`Badge` still carries `gold` / `silver` / `bronze`**~~ ✅ **removed 2026-08-28** —
  MIGRATION-PLAN **step 5** ("remove tier badge"), which had been unchecked for a month. The
  crown icon `Badge` injected on its own for `variant="gold"` went too: a badge now draws an
  icon only when the caller passes one. They were also the only hard-coded hexes left in the
  file, predating the token system entirely. The three tests were **retargeted rather than
  deleted** — two of them pinned behaviour that outlived the variants.
- ~~**`ExecutiveSummaryPanel` offers a "Client Portal" audience**~~ ✅ **fixed 2026-08-28 — and it
  was far worse than vocabulary.** The API does **not** take `audience`; it never has. Checked
  against the running service's OpenAPI document, the SPA and the API had never agreed on this
  endpoint in either direction: `audience` and `language` were discarded by model binding, and
  only `headline` matched on the response, so the panel rendered a headline over three blanks.
  `ai.test.ts` mocked the response in the frontend's shape, so it passed and proved nothing —
  the same failure the `ApprovalChainsPage` comment warns about, one module over.
  `audience` was deleted (ADR-0001 removed clients) rather than wired up; `packages/types` now
  mirrors `AiIntegrationDtos.cs`.
  > ✅ **`language` is wired end to end (2026-08-28).**
  > `GenerateExecutiveSummaryRequest.Language` reaches the prompt, Burmese is requested as
  > **Unicode explicitly** (a model asked for "Burmese" can return Zawgyi), `bilingual` puts
  > English first then Burmese in every field, and an unknown value falls back to English rather
  > than 400. The dev stub honours it too, so the selector is not dead on a machine without an
  > API key. **11 backend tests**, mutation-proved (ignoring `Language` fails 6). Confirmed in
  > the live OpenAPI schema after a container rebuild.
  > ⚠️ The stub's Burmese strings are a **developer placeholder pending native review**.
  > ⚠️ Worth a sweep: this endpoint's contract was fiction and nothing noticed. The **other AI
  > endpoints have not been checked the same way** — `matchCandidate` and `prepareDocument` have
  > mocks written in the same style, and `PrepareDocumentRequest` still lists a
  > `"ClientDossier"` document type, which is the same agency-era concept.
- Re-run Module 5's metrics against the **new** definitions once Module 4 exists; the shipped
  ones end at a different event.
- ~~Fix or delete the CI `Test counts` summary step~~ ✅ **the frontend now has one too
  (2026-08-28).** The backend's has been trustworthy since run #4; the frontend had only a green
  tick, which mattered because **`npm run test --workspaces --if-present` exits 0 when a script is
  missing** — a workspace whose tests stopped running looked exactly like one that passed. The new
  step prints a per-workspace table and **fails the job** when a workspace declaring a `test`
  script produces no vitest summary; the expected list comes from `package.json`, so it extends
  itself. Verified by extracting the script back out of `ci.yml` and running it against four logs
  (pass / failing / absent / no-log). ⚠️ `set -o pipefail` on the test step is load-bearing —
  without it `tee` masks a failing suite as green.
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
  > **Since 2026-08-27 there is one door: `IDepartmentAccess.CanReachCandidatesInAsync`.** It is
  > department scoping *and* the ADR-0018 exclusion, implemented once. Call it for anything that
  > touches a candidate, an application, a CV, a scorecard or a pipeline stage. `CanAccessAsync`
  > is the **requisition axis only** and its doc comment now says so. The three private copies
  > that used to hold this rule (`PipelineService`, `BulkResumeService`, `ApplicationAccess`) are
  > forwards now. Mutation-proved: dropping the exclusion from the shared helper fails **8**
  > `ApproverReachTests` at once, where before it would have failed only one service's.
  > ⚠️ It is still *possible* to call `CanAccessAsync` about a candidate — it stays public because
  > requisitions need it. Making that impossible needs the interface split in two
  > (`ICandidateReach`); not done, and a reasonable next step if this bug ever recurs.
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
- **You cannot override a Tailwind utility by appending another one for the same property.**
  `border-line` and `border-critical-500` have identical specificity, so the winner is decided by
  Tailwind's own output order — not by the order they appear in your class string. The login
  password field's error outline read correctly in the source and rendered the ordinary grey
  border. Build the class so the losing utility is never emitted, and check it in a browser: a
  unit test on `className` passes either way unless it also asserts the *absence* of the default.
- **The internal app's session is in `sessionStorage`, which is PER-TAB — so "I logged in" and
  "the agent can see a session" are different facts.** `auth.ts` keeps `recruitops.session` in
  `sessionStorage` deliberately (see its comment: it dies with the tab, which is the point). A
  login therefore exists *only* inside the exact tab it was typed into. Another tab on the same
  origin, in the same browser and the same profile, sees an **empty** `sessionStorage` and bounces
  to `/login` — which looks identical to "the login failed". Cost a round trip on 2026-08-27.
  If a session needs driving, the login must happen **in the tab being driven**; after that,
  navigating that tab is fine, and only *closing* it drops the session. Checking `localStorage`
  tells you nothing here — it is always empty.
- **The Browser pane does not composite, so CSS transitions never advance.** Any property under
  `transition-colors` reads frozen at its starting value in `getComputedStyle`, which looks
  exactly like a broken rule. Set `el.style.transition = 'none'`, force a reflow, then read — or
  measure a freshly created element. Two real-looking "bugs" on 2026-08-21 were this.
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
