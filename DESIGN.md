---
name: RecruitOps — Marketing Register
description: The marketing register of RecruitOps V1.0 — the same audit-grade tokens as the product, spoken at poster scale.
colors:
  ink-900: "#0F172A"
  ink-800: "#1E293B"
  ink-700: "#334155"
  ink-600: "#475569"
  ink-500: "#64748B"
  ink-400: "#94A3B8"
  canvas: "#F8FAFC"
  line: "#E2E8F0"
  line-strong: "#CBD5E1"
  brand-900: "#134E4A"
  brand-800: "#115E59"
  brand-700: "#0F766E"
  brand-600: "#0D9488"
  brand-200: "#99F6E4"
  brand-100: "#CCFBF1"
  brand-50: "#F0FDFA"
  positive-700: "#047857"
  positive-500: "#10B981"
  positive-100: "#D1FAE5"
  positive-50: "#ECFDF5"
  warn-700: "#B45309"
  warn-500: "#F59E0B"
  warn-100: "#FEF3C7"
  warn-50: "#FFFBEB"
  critical-700: "#B91C1C"
  critical-500: "#EF4444"
  critical-100: "#FEE2E2"
  critical-50: "#FEF2F2"
  info-700: "#1D4ED8"
  info-500: "#3B82F6"
  info-100: "#DBEAFE"
  info-50: "#EFF6FF"
typography:
  display:
    fontFamily: "Inter, Noto Sans Myanmar, system-ui, sans-serif"
    fontSize: "56px"
    fontWeight: 700
    lineHeight: 1.05
    letterSpacing: "-0.04em"
  headline:
    fontFamily: "Inter, Noto Sans Myanmar, system-ui, sans-serif"
    fontSize: "40px"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "-0.04em"
  title:
    fontFamily: "Inter, Noto Sans Myanmar, system-ui, sans-serif"
    fontSize: "22px"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "-0.02em"
  lead:
    fontFamily: "Inter, Noto Sans Myanmar, system-ui, sans-serif"
    fontSize: "17px"
    fontWeight: 400
    lineHeight: 1.65
  body:
    fontFamily: "Inter, Noto Sans Myanmar, system-ui, sans-serif"
    fontSize: "15px"
    fontWeight: 400
    lineHeight: 1.65
  small:
    fontFamily: "Inter, Noto Sans Myanmar, system-ui, sans-serif"
    fontSize: "13px"
    fontWeight: 400
    lineHeight: 1.6
  data:
    fontFamily: "JetBrains Mono, ui-monospace, SFMono-Regular, monospace"
    fontSize: "12px"
    fontWeight: 500
    lineHeight: 1.5
    fontFeature: "tabular-nums"
  burmese:
    fontFamily: "Noto Sans Myanmar, Inter, sans-serif"
    fontSize: "13px"
    fontWeight: 400
    lineHeight: 1.75
rounded:
  sm: "6px"
  DEFAULT: "8px"
  md: "10px"
  lg: "12px"
  xl: "16px"
  2xl: "20px"
  full: "9999px"
spacing:
  hairline: "1px"
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  2xl: "32px"
  section: "80px"
  section-wide: "112px"
components:
  button-primary:
    backgroundColor: "{colors.brand-700}"
    textColor: "{colors.white}"
    rounded: "{rounded.md}"
    padding: "0 24px"
    height: "48px"
    typography: "{typography.body}"
  button-primary-hover:
    backgroundColor: "{colors.brand-800}"
    textColor: "{colors.white}"
  button-secondary:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink-900}"
    rounded: "{rounded.md}"
    padding: "0 24px"
    height: "48px"
    typography: "{typography.body}"
  button-inverse:
    backgroundColor: "{colors.white}"
    textColor: "{colors.brand-900}"
    rounded: "{rounded.md}"
    padding: "0 24px"
    height: "48px"
    typography: "{typography.body}"
  card:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink-600}"
    rounded: "{rounded.xl}"
    padding: "32px"
  pill-status:
    backgroundColor: "{colors.warn-50}"
    textColor: "{colors.warn-700}"
    rounded: "{rounded.full}"
    padding: "0 10px"
    height: "24px"
    typography: "{typography.data}"
  chip-outline:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink-600}"
    rounded: "{rounded.full}"
    padding: "0 12px"
    height: "28px"
    typography: "{typography.small}"
  rail-node-done:
    backgroundColor: "{colors.brand-50}"
    textColor: "{colors.brand-800}"
    rounded: "{rounded.full}"
    size: "31px"
  rail-node-waiting:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink-400}"
    rounded: "{rounded.full}"
    size: "31px"
