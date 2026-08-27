# RecruitOps Design System — "RecruitOps V1.0"

Design system for an **in-house Recruitment Operations platform**: a company's own talent
acquisition department, not a recruitment agency.

> **Retokened 2026-08-27 to V1.0 ([ADR-0025](docs/decisions/ADR-0025-token-system-v1-proposal.md)).**
> This document described the **"Clear Pipeline"** token system — `#16232B` ink, `#0E6E6B` teal,
> Bricolage Grotesque, IBM Plex Mono, `primary-*` / `surface-*` / `success|warning|danger|accent`.
> The code moved to V1.0 in **step 3** on 2026-08-25. This file was named in step 3's scope and
> **step 3 closed without it**, so for two days the product's own design document told an engineer
> to reach for `primary-600` — a class name that no longer resolves anywhere in the repo.
>
> Its structure, vocabulary and signature patterns all survive: Approval Chain Rail, Blind Panel
> Scorecard, department-scope `404`, the status vocabulary tied to the four backend enums. Only
> the **colour, type, radius and elevation tokens** are superseded.

> **Pivoted 2026-08-17.** This document described "a B2B Recruitment Agency Platform (RAaaS)"
> for three weeks after [ADR-0001](docs/decisions/ADR-0001-pivot-to-inhouse.md) deleted that
> product on 2026-07-27. It specified a client portal, Gold/Silver/Bronze client tiers, a
> client feedback bar, contract-expiry cards, and a `Sent to Client` / `Placed` status
> vocabulary — none of which exist. The components implementing them shipped from
> `packages/ui` until the same date, kept reachable only by their own tests.
>
> The lesson is worth keeping: **a design system is product truth and rots exactly like code.**
> The token file and this document had already disagreed (the preset carried no tier colours)
> and nothing caught it, because docs have no compiler. **It happened again with V1.0**, in the
> same file, ten days later.

**Source of truth for tokens: `design/internal/ds.js`** — the design kit is authored first and
`packages/ui/tailwind-preset.js` follows it. Both frontends import that preset
([ADR-0012](docs/decisions/ADR-0012-frontend-split.md)); tokens are never redefined per app.

Where this document and the kit disagree, **the kit wins and this document is the bug**. Where the
preset and the kit disagree, the kit wins and the preset is stale.

---

## 1. Brand Foundation

**Personality:** Trustworthy · Fast · Modern · Calm under pressure
**One-line thesis:** *"Every decision has a record."* The product's value is that you can always
answer who asked, who approved, on what version, and when.

**Three surfaces, one system:**

| Surface | Audience | Mood |
|---|---|---|
| **Internal app** (`frontend/internal`) | Recruiters (8 hrs/day) and hiring managers (occasional, non-expert) | Calm, low-saturation, dense but scannable |
| **Public job page** (`frontend/public`) | Job applicants, often on a phone, often from a shared link | Spacious, plain, trustworthy; no internal data ever |
| **Marketing** (`marketing/landing.html`) | CHROs and procurement evaluating the product | Same tokens, louder register. Its own record is [DESIGN.md](DESIGN.md) |

> The public job page is **for applicants**. It is not a client CV-review portal — that concept
> was deleted with the pivot. See `docs/product/overview.md` → "What this is NOT".

**Design principles:**

1. **Status is always visible.** The state of any requisition, application, posting or interview
   is readable in about a second, via the pill system rather than prose.
2. **The record is never rewritten.** A rejected approval round stays legible beside its
   revision; a cancelled requisition keeps its half-decided chain. The UI must never present a
   tidier history than actually happened.
3. **One primary action per screen.** Everything else is secondary or ghost.
4. **Spacious over clever.** Whitespace does the work. No decorative gradients, no
   glassmorphism in the app, no heavy shadows.
5. **Bilingual-safe.** Every text area renders mixed English + Burmese without clipping;
   line-height ≥ 1.6 for Burmese script.
