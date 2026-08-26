# RecruitOps Design System — "Clear Pipeline"

Design system for an **in-house Recruitment Operations platform**: a company's own talent
acquisition department, not a recruitment agency.

> **Pivoted 2026-08-17.** This document described "a B2B Recruitment Agency Platform (RAaaS)"
> for three weeks after [ADR-0001](docs/decisions/ADR-0001-pivot-to-inhouse.md) deleted that
> product on 2026-07-27. It specified a client portal, Gold/Silver/Bronze client tiers, a
> client feedback bar, contract-expiry cards, and a `Sent to Client` / `Placed` status
> vocabulary — none of which exist. The components implementing them shipped from
> `packages/ui` until the same date, kept reachable only by their own tests.
>
> The lesson is worth keeping: **a design system is product truth and rots exactly like code.**
> The token file and this document had already disagreed (the preset carried no tier colours)
> and nothing caught it, because docs have no compiler.

Source of truth for tokens: **`packages/ui/tailwind-preset.js`**. Both frontends import that
preset ([ADR-0012](docs/decisions/ADR-0012-frontend-split.md)); tokens are never redefined per
app. Where this document and the preset disagree, **the preset wins and this document is the
bug**.

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

### Core palette

| Token | Hex | Usage |
|---|---|---|
| `ink-900` | `#16232B` | Primary text, headings |
| `ink-600` | `#4A5B66` | Secondary text, labels, **meta text** |
| `ink-400` | `#8A99A3` | Non-text only — icons, dividers, disabled affordances |
| `line-200` | `#E3E9EC` | Borders, dividers, table rules |
| `surface-0` | `#FFFFFF` | Cards, panels |
| `surface-50` | `#F6F9F9` | App background |
| `primary-700` | `#0B5654` | Primary hover, active nav, text on `primary-100` |
| `primary-600` | `#0E6E6B` | **Primary brand** — buttons, links, focus rings, active states |
| `primary-100` | `#DCEFEE` | Primary tint — selected rows, active tab bg, info chips |
| `accent-500` | `#F2A33C` | **Amber accent** — attention moments, threshold breach |
| `accent-100` | `#FCF0DC` | Amber tint background |
| `accent-700` | `#8A5A08` | Text on `accent-100` |

### Semantic (status) colors

Each family has a **`-600` fill** and a **`-700` text-on-tint** step. They are not
interchangeable.

| Token | Fill `-600` | Text `-700` | Tint `-100` | Meaning |
|---|---|---|---|---|
| success | `#1E8E5A` | `#146B43` | `#E2F4EA` | Approved, Hired, Live, Completed |
| warning | `#C97A0A` | `#8A5A08` | `#FCF0DC` | PendingApproval, Interview, NoShow |
| danger | `#C94430` | `#A63423` | `#FBE8E4` | Rejected |
| info | `#2E6ECF` | `#22528F` | `#E6EEFB` | Applied, Screening, Scheduled |

**Rules:**

- Saturated colors appear only in pills, badges, buttons and small indicators — never as large
  background fills in the app. (The marketing surface is allowed full-bleed fields; see
  `DESIGN.md`.)
- **Text on a `-100` tint uses the `-700` step, never `-600`.**
- **`ink-400` is not a text color.** Use `ink-600` for meta text.
- Never pure black `#000` or pure grey `#808080`.
- Amber is reserved. It means *a human should look at this* — a budget threshold breached, an
  approval waiting on you. Spending it on decoration makes the real signal invisible.

> ⚠️ **This section previously claimed every `-600` on `-100` pair was "WCAG AA guaranteed",
> and that `ink-400` meta text was pre-checked. Both were false.** Measured 2026-08-17 at pill
> size (13px/600): warning **2.97:1**, success **3.62**, danger **4.08**, info **4.23**, and
> `ink-400` on `surface-50` **2.77** — against a 4.5:1 floor. The `-700` steps exist to fix
> this, and `StatusPill`'s contrast contract is pinned by tests in
> `signatureComponents.test.tsx` rather than by this paragraph.

---

## 3. Typography

| Role | Font | Fallback | Notes |
|---|---|---|---|
| Display / Headings | **Bricolage Grotesque** | Inter | Character without being loud; weights 600–700 only |
| Body / UI | **Inter** | system-ui | 400 / 500 / 600 |
| Burmese content | **Noto Sans Myanmar** | — | Auto-fallback in the stack; line-height 1.7 |
| Data / IDs / Mono | **IBM Plex Mono** | monospace | Requisition ids, dates, counts, permission codes |

**Font stack:** `Inter, "Noto Sans Myanmar", system-ui, sans-serif`
**Headings:** `"Bricolage Grotesque", Inter, "Noto Sans Myanmar", sans-serif`

Burmese is an **encoding** problem before it is a font problem: Zawgyi and Unicode occupy the
same code block, so Zawgyi text renders as garbage and never matches a search. Text is
normalised to Unicode at ingest ([ADR-0009](docs/decisions/ADR-0009-myanmar-script-handling.md)).
Correct storage is what makes this font stack meaningful.

### Type scale

| Token | Size / Line | Weight | Usage |
|---|---|---|---|
| `display` | 32 / 40 | 700 | Public job page hero, empty states |
| `h1` | 24 / 32 | 700 | Page titles |
| `h2` | 19 / 28 | 600 | Card titles, section heads |
| `h3` | 16 / 24 | 600 | Sub-sections, modal titles |
| `body` | 15 / 24 | 400 | Default text |
| `body-strong` | 15 / 24 | 600 | Emphasis, names |
| `small` | 13 / 20 | 400 | Meta, timestamps, helper text |
| `overline` | 11 / 16 | 600 | ALL-CAPS labels, +0.08em tracking |