---

# Design System: RecruitOps — Marketing Register

## Overview

**Creative North Star: "The Record, Enlarged"**

> ⚠️ **Retokened 2026-08-26 (ADR-0025 step 4).** This document was written against
> **"Clear Pipeline"**, and the landing page it describes has since moved to **V1.0**. The
> page's structure, copy, composition and motion are unchanged — the finish review that
> approved them still stands, and every layout, grid and behaviour rule below is as verified as
> it was. What changed is the token vocabulary, so **colour, type and radius values here were
> rewritten from the retokened artifact**, not from intent. Two changes are substantive rather
> than renames: V1.0 has **no display face**, so Bricolage Grotesque is gone and Inter carries
> the hero; and `accent` **merged into** `warn`, which cost a name but not a colour — the two
> families were already the same hexes. One rule below genuinely died with the move and is
> marked where it stood: the marketing radius extension.

This is not a new world. It is the marketing register of **RecruitOps V1.0** (ADR-0025) — the
shipped design system whose direction of truth is `design/internal/ds.js`, mirrored into
`packages/ui/tailwind-preset.js` and consumed by `frontend/internal` and `frontend/public`
(ADR-0012). The landing page was built by inheriting that system rather than forking a
marketing identity, and every colour, family and radius below traces back to the kit. When the
kit changes, this file is downstream of it, not beside it — which is exactly what happened on
2026-08-26.

The register is louder, not different. The product's own claim — *every decision has a record* —
is argued visually by enlarging the product's own artifacts: an approval chain, a status pill, a
mono requisition id, a hairline rail. Nothing decorative is introduced to sell it. The page's
argument structure is one artifact replaced per section, and its materials are ink on a cool
paper ground, teal carrying every act of authority, and amber held back for the single moment a
human is being asked to look. Surfaces are border-first: a card sits on a 1px `line` rule,
not on a shadow. Figures are always monospaced and tabular, because a number you cannot line up
is a number you cannot audit.

**Three** things the marketing surface is licensed to do that the app is not, and they are the
whole extension: **full-bleed saturated colour fields** (the deep-teal sovereignty section, the
`ink-900` closing call — the app forbids saturated page backgrounds); **a frosted sticky
navigation** (the app's system says no glassmorphism — here it is a specific behaviour over
scrolling content, not ornament); and **a display scale reaching 56px**, where the app's ramp
tops out at 24px. Outside those three, the app's rules bind here unchanged.

> **This list was four until 2026-08-26.** The fourth was *page-scale radii* — 20px poster
> containers against the app's 16px ceiling. V1.0 re-cut the whole radius ramp (`xl` 20→16,
> `lg` 16→12, `md` 12→10) and added a `2xl` at 20px that this page does not use, so poster
> containers now sit at `rounded-xl` **16px** — the same value the app's largest container
> uses. The marketing radius extension no longer exists. Do not reintroduce it to recover the
> old silhouette; the page was re-reviewed at 16px and reads correctly.

**Key Characteristics:**
- Border-first surfaces; hairline rules do the structural work that shadows do elsewhere
- Asymmetric 12-column splits (5/7, 7/5, 6/6, 4/8) — never a row of identical tiles
- Every figure in JetBrains Mono with tabular numerals
- Teal for authority, amber for attention, and nothing else saturated
- One authored motion moment on the whole page; no scroll-reveal anywhere
- Bilingual by construction: Burmese is a peer rendering, never a translation footnote

## Colors

A cool, low-saturation ground carrying exactly two voices: a deep teal that means authority and
an amber that means look here.

### Primary
- **Governance Teal** (`brand-700`, `#0F766E`): every act of authority — primary buttons, links
  on hover, focus rings, the caret, the completed-node check, the logo mark. It is the only
  colour allowed to fill a button on a light ground. Measured 5.47:1 with white on it, 5.47:1
  as text on white and 5.23:1 as text on `canvas`.