6. **Hiring managers are not power users.** They visit a few times a month. Anything that
   assumes recruiter muscle memory has failed its second-largest audience.

---

## 2. Color Tokens

V1.0's hexes are Tailwind's own defaults, so the semantic names below are **aliases, not a custom
palette**. That is deliberate: a designer can look a value up, and nobody has to maintain a
bespoke ramp.

### Core palette

| Token | Hex | Tailwind | Usage |
|---|---|---|---|
| `ink-900` | `#0F172A` | slate-900 | Primary text, headings |
| `ink-800` | `#1E293B` | slate-800 | Inverse-surface rules |
| `ink-700` | `#334155` | slate-700 | Table body text |
| `ink-600` | `#475569` | slate-600 | Secondary text, labels, **meta text** |
| `ink-500` | `#64748B` | slate-500 | Mono counts, de-emphasised figures |
| `ink-400` | `#94A3B8` | slate-400 | Non-text only — icons, dividers, disabled affordances |
| `canvas` | `#F8FAFC` | slate-50 | App background |
| *(white)* | `#FFFFFF` | — | Cards, panels |
| `line` | `#E2E8F0` | slate-200 | Borders, dividers, table rules |
| `line-strong` | `#CBD5E1` | slate-300 | Hollow node rings, heavier strokes |
| `brand-900` | `#134E4A` | teal-900 | Button active, full-bleed field (marketing only) |
| `brand-800` | `#115E59` | teal-800 | Button hover, **text on `brand-50`** |
| `brand-700` | `#0F766E` | teal-700 | **Primary brand** — buttons, links, focus rings, active states |
| `brand-600` | `#0D9488` | teal-600 | Hover on a dark ground, where -800 would go backwards |
| `brand-200` | `#99F6E4` | teal-200 | Node rings, selected borders |
| `brand-100` | `#CCFBF1` | teal-100 | `.mention` background, selection highlight |
| `brand-50` | `#F0FDFA` | teal-50 | Brand tint — selected rows, active tab bg, brand pills |

> **There is no `brand-500`, and no `-500` step is a text colour.** `-500` steps are fills, icons
> and borders; `-700` steps are text on a `-50`/`-100` tint.

> **`accent` is gone.** Clear Pipeline carried both an `accent` family and a `warning` family for
> amber, and their hexes were **identical at every step** (`-700 #8A5A08`, `-600 #C97A0A`,
> `-100 #FCF0DC`; only `accent` had a `-500`). V1.0 has one `warn` family. That cost a name, not a
> colour — but see the amber rule below, which is now a discipline rather than a naming
> distinction, and is therefore easier to break.

### Semantic (status) colors

Each family has a **`-500` fill** (dots, icons, bars) and a **`-700` text-on-tint** step, over a
`-50` or `-100` tint. They are not interchangeable.

| Token | Fill `-500` | Text `-700` | Tint `-50` | Tint `-100` | Meaning |
|---|---|---|---|---|---|
| positive | `#10B981` | `#047857` | `#ECFDF5` | `#D1FAE5` | Approved, Hired, Live, Completed |
| warn | `#F59E0B` | `#B45309` | `#FFFBEB` | `#FEF3C7` | PendingApproval, Interview, Offer, NoShow |
| critical | `#EF4444` | `#B91C1C` | `#FEF2F2` | `#FEE2E2` | Rejected |
| info | `#3B82F6` | `#1D4ED8` | `#EFF6FF` | `#DBEAFE` | Applied, Screening, Scheduled |

**Rules:**

- Saturated colors appear only in pills, badges, buttons and small indicators — never as large
  background fills in the app. (The marketing surface is allowed full-bleed fields; see
  `DESIGN.md`.)
- **Text on a `-50`/`-100` tint uses the `-700` step, never `-500`.**
- **`ink-400` is not a text color on light grounds.** Use `ink-600` for meta text. Against
  `ink-900` it is fine — 6.96:1 — and that is where the footer and CTA copy use it.