Numbers that line up in columns use `font-variant-numeric: tabular-nums`.

---

## 4. Spacing, Radius, Elevation, Grid

**Spacing scale (4px base):** 4 · 8 · 12 · 16 · 24 · 32 · 48 · 64
Card padding 24. Section gap 32. Form field gap 16. Inline gap 8.

**Radius:** `r-sm` 8px (inputs, pills, chips) · `r-md` 12px (buttons, cards) · `r-lg` 16px
(modals) · `r-full` 999px (status pills, avatars).

**Elevation (sparingly):**
- `shadow-card` `0 1px 2px rgba(22,35,43,0.06)` — cards sit on their border, not a shadow
- `shadow-pop` `0 8px 24px rgba(22,35,43,0.12)` — dropdowns, modals, toasts only

**Grid:**
- Internal app: fixed left sidebar 240px + fluid content, max-width 1280, 24px gutters
- Public job page: single centred column, max-width 760px, 48px vertical rhythm

---

## 5. Core Components

### 5.1 Button
**Primary:** `primary-600` bg, white text, radius 12, height 40, weight 600. Hover `primary-700`.
**Secondary:** white bg, `line-200` border, `ink-900` text.
**Ghost:** transparent, `primary-600` text.
**Danger:** `danger-600` bg, white text — destructive confirms only.
One primary button per view. Icon-left optional, 16px.

### 5.2 Status Pill ★ SIGNATURE COMPONENT
Radius-full, tint background + `-700` text + 6px dot, height 24, padding 4×10, weight 600.

**The vocabulary is exactly the backend enums.** `StatusPillVocabulary` is the union of four
generated types and has no free-form extension point, deliberately: a label with no enum behind
it is a status the product cannot actually be in.

| Lifecycle | Values |
|---|---|
| Candidate pipeline (`PipelineStatus`) | `Sourced` (ink) · `Applied` (info) · `Screening` (info) · `Shortlisted` (primary) · `Interview` (warning) · `Offer` (accent) · `Hired` (success) · `Rejected` (danger) |
| Requisition (`RequisitionStatus`) | `Draft` (ink) · `PendingApproval` (warning) · `Approved` (success) · `Rejected` (danger) · `Cancelled` (ink) |
| Job posting (`JobStatus`) | `Draft` (ink) · `Live` (success) · `Closed` (ink) |
| Interview (`InterviewStatus`) | `Scheduled` (info) · `Completed` (success) · `Cancelled` (ink) · `NoShow` (warning) |
| Approval step (`ApprovalDecision`) | `Waiting` (ink) · `Approved` (success) · `Rejected` (danger) |

`NoShow` is warning, not danger: a candidate not turning up is a fact to record, not a failure
to flag red at a recruiter. `Hired` and `Rejected` are **terminal** — the UI must not offer a
route out of them, because reopening corrupts the analytics figures.

### 5.3 Card
White bg, `line-200` 1px border, radius 12, padding 24, `shadow-card`. Optional header row: h2
title left, action right. **No nested cards.**

### 5.4 Input & Select
Height 40, radius 8, `line-200` border, white bg. Focus: 2px `primary-600` ring. Label above
(small, 600), helper/error below. Error: `danger-600` border + message. Never
placeholder-as-label.

### 5.5 Table
Header row `overline`, `ink-600`, `surface-50` bg. Rows 48px min-height, `line-200` bottom rule
only — no vertical rules, no zebra. Hover `surface-50`. Selected `primary-100`. First column is
the entity (avatar + name, weight 600); the status pill column is right-aligned before actions.

### 5.6 Avatar
Radius-full, 24 / 32 / 40. Fallback: initials on `primary-100`, `primary-700` text.

### 5.7 Tabs
Underline only: active = `ink-900` text + 2px `primary-600` underline; inactive `ink-600`.
Height 44, gap 24. No pill/segmented tabs.

### 5.8 Toast
Bottom-right, white bg, radius 12, `shadow-pop`, left 3px status bar, auto-dismiss 4s. Message
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
Each stage is a tappable chip, count in mono; the active stage uses `primary-100`.
**`Rejected` is deliberately absent** — it is an exit from the funnel, not a stage along it, and
including it would imply candidates flow into it from `Hired`.

### 6.2 Approval Chain Rail ★
The requisition's decision history, and the clearest expression of principle 2.

- Vertical hairline connecting ordered nodes: one per approval step, in sequence.
- Node state is `Waiting` (hollow, `line-300`) / `Approved` (`primary-100` + check) /
  `Rejected` (`danger-100`).
- A step closed by a **senior skipping ahead** names both the person who acted and the person
  it was assigned to. The chain records what happened, not what the template expected.
- **Rounds stack, they do not replace.** A rejected round renders above its revision, dimmed but
  fully legible, with its rejection comment intact. Never collapse it to a count.
- A **threshold breach** (salary or headcount over band) renders as an amber `accent-100` note
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

- Sentence case everywhere. No ALL CAPS except `overline` labels.
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
- Focus ring: 2px `primary-600`, visible on every interactive element.
- Touch targets ≥ 44px on the public job page (applicants arrive on phones).
- Status never by colour alone — the pill always carries its text label.
- `prefers-reduced-motion` respected; motion limited to 150–200ms ease-out.
- Burmese text must not clip at any breakpoint; test with real mixed-script strings.

---

## 10. Do / Don't

**Do:** border-first cards · one accent moment per screen · pills for every status · mono for
ids and dates · `-700` text on tints · generous whitespace on public surfaces.

**Don't:** gradients on app surfaces · more than two font families visible at once · zebra
tables · icon-only buttons without labels · saturated colour as an app page background · new
status labels outside §5.2 · `ink-400` as a text colour · a padlock where a `404` belongs.