- **Deep Pipeline** (`brand-900` / `brand-800`): the full-bleed sovereignty field and its
  interior rules. `brand-900` is the page background; `brand-800` is its border and node fill;
  `brand-700` is the dark rail's connector and the light-ground hover target.
- **Teal Tints** (`brand-200` / `brand-100` / `brand-50`): completed rail nodes, the highlighted
  Enterprise column, the "You" row in the panel widget, selection highlight. Text on `brand-50`
  is `brand-800` (7.27:1) — the kit's own pill pattern.
- **Signal Teal** (`brand-600`): reserved for hover on the inverse-ground CTA, where `brand-800`
  would go backwards against a dark field. This is the one hover on the page that goes *lighter*.

### Secondary
- **Threshold Amber** (`warn-500`, tint `warn-50`, text `warn-700`): attention and threshold
  breach only. It appears as a fill in exactly one place on the page — the third node of the
  logo mark — and as a tinted note in exactly one place: the hero's salary-band breach.

### Neutral
- **Ink** (`ink-900` through `ink-600`): `ink-900` is body text, headings, and the closing
  section's full-bleed ground; `ink-800` and `ink-700` are its interior rules and secondary body;
  `ink-600` carries every piece of meta text, caption and secondary paragraph on light grounds.
- **Fog** (`ink-400`): non-text on light grounds — the waiting-node dot and the scrollbar hover
  thumb — but a legitimate text colour against `ink-900`, where it measures 6.96:1 as inverse
  footer and CTA body.
- **Rule** (`line` / `line-strong`): `line` is every border, divider, table rule and the drawn
  spreadsheet grid; `line-strong` is the heavier hollow-node and index-circle stroke. Note the
  names carry no numeric step — `line` and `line-strong` do not exist in V1.0.
- **Paper** (`canvas`) and **Panel** (white): the page ground and the card ground. A section
  alternates between them to mark a change of subject without introducing a third value.

### Tertiary
- **Status families** (`positive` / `warn` / `critical` / `info`): used only inside pills, chips
  and small indicators, exactly as in the app. Each family is a `-500` fill, a `-700`
  text-on-tint, and `-50`/`-100` tints. These are the same four families `StatusPill` ships, so
  a pill drawn here and a pill drawn in the product are the same object.

### Named Rules

**The Seven-Hundred Rule.** Text on a `-50`/`-100` tint uses the `-700` step of *its own family*,
never `-500`. This is measured, not assumed. It was first forced on 2026-08-17, when the old
`-600`-on-`-100` pairs were found at 2.97 (warning), 3.62 (success), 4.08 (danger) and 4.23
(info) against a 4.5:1 floor. It survives V1.0 unchanged, and was re-measured on the new steps
on 2026-08-26: **warn 4.84, positive 5.21, critical 5.91, info 6.16** on their `-50` fills. Do
not reintroduce the retired rule.

**The Reserved Amber Rule.** `warn-500` means *a human should look at this* — a breached
threshold, an approval waiting on someone. It is spent on one element per page. If a second
amber appears, the first one has stopped meaning anything.
> Clear Pipeline expressed this with two names, `accent` and `warning`, whose hexes were
> **already identical**. V1.0 has one `warn` family, so the reservation is now a discipline
> rather than a naming distinction. That makes it easier to break, not harder — which is why
> the rule is stated here rather than assumed from the palette.

**The Not-A-Text-Colour Rule.** `ink-400` is not a text colour on light grounds (2.45:1 on
`canvas`, slightly worse than the 2.77:1 it measured under Clear Pipeline). Meta text on light
is `ink-600`. Against `ink-900` it is a different question and a legitimate one: 6.96:1.

**The Two-Field Rule.** Full-bleed saturated colour is a marketing privilege and it is spent
twice: once on the deep-teal sovereignty section, once on the `ink-900` close. A third field
would turn a structural beat into wallpaper, and none of it is permitted on app surfaces.
> V1.0's `brand-900` (`#134E4A`) is markedly lighter than Clear Pipeline's `#052A29`, so the
> sovereignty field reads as a saturated teal rather than a near-black one. Body text on it
> moved from 10.77:1 to **7.52:1** — still comfortably over the floor, and the section still
> carries. Recorded because it is the largest single visual change of the retokening.

## Typography