- Never pure black `#000` or pure grey `#808080`.
- Amber is reserved. It means *a human should look at this* — a budget threshold breached, an
  approval waiting on you. Spending it on decoration makes the real signal invisible.

> ⚠️ **This section once claimed every `-600` on `-100` pair was "WCAG AA guaranteed", and that
> `ink-400` meta text was pre-checked. Both were false.** Measured 2026-08-17 at pill size
> (13px/600): warning **2.97:1**, success **3.62**, danger **4.08**, info **4.23**, and `ink-400`
> on the page ground **2.77** — against a 4.5:1 floor.

**Re-measured on the V1.0 steps, 2026-08-27** — computed, not asserted:

| Pair | Ratio |
|---|---|
| `warn-700` on `warn-50` | **4.84** |
| `positive-700` on `positive-50` | **5.21** |
| `critical-700` on `critical-50` | **5.91** |
| `info-700` on `info-50` | **6.16** |
| `brand-800` on `brand-50` | **7.27** |
| `brand-700` on `brand-100` (`.mention`) | **4.86** |
| white on `brand-700` (primary button) | **5.47** |
| `ink-600` on `canvas` (body) | **7.24** |
| `ink-400` on `canvas` — **still not a text colour** | 2.45 |

`StatusPill`'s contrast contract is pinned by tests in `signatureComponents.test.tsx` rather than
by this table. Verify, do not assert.

---

## 3. Typography

| Role | Font | Fallback | Notes |
|---|---|---|---|
| Headings **and** body / UI | **Inter** | system-ui | 400 / 500 / 600 / 700 |
| Burmese content | **Noto Sans Myanmar** | — | Auto-fallback in the stack; line-height 1.7 |
| Data / IDs / Mono | **JetBrains Mono** | monospace | Requisition ids, dates, counts, permission codes |

**Font stack:** `Inter, "Noto Sans Myanmar", system-ui, sans-serif`
**Headings:** the same stack. There is no separate heading font.