**One Font:** Inter (with Noto Sans Myanmar, system-ui) — headings, body and labels alike
**Label/Mono Font:** JetBrains Mono (with ui-monospace, SFMono-Regular)
**Burmese:** Noto Sans Myanmar, reached through the stack and forced by the `.mm` class

**Character:** V1.0 has **no display face** — one family carries everything, on the reasoning
that a display font in a UI label is a product-slop tell. So the hero is Inter at 700 pulled to
-0.04em, which reads as deliberate rather than defaulted only *because* of the tracking; at
default tracking a 56px Inter headline looks like an unstyled document. Inter then does all the
arguing underneath at a generous 1.65–1.7 line-height, which is a Burmese requirement before it
is a taste. JetBrains Mono is not decoration: it marks the values that belong to the record.

> **Recorded honestly:** Clear Pipeline set headlines in Bricolage Grotesque, which had enough
> irregularity to feel authored. Dropping it costs this page real character, and that cost was
> accepted in ADR-0025 rather than discovered here — the alternative was a third font stack
> living only on the marketing surface, which is the fork this whole system exists to prevent.
> The `letterSpacing` extension in the page's own config is what carries the hero without it,
> and it is the one token this file adds that `ds.js` does not have.

### Hierarchy
- **Display** (700, 40px → 52px → 56px across sm/lg, 1.05, -0.04em): the hero claim only. One
  per page.
- **Headline** (700, 32px → 40px, 1.1, -0.04em): section openers. The closing CTA runs 42px at
  1.08 as the page's only headline variant.
- **Title** (600, 22px, -0.02em; 24–26px on the inverse field, 16–19px for card and rail
  sub-heads): card titles, rail node names, statistic values.
- **Lead** (400, 17px, 1.65, `ink-600`): the paragraph directly under a headline. Constrained to
  68ch.
- **Body** (400, 15px, 1.65, `ink-600`): card and FAQ prose.
- **Small** (400, 13px, 1.6, `ink-600`): meta, captions, provenance lines, helper text.
- **Data** (500, 12–14px, tabular): requisition ids, timestamps, counts, percentages, permission
  codes, filenames, status vocabulary rendered as literal enum text.
- **Micro-label** (600, 11–13px, sentence case, `ink-600`): the one-word label above a paired
  block. Sentence case, never uppercase, never a decorative eyebrow above a headline.

### Named Rules

**The Measure Rule.** Body copy is capped at 68ch (`.measure`). The dominant claim on a
full-bleed field runs shorter at 54ch, and rail body at 62ch, because a wide measure on a
saturated ground is unreadable before it is ugly.

**The Every-Figure-Is-Mono Rule.** Any number a reader might check — a count, a percentage, a
date, a score, an id — is JetBrains Mono with `font-variant-numeric: tabular-nums`. Prose numbers
that are merely rhetorical stay in Inter.

**The Sentence-Case Rule.** Sentence case everywhere. The system carries no ALL-CAPS role on
this surface; the app's `overline` token is deliberately unused here.

**The Burmese Peer Rule.** Where English and Burmese render the same content they sit side by
side as equals with equal weight and equal container, never primary-and-translation. Burmese
text takes 1.75 line-height and must not clip at any width.

## Layout

A 12-column grid inside a 1280px container (`max-w-7xl`) with 24px gutters, held constant from
the nav through the footer. Columns are split **asymmetrically and differently each time** —
5/7 in the hero, 7/5 and 6/6 and 5/7 across the governance rows, 5/7 in the tour, 4/8 in the
FAQ, 7/5 in the close. A row of four identical tiles is the arrangement this page exists to
refuse; when a section needs several items, they get different sizes and different internal
shapes.

**Vertical rhythm.** Sections run 80px of padding, opening to 112px at `lg`. The hero runs
64/80 opening to 96/112. Section boundaries are a 1px `line` rule or a change of ground
(`canvas` ↔ white ↔ a full-bleed field) — never both at once.

**Spacing scale.** 4px base: 4 · 8 · 12 · 16 · 24 · 32 · 48 · 64 · 80 · 112. Card padding is 24px
rising to 32px at `sm`. Grid gutters between cards are 24px. Inline gaps are 8–14px.

**The hairline-gap grid.** Statistic and definition grids are built as a `line` background
showing through 1px gaps between white cells, so the divider is the gap itself. This is how the
page draws a table without table borders.

**Breakpoints.** 640 (`sm`), 768 (`md`), 1024 (`lg`), 1280 (`xl`). Column splits collapse at
`lg`; the hero's overlapping composition collapses to a stack at `md`; the language toggle and
inline CTA appear at `sm`. The tier table keeps a 720px minimum and scrolls inside its own box
below `lg`, with a gradient fade marking the cut edge and an explicit instruction above it.

**Sticky navigation** is 64px tall with `scroll-padding-top: 88px` so an anchored section never
lands under it.

## Elevation & Depth

Depth is carried by **borders and ground changes, not shadows**. Every card, panel, chip and
table sits on a 1px `line` rule against a white or `canvas` ground; a card that
needs to read as raised gets a different ground, not a bigger shadow. Overlap — the hero's
spreadsheet sitting above and behind the requisition record — does the one piece of genuine
z-work on the page.

### Shadow Vocabulary

V1.0 ships **three** tiers and this page uses two of them. Clear Pipeline's `pop` and `lift`
have no V1.0 equivalent and both collapsed into `overlay`.

- **sm** (`0 1px 2px 0 rgba(15,23,42,.05)`): the selected tour step's contact seat.
- **Card** (`0 1px 3px 0 rgba(15,23,42,.07), 0 1px 2px -1px rgba(15,23,42,.05)`): a hairline
  seat under a primary button. It is a contact shadow, not a lift.
- **Overlay** (`0 10px 30px -8px rgba(15,23,42,.20), 0 4px 10px -4px rgba(15,23,42,.10)`):
  anything that genuinely floats — the focused skip link, and the single hero artifact that the
  page's whole argument resolves into.

### Named Rules

**The One Overlay Rule.** On this page `shadow-overlay` does the job `shadow-overlay` used to: it
separates the hero record from the artifact dissolving behind it. A second element carrying it
in the page body makes neither of them the subject. (The skip link is exempt — it is a transient
overlay that is invisible until focused.)
> Clear Pipeline's `lift` was `0 24px 60px -18px` at 28% — a far deeper poster shadow than
> `overlay`. The hero record therefore sits closer to the page than it used to. Verified on
> 2026-08-26 that the overlap still reads: the record's z-order over the dissolving spreadsheet
> is carried by the mask and the stacking order, with the shadow as reinforcement rather than
> the mechanism.

**The Border-First Rule.** If a surface needs definition, give it a `line` border and a ground.
Reach for a shadow only when something genuinely floats over scrolling content.

## Shapes

Rounded, but never soft. Radii step with scale rather than with importance: 8px (`rounded`) on
small inline notes; **10px (`rounded-md`) on buttons**, inner cards and the mobile nav; 12px
(`rounded-lg`) on grouped panels and the tour step list; **16px (`rounded-xl`) on poster-scale
containers** — the hero record, the section cards, the tier table shell; and fully round on
status pills, chips, rail nodes, the language toggle and the numeric step indices.