> **V1.0 has no display face.** Clear Pipeline set headings in Bricolage Grotesque; V1.0 drops it
> on the reasoning that *a display font in a UI label is a product-slop tell*. One family carries
> headings, labels, data and body, separated by weight and size rather than by typeface.
>
> `font-display` used to survive in the preset as a compat alias resolving to the same Inter
> stack. Measured 2026-08-27 it had **0 usages** across `frontend/internal`, `frontend/public` and
> `packages/ui`, and it was **deleted the same day** — so `font-display` now emits no CSS at all,
> which is the right outcome for a class naming a typeface this system does not have. (The
> preset's comment had claimed 167 usages and was two migrations stale.)
>
> The marketing surface is the one place the missing display face is a real loss, at 56px poster
> scale. It compensates with `-0.04em` tracking; see [DESIGN.md](DESIGN.md).

Burmese is an **encoding** problem before it is a font problem: Zawgyi and Unicode occupy the
same code block, so Zawgyi text renders as garbage and never matches a search. Text is
normalised to Unicode at ingest ([ADR-0009](docs/decisions/ADR-0009-myanmar-script-handling.md)).
Correct storage is what makes this font stack meaningful.

### Type scale

A fixed rem scale at roughly a 1.15 ratio, **named for Tailwind's own size utilities**, so
`text-sm` and friends are what you write. Deliberately not fluid: users sit at a consistent DPI,
and a heading that shrinks inside a panel looks broken.

| Utility | Size / Line | Usage |
|---|---|---|
| `text-3xl` | 24 / 32 | Page titles — the app's largest type |
| `text-2xl` | 20 / 28 | Section heads |
| `text-xl` | 18 / 26 | Card titles |
| `text-lg` | 16 / 24 | Sub-sections, modal titles |
| `text-md` | 15 / 22 | Emphasis, names |
| `text-base` | 14 / 20 | **Default text** |
| `text-sm` | 13 / 20 | Meta, timestamps, helper text |
| `text-xs` | 12 / 16 | Pills, chips, dense labels |
| `text-2xs` | 11 / 16 | Micro labels |

> ⚠️ **This REPLACES Tailwind's defaults for these names — `text-base` is 14px here, not 16.**
> That is the product's density, and it is why the kit's screens read as an operations tool
> rather than a marketing page. The app's ramp **tops out at 24px**; anything larger belongs to
> the marketing surface.

> **`overline` is gone.** It was an 11px ALL-CAPS token with +0.08em tracking. V1.0 has no
> ALL-CAPS role, matching the sentence-case rule in §8, and measured 2026-08-27 the string
> `overline` appears **nowhere** in `packages/ui`, `frontend/internal` or `frontend/public`.
> Table headers use `text-xs` in `ink-600`, sentence case.

Numbers that line up in columns use `font-variant-numeric: tabular-nums` (`.tnum`).

---

## 4. Spacing, Radius, Elevation, Grid

**Spacing scale (4px base):** 4 · 8 · 12 · 16 · 24 · 32 · 48 · 64
Card padding 24. Section gap 32. Form field gap 16. Inline gap 8.

**Radius:** `rounded-sm` 6px · `rounded` 8px (inputs, chips) · `rounded-md` 10px (buttons) ·
`rounded-lg` 12px (cards) · `rounded-xl` 16px (modals, poster containers) · `rounded-2xl` 20px.

> The whole ramp was re-cut by V1.0 — `md` 12→10, `lg` 16→12, `xl` 20→16 — so every surface is
> slightly tighter than under Clear Pipeline. `rounded-full` is a **compat entry** at 999px: V1.0
> has no `full` step, but 38 usages (status pills, avatars, rail nodes) still depend on it.
> Tailwind's own default is 9999px, which is visually identical at these sizes.

**Elevation (sparingly):** three tiers, and the app uses the first two.
- `shadow-sm` `0 1px 2px 0 rgba(15,23,42,.05)` — the lightest contact seat
- `shadow-card` `0 1px 3px 0 rgba(15,23,42,.07), 0 1px 2px -1px rgba(15,23,42,.05)` — cards sit
  on their border, not a shadow
- `shadow-overlay` `0 10px 30px -8px rgba(15,23,42,.20), 0 4px 10px -4px rgba(15,23,42,.10)` —
  dropdowns, modals, toasts only

> `shadow-pop` was a compat alias pointing at `overlay`. Measured 2026-08-27 it had **0 usages**
> and was **deleted the same day**, alongside `font-display`. See §11.

**Dark mode:** there isn't one, and `darkMode: 'class'` in the preset is what makes that true
rather than aspirational. Tailwind's default is `'media'`, under which a stray `dark:` utility
fires on any dark-set OS with no opt-in — `features/analytics` once carried 97 of them and painted
half a page in the wrong theme, measured at **2.45:1**, invisible to anyone developing in light
mode. With `'class'` the next stray one is inert until someone deliberately puts `.dark` on an
ancestor. When a real dark theme is designed, that is where it turns on.

**Grid:**
- Internal app: fixed left sidebar 240px + fluid content, max-width 1280, 24px gutters
- Public job page: single centred column, max-width 760px, 48px vertical rhythm

---

## 5. Core Components

### 5.1 Button
**Primary:** `bg-brand-700`, white text, `rounded-md` (10px), height 36 (`h-9`), `text-base`
weight 500. **Hover `brand-800`, active `brand-900`** — it darkens on both, in that order.
**Secondary:** white bg, `line` border, `ink-900` text.
**Ghost:** transparent, `brand-700` text.
**Danger:** `critical-500` bg, white text — destructive confirms only.
**Focus:** `ring-2 ring-brand-700 ring-offset-2`, never a background change alone.
One primary button per view. Icon-left optional, 16px.

> Height and radius both shrank with V1.0: 40→36 and 12→10. The exact string the kit ships is
> `h-9 px-3.5 rounded-md bg-brand-700 hover:bg-brand-800 active:bg-brand-900 text-white
> text-base font-medium transition-colors`. On a **dark** ground the hover goes *lighter*
> (`brand-600`) instead, because darkening sinks the button into the background.

### 5.2 Status Pill ★ SIGNATURE COMPONENT
Radius-full, **`-50` tint background + `-700` text**, height 24, padding 4×10, `text-xs`
weight 500. The neutral state is `bg-canvas border border-line text-ink-600` — a bordered chip
rather than a tinted one, so "no colour" still reads as a deliberate state.

**The vocabulary is exactly the backend enums.** `StatusPillVocabulary` is the union of four
generated types and has no free-form extension point, deliberately: a label with no enum behind
it is a status the product cannot actually be in.

| Lifecycle | Values |
|---|---|
| Candidate pipeline (`PipelineStatus`) | `Sourced` (neutral) · `Applied` (info) · `Screening` (info) · `Shortlisted` (brand) · `Interview` (warn) · `Offer` (warn) · `Hired` (positive) · `Rejected` (critical) |
| Requisition (`RequisitionStatus`) | `Draft` (neutral) · `PendingApproval` (warn) · `Approved` (positive) · `Rejected` (critical) · `Cancelled` (neutral) |
| Job posting (`JobStatus`) | `Draft` (neutral) · `Live` (positive) · `Closed` (neutral) |
| Interview (`InterviewStatus`) | `Scheduled` (info) · `Completed` (positive) · `Cancelled` (neutral) · `NoShow` (warn) |
| Approval step (`ApprovalDecision`) | `Waiting` (neutral) · `Approved` (positive) · `Rejected` (critical) |

`NoShow` is `warn`, not `critical`: a candidate not turning up is a fact to record, not a failure
to flag red at a recruiter. `Hired` and `Rejected` are **terminal** — the UI must not offer a
route out of them, because reopening corrupts the analytics figures.

> ⚠️ **`Offer` and `Interview` are now the same colour**, and they were not before. `Offer` was
> `accent` and `Interview` was `warning` under Clear Pipeline — two names whose hexes were already
> identical, so the pills looked the same then too. V1.0 merging them into `warn` simply makes
> that visible in the source. If these two ever need to be told apart at a glance, that is a
> **product decision requiring a new colour**, not a token rename.

> An unknown status falls back to neutral rather than throwing — it is a label the backend sent
> that this build does not know yet, and showing it plainly beats a blank space.

### 5.3 Card
White bg, `line` 1px border, radius 12, padding 24, `shadow-card`. Optional header row: h2
title left, action right. **No nested cards.**

### 5.4 Input & Select
Height 40, radius 8, `line` border, white bg. Focus: 2px `brand-700` ring. Label above
(small, 600), helper/error below. Error: `critical-500` border + message. Never
placeholder-as-label.

### 5.5 Table
Header row `text-xs`, `ink-600`, `canvas` bg. Rows 48px min-height, `line` bottom rule
only — no vertical rules, no zebra. Hover `canvas`. Selected `brand-50`. First column is
the entity (avatar + name, weight 600); the status pill column is right-aligned before actions.

### 5.6 Avatar
Radius-full, 24 / 32 / 40. Fallback: initials on `brand-50`, `brand-800` text.

### 5.7 Tabs
Underline only: active = `ink-900` text + 2px `brand-700` underline; inactive `ink-600`.
Height 44, gap 24. No pill/segmented tabs.

### 5.8 Toast
Bottom-right, white bg, radius 12, `shadow-overlay`, left 3px status bar, auto-dismiss 4s. Message
is past tense: "Requisition submitted." "Scorecard submitted."

### 5.9 Empty State
Centred in card: line icon (48, `ink-400` — an icon, not text), h3 title, one line of small
text, one primary button. Copy invites: "No requisitions yet — raise one to get started."

### 5.10 Dialog / Sheet
Modal only where the task needs protected focus. A sheet is preferred for anything the user
should be able to abandon.

---

## 6. Signature Patterns

### 6.1 Pipeline Stage Rail
Horizontal row of stage counts at the top of a posting or pipeline view:
`Sourced 24 → Applied 18 → Screening 12 → Shortlisted 8 → Interview 4 → Offer 2 → Hired 1`
Each stage is a tappable chip, count in mono; the active stage uses `brand-50`.
**`Rejected` is deliberately absent** — it is an exit from the funnel, not a stage along it, and
including it would imply candidates flow into it from `Hired`.

### 6.2 Approval Chain Rail ★
The requisition's decision history, and the clearest expression of principle 2.

- Vertical hairline connecting ordered nodes: one per approval step, in sequence.
- Node state is `Waiting` (hollow, `line-strong`) / `Approved` (`brand-50` + check) /
  `Rejected` (`critical-50`).
- A step closed by a **senior skipping ahead** names both the person who acted and the person
  it was assigned to. The chain records what happened, not what the template expected.
- **Rounds stack, they do not replace.** A rejected round renders above its revision, dimmed but
  fully legible, with its rejection comment intact. Never collapse it to a count.
- A **threshold breach** (salary or headcount over band) renders as an amber `warn-50` note
  attached to the step that triggered it, naming the rule that extended the chain.
- Cancellation leaves remaining steps `Waiting`. Do not backfill them.

### 6.3 Blind Panel Scorecard ★
Interview evaluation, where the interface must enforce nothing and reveal nothing.

- Before the viewer submits: their own scorecard is editable; **other panel members' scores are
  not rendered at all** — the server does not send them. Show a count of what is withheld
  ("2 evaluations hidden"), never a blurred placeholder implying the value is present.
- `hiddenCount === 0` says nobody has submitted yet. "0 evaluations are waiting for yours" is
  not a sentence.
- After submitting: the full panel appears, disagreements included. Submitting is
  **irreversible** and the button must say so before the click, not after.
- A non-participant sees an ordinary read-only state with **no form** — offering one would be a
  button that can only fail.

### 6.4 Bilingual Text
- Any field that can hold a candidate's own words gets the Myanmar fallback and 1.7 line-height.
- Where a Burmese and an English rendering of the same content are shown together, they are
  **peers side by side**, not primary-and-translation.
- Search inputs accept Burmese without word spacing; never imply word-boundary tokenisation.

### 6.5 Department Scope
A record outside the viewer's departments is **absent, not locked**. There is no padlock state
and no "request access" affordance, because the API returns `404` rather than `403` precisely so
that existence is not disclosed. The UI must not reintroduce the leak the API closes.

---

## 7. Screen Patterns

**Recruiter dashboard:** greeting + date → stat cards (open requisitions, awaiting my approval,
interviews this week, offers out) → Pipeline Stage Rail per active posting → recent activity.

**Hiring manager view:** their departments only. "Raise a requisition" is the primary action.
Their in-flight requisitions show the Approval Chain Rail with whose desk it is on right now.

**Approver inbox:** only requisitions whose lowest-sequence `Waiting` step belongs to the viewer
and whose status is still `PendingApproval`. Ordered by wait time.

**Requisition detail:** header (id, title, department, status pill) → job description →
Approval Chain Rail, rounds newest last → actions (submit / approve / reject / cancel / revise)
gated on permission, never on role literal.

**Interview detail:** panel roster → the viewer's own scorecard → the panel's evaluations under
the blind rule → the round's notes with @mentions.

**Public job page:** company mark + job title (display) → summary → the customer-defined
application form → submit. No nav, no internal fields, and **salary only if explicitly opted
in**.

---

## 8. UX Writing

- Sentence case everywhere. No ALL CAPS except `text-xs` labels.
- Buttons say the outcome: "Submit requisition", "Approve step", "Publish posting" — never
  "Submit"/"OK".
- Same word through a flow: button "Submit requisition" → toast "Requisition submitted" → pill
  `Pending Approval`.
- Errors state the fix, not the fault: "This phone number already belongs to Aung Ko — open that
  candidate or merge the two."
- Never invent a status label outside the tables in §5.2.
- Numbers in mono inside tables and timelines.

---

## 9. Accessibility & Quality Floor

- All text ≥ 4.5:1; large text ≥ 3:1. **Verify, do not assert** — this document claimed
  pre-checked pairs for a year and was wrong about five of them.
- Focus ring: 2px `brand-700`, visible on every interactive element.
- Touch targets ≥ 44px on the public job page (applicants arrive on phones).
- Status never by colour alone — the pill always carries its text label.
- `prefers-reduced-motion` respected; motion limited to 150–250ms (`transitionDuration` defaults
  to 160ms, `slow` 220ms). Users are mid-task; nobody wants to watch choreography.
- Burmese text must not clip at any breakpoint; test with real mixed-script strings.
- **No `dark:` utilities.** V1.0 has no dark theme; `darkMode: 'class'` makes a stray one inert
  rather than firing on every dark-set OS. See §4.

---

## 10. Do / Don't

**Do:** border-first cards · one amber moment per screen · pills for every status · mono for
ids and dates · `-700` text on tints · generous whitespace on public surfaces.

**Don't:** gradients on app surfaces · a second font family (there is only Inter and the mono) ·
zebra tables · icon-only buttons without labels · saturated colour as an app page background ·
new status labels outside §5.2 · `ink-400` as a text colour on a light ground · a `-500` step as
text on a tint · a `dark:` utility · a padlock where a `404` belongs.

---

## 11. Migration state (as of 2026-08-27)

V1.0 is live in both frontends and on the marketing page. What remains is a **compatibility block
in `packages/ui/tailwind-preset.js`** that maps the old names onto the new values, so nothing
renders unstyled while the last screens move.

Measured 2026-08-27 across `frontend/internal/src`, `frontend/public/app` and `packages/ui/src`:

| Alias | Live usages | State |
|---|---|---|
| `font-display` | **0** | ✅ **deleted 2026-08-27** |
| `shadow-pop` | **0** | ✅ **deleted 2026-08-27** |
| `rounded-full` | **38** | Still needed (pills, avatars, rail nodes). |
| `primary-*` / `surface-*` / `success` / `warning` / `danger` / `accent` | **38** | All in the two **parked** orphan folders. |

> The two deletions were proved inert rather than assumed: the internal app's built stylesheet
> hashes to **`index-DJXXbcKM.css` both before and after**, i.e. the CSS output is byte-identical.
> A compat alias nothing references emits nothing. `font-display` and `shadow-pop` now correctly
> produce no CSS at all — the right outcome for class names that reference a typeface and an
> elevation tier this system does not have.

The 38 old-name usages are entirely inside `features/interviews/BlindScorecardDrawer.tsx` (24) and
`features/requisitions/` (14) — both orphaned trees with no importers, **parked by the product
owner on 2026-08-25** pending a decision to migrate, delete or wire them up. The compat block
cannot be removed until that decision is made.

> ⚠️ **The exit-condition grep in the preset over-counts and will never reach zero as written.**
> It reports **43**, not 38: five of its hits are *comment prose* in `features/analytics/`
> recording the old chart palette that failed the CVD validator (`teal-500`, `emerald-500`,
> `zinc-100` inside `//` lines). Analytics itself is fully migrated. Whoever finally deletes the
> compat block should exclude comments, or check the count per file rather than in aggregate.