> Every number in that sentence moved on 2026-08-26. V1.0 re-cut the ramp — `md` 12→10, `lg`
> 16→12, `xl` 20→16 — so the page is uniformly a little tighter than the build the finish
> review saw. `rounded-full` resolves to 9999px (Tailwind's default) rather than the 999px
> Clear Pipeline declared; no visible difference at these sizes. `2xl` (20px) exists in V1.0
> but this page does not use it.

Two recurring silhouettes define the page. The **bordered card with a tinted foot**: a white
body carrying the claim, and a `canvas` compartment below a `line` rule carrying the
evidence — a status flow, a widget, a bilingual pair. And the **hairline rail**: a 1px vertical
connector running between 31px round nodes, the same geometry on light and dark grounds.

Icons are drawn — Lucide line icons at 12–20px, plus a hand-built SVG logo mark. Typographic
glyphs are never used as icons: the tier table's included/not-included marks are drawn check and
minus icons with screen-reader text, not ✓ and ✗ characters.

## Components

### Buttons
- **Shape:** Gently rounded (12px), 48px tall at page scale, 40–44px inside widgets and nav.
- **Primary:** `brand-700` fill, white text, 15px/600, 24px horizontal padding, `shadow-card`.
- **Hover / Focus:** background to `brand-800` on light grounds and `brand-600` on dark
  grounds, over a 150ms colour transition. Focus is the global 2px `brand-700` ring at 3px
  offset — never a background change alone.
- **Secondary:** white fill, `line` border, `ink-900` text; border moves to `brand-200` on
  hover. Optional trailing Lucide icon in `brand-700`.
- **Inverse:** on a `brand-900` field, a white fill with `brand-900` text; its sibling is a
  bordered ghost with `ink-700` border and white text.

### Chips
- **Style:** Fully round, 28px tall, `canvas` fill with a `line` border and `ink-600`
  text for neutral facts (industries, technical labels); mono 12px when the content is a token,
  count or system value.
- **State:** Chips on this surface are static labels, not filters. A chip that carries a status
  becomes a Status Pill instead.

### Status Pill
- **Style:** Fully round, 24px tall, 4×10 padding, 12px/600, tint fill plus its own family's
  `-700` text, with an optional 6px dot or 12px Lucide icon.
- **Vocabulary:** the backend enums verbatim — `Draft`, `PendingApproval`, `Approved`,
  `Rejected`. The marketing surface never invents a status label; a label with no enum behind it
  is a state the product cannot be in.

### Cards / Containers
- **Corner Style:** 20px at section scale, 12–16px for nested compartments.
- **Background:** white body; `canvas` for the evidence foot below a `line` rule.
- **Shadow Strategy:** none by default — see Elevation & Depth. The hero record is the exception.
- **Border:** 1px `line` on light grounds, `brand-800` on the deep-teal field.
- **Internal Padding:** 24px, rising to 32px at `sm`. Never nested cards more than one deep.

### Navigation
- **Style:** Sticky, 64px, frosted — `rgba(248,250,252,.78)` under `saturate(1.6) blur(14px)` —
  with a `line` bottom rule. This is the page's only backdrop filter, justified by content
  scrolling beneath it.
- **Links:** 15px `ink-600`, moving to `brand-800` on hover, colour transition only. No
  underline, no active-state indicator.
- **Right cluster:** an EN/MM language toggle in a fully round bordered group, then the primary
  CTA. Below `lg` the links collapse into a bordered icon button opening a white panel that
  repeats every link, the toggle and the CTA at touch size (44px+).

### Governance Rail — signature component
The page's defining device, and the same one the product's design system documents as the
Approval Chain Rail. A 1px `line` connector runs from each node down to the next, suppressed
on the last; a completed node switches its connector to `brand-200`. Nodes are 31px circles:
**done** is `brand-50` with a `brand-200` border and a `brand-800` check; **waiting** is
white with a `line-strong` border and an `ink-400` dot. Each node carries a name, a role and a mono
timestamp, and a threshold breach hangs off its node as an `warn-50` note bordered in
25%-opacity `warn-500`.

A `rail-dark` variant reuses the identical geometry on the `brand-900` field with a
`brand-800` connector and `brand-800` node fills. It is used there deliberately: those items
are sequential guarantees about one record's life, and rendering them as a rail rather than four
tiles is the claim.

**A rejected round is preserved beside its revision, never over it** — dimmed to `canvas`
with its rejection comment intact. Never collapse history to a count.

### Interactive Demonstrations
Two widgets let a reader operate the product's actual rules rather than read about them: the
blind-panel scorecard (scores blurred at 7px under `[data-locked="true"]`, revealing on submit,
with the pill moving warn → positive) and the five-step workflow tour (a `role="tablist"` of
bordered steps with full arrow/Home/End keyboard support; the selected step turns white with a
`brand-200` border, `shadow-card`, and a `brand-700` index disc). Selection is signalled by
ground, border and index fill together — never by colour alone.

## Do's and Don'ts

### Do:
- **Do** take every token from `design/internal/ds.js` (mirrored in
  `packages/ui/tailwind-preset.js`). This surface is a register of **V1.0**, not a second
  identity; a colour that is not in the kit is a fork. The page's own inline config is a *copy*
  of `ds.js` and says so — when they disagree, the kit is right and the page is stale.
- **Do** put `-700` text on a `-100` tint, from the same family (The Seven-Hundred Rule).
- **Do** set every checkable figure in JetBrains Mono with `.tnum`.
- **Do** define surfaces with a 1px `line` border and a ground change (The Border-First Rule).
- **Do** split the 12-column grid asymmetrically, and differently in each section.
- **Do** cap body copy at 68ch, 54ch for a dominant claim on a saturated field.
- **Do** theme the browser's own surfaces — selection, caret, focus ring, scrollbar,
  underline-offset — to system values. Their defaults belong to no design system.
- **Do** render Burmese and English as peers, at 1.75 line-height, and check both at every width.
- **Do** draw icons (Lucide, or a hand-built SVG), and give every icon-only control a screen-reader
  label.
- **Do** keep the page to one authored motion moment: the mask dissolve of the spreadsheet into
  the record, with node stamps staggered to 2150ms — the same moment re-arranged to run downward
  below 768px, not a second effect. Honour `prefers-reduced-motion` by collapsing all of it.

### Don't:
- **Don't** spend amber twice on one page (The Reserved Amber Rule).
- **Don't** use `ink-400` as a text colour on a light ground.
- **Don't** put a saturated colour field behind an app surface. The privilege is marketing-only,
  and even here it is spent exactly twice.
- **Don't** use a backdrop filter anywhere but the sticky nav, where content genuinely scrolls
  beneath.
- **Don't** add a scroll-reveal, a parallax, or a second animated moment. The page has one, on
  purpose.
- **Don't** build a row of four identical tiles, or a centred headline above one.
- **Don't** carry an ALL-CAPS eyebrow or kicker above a headline. Sections open on the headline
  itself; the only micro-labels are sentence-case labels *inside* a block, naming its contents.
- **Don't** use a typographic glyph as an icon (✓, ✗, →, •) where a drawn icon belongs.
- **Don't** invent a status label outside the backend enums.
- **Don't** blur a value to imply it exists but is withheld — except in the deliberately labelled
  blind-panel demonstration, where the surrounding copy says what is hidden and why. In the
  product itself, withheld scores are counted, never blurred.
- **Don't** stack a shadow on top of a border to manufacture depth.

---

### Verification scope

This system was recorded from a build whose finish review closed **ship**, on evidence captured
at **desktop 1440 and narrow 505 CSS px** (`.impeccable/review/`). **This is not a whole-surface
approval.** Nothing below 505px was ever rendered or reviewed — Chrome on Windows enforces a
~500px minimum window width and `--force-device-scale-factor` does not reduce layout width — so
the rules above are verified at those two widths and unverified below 505px. The mechanical
detector ran once in DEGRADED mode (`htmlparser2`, `css-select`, `css-tree`, `domutils` absent);
its findings are an undercount, not a clean bill.

#### Re-verification after the V1.0 retokening (2026-08-26)

Colour was re-verified **completely and by computation**; layout was **not re-reviewed**, on the
grounds that no structural class changed.

- **Every rendered text node measured against its actual painted background, in the browser:**
  198 nodes, **0 below the WCAG AA floor** (4.5:1, or 3:1 where the computed size and weight
  qualify as large text). This is stronger than the pair table that preceded it, because it
  measures what the page actually paints rather than what it was intended to paint.
- **All 37 distinct token utilities confirmed to emit real CSS.** This was the specific failure
  the retokening existed to prevent — a renamed class silently producing nothing.
- **Both JS-driven components round-tripped** with their computed colours read back: the blind
  panel (`warn-50/warn-700` → `positive-50/positive-700` → back) and the EN/MM toggle.
- **`prefers-color-scheme: dark` emulated:** the page stays light (`#F8FAFC` ground, `#0F172A`
  ink). Zero `dark:` utilities in the file, and `darkMode: 'class'` is set so a future stray one
  is inert.
- **One mark was deliberately not translated step-for-step.** The dot inside the pending pill
  would have gone from 2.97:1 to 2.07:1 under a literal mapping — the only element the migration
  would have made worse. It is `warn-700`, matching its own label, at 4.84:1.

**Known defect, pre-existing and NOT introduced here:** the page overflows horizontally at
narrow widths — measured at a 440px viewport, `scrollWidth` 602 before the retokening and 601
after. It is filed separately with both sources identified. Do not read the "ship" verdict above
as covering it; it was never caught because nothing below 505px was ever reviewed.
