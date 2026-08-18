---
name: RecruitOps — Marketing Register
description: The marketing voice of "Clear Pipeline" — the same audit-grade tokens as the product, spoken at poster scale.
colors:
  ink-900: "#16232B"
  ink-800: "#1F2B32"
  ink-700: "#2F3D46"
  ink-600: "#4A5B66"
  ink-400: "#8A99A3"
  line-200: "#E3E9EC"
  line-300: "#C2D0D6"
  surface-0: "#FFFFFF"
  surface-50: "#F6F9F9"
  primary-900: "#052A29"
  primary-800: "#08403E"
  primary-700: "#0B5654"
  primary-600: "#0E6E6B"
  primary-500: "#149B97"
  primary-200: "#B8E0DE"
  primary-100: "#DCEFEE"
  primary-50: "#F0F9F9"
  accent-700: "#8A5A08"
  accent-500: "#F2A33C"
  accent-100: "#FCF0DC"
  success-700: "#146B43"
  success-600: "#1E8E5A"
  success-100: "#E2F4EA"
  warning-700: "#8A5A08"
  warning-600: "#C97A0A"
  warning-100: "#FCF0DC"
  danger-700: "#A63423"
  danger-600: "#C94430"
  danger-100: "#FBE8E4"
  info-700: "#22528F"
  info-600: "#2E6ECF"
  info-100: "#E6EEFB"
typography:
  display:
    fontFamily: "Bricolage Grotesque, Inter, Noto Sans Myanmar, sans-serif"
    fontSize: "56px"
    fontWeight: 700
    lineHeight: 1.05
    letterSpacing: "-0.04em"
  headline:
    fontFamily: "Bricolage Grotesque, Inter, Noto Sans Myanmar, sans-serif"
    fontSize: "40px"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "-0.04em"
  title:
    fontFamily: "Bricolage Grotesque, Inter, Noto Sans Myanmar, sans-serif"
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
    fontFamily: "IBM Plex Mono, ui-monospace, SFMono-Regular, monospace"
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
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "20px"
  full: "999px"
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
    backgroundColor: "{colors.primary-600}"
    textColor: "{colors.surface-0}"
    rounded: "{rounded.md}"
    padding: "0 24px"
    height: "48px"
    typography: "{typography.body}"
  button-primary-hover:
    backgroundColor: "{colors.primary-700}"
    textColor: "{colors.surface-0}"
  button-secondary:
    backgroundColor: "{colors.surface-0}"
    textColor: "{colors.ink-900}"
    rounded: "{rounded.md}"
    padding: "0 24px"
    height: "48px"
    typography: "{typography.body}"
  button-inverse:
    backgroundColor: "{colors.surface-0}"
    textColor: "{colors.primary-900}"
    rounded: "{rounded.md}"
    padding: "0 24px"
    height: "48px"
    typography: "{typography.body}"
  card:
    backgroundColor: "{colors.surface-0}"
    textColor: "{colors.ink-600}"
    rounded: "{rounded.xl}"
    padding: "32px"
  pill-status:
    backgroundColor: "{colors.warning-100}"
    textColor: "{colors.warning-700}"
    rounded: "{rounded.full}"
    padding: "0 10px"
    height: "24px"
    typography: "{typography.data}"
  chip-outline:
    backgroundColor: "{colors.surface-50}"
    textColor: "{colors.ink-600}"
    rounded: "{rounded.full}"
    padding: "0 12px"
    height: "28px"
    typography: "{typography.small}"
  rail-node-done:
    backgroundColor: "{colors.primary-100}"
    textColor: "{colors.primary-700}"
    rounded: "{rounded.full}"
    size: "31px"
  rail-node-waiting:
    backgroundColor: "{colors.surface-0}"
    textColor: "{colors.ink-400}"
    rounded: "{rounded.full}"
    size: "31px"
---

# Design System: RecruitOps — Marketing Register

## Overview

**Creative North Star: "The Record, Enlarged"**

This is not a new world. It is the marketing register of **"Clear Pipeline"** — the shipped
RecruitOps design system documented in `RecruitOps_Design_System.md`, whose token source of
truth is `packages/ui/tailwind-preset.js` and whose consumers are `frontend/internal` and
`frontend/public` (ADR-0012). The landing page was built by inheriting that system rather than
forking a marketing identity, and every colour, family and radius below traces back to the
preset. When the preset changes, this file is downstream of it, not beside it.

The register is louder, not different. The product's own claim — *every decision has a record* —
is argued visually by enlarging the product's own artifacts: an approval chain, a status pill, a
mono requisition id, a hairline rail. Nothing decorative is introduced to sell it. The page's
argument structure is one artifact replaced per section, and its materials are ink on a cool
paper ground, teal carrying every act of authority, and amber held back for the single moment a
human is being asked to look. Surfaces are border-first: a card sits on a 1px `line-200` rule,
not on a shadow. Figures are always monospaced and tabular, because a number you cannot line up
is a number you cannot audit.

Four things the marketing surface is licensed to do that the app is not, and they are the whole
extension: **full-bleed saturated colour fields** (the deep-teal sovereignty section, the
`ink-900` closing call — the app forbids saturated page backgrounds); **page-scale radii** (20px
on poster containers, above the app's 16px ceiling); **a frosted sticky navigation** (the app's
system says no glassmorphism — here it is a specific behaviour over scrolling content, not
ornament); and **a display scale reaching 56px**, where the app's ramp tops out at 32px. Outside
those four, the app's rules bind here unchanged.

**Key Characteristics:**
- Border-first surfaces; hairline rules do the structural work that shadows do elsewhere
- Asymmetric 12-column splits (5/7, 7/5, 6/6, 4/8) — never a row of identical tiles
- Every figure in IBM Plex Mono with tabular numerals
- Teal for authority, amber for attention, and nothing else saturated
- One authored motion moment on the whole page; no scroll-reveal anywhere
- Bilingual by construction: Burmese is a peer rendering, never a translation footnote

## Colors

A cool, low-saturation ground carrying exactly two voices: a deep teal that means authority and
an amber that means look here.

### Primary
- **Governance Teal** (`primary-600`): every act of authority — primary buttons, links on hover,
  focus rings, the caret, the completed-node check, the logo mark. It is the only colour allowed
  to fill a button on a light ground.
- **Deep Pipeline** (`primary-900` / `primary-800` / `primary-700`): the full-bleed sovereignty
  field and its interior rules. `primary-900` is a page background; `primary-800` is its border
  and node fill; `primary-700` is the dark rail's connector and the light-ground hover.
- **Teal Tints** (`primary-200` / `primary-100` / `primary-50`): completed rail nodes, the
  highlighted Enterprise column, the "You" row in the panel widget, selection highlight.
- **Signal Teal** (`primary-500`): reserved for hover on the inverse-ground CTA, where
  `primary-700` would go backwards against a dark field.

### Secondary
- **Threshold Amber** (`accent-500`, tint `accent-100`, text `accent-700`): attention and
  threshold breach only. It appears as a fill in exactly one place on the page — the third node
  of the logo mark — and as a tinted note in exactly one place: the hero's salary-band breach.

### Neutral
- **Ink** (`ink-900` through `ink-600`): `ink-900` is body text, headings, and the closing
  section's full-bleed ground; `ink-800` and `ink-700` are its interior rules and secondary body;
  `ink-600` carries every piece of meta text, caption and secondary paragraph on light grounds.
- **Fog** (`ink-400`): non-text only — the waiting-node dot, the scrollbar hover thumb, and
  footer body against `ink-900` where it clears contrast as inverse text.
- **Rule** (`line-200` / `line-300`): `line-200` is every border, divider, table rule and the
  drawn spreadsheet grid; `line-300` is the heavier hollow-node and index-circle stroke.
- **Paper** (`surface-50`) and **Panel** (`surface-0`): the page ground and the card ground. A
  section alternates between them to mark a change of subject without introducing a third value.

### Tertiary
- **Status families** (`success` / `warning` / `danger` / `info`): used only inside pills, chips
  and small indicators, exactly as in the app. Each family is a `-600` fill, a `-700`
  text-on-tint, and a `-100` tint.

### Named Rules

**The Seven-Hundred Rule.** Text on a `-100` tint uses the `-700` step of *its own family*,
never `-600`. This is measured, not assumed: on 2026-08-17 the old `-600`-on-`-100` pairs were
found at 2.97 (warning), 3.62 (success), 4.08 (danger) and 4.23 (info) against a 4.5:1 floor.
The `-700` steps exist because of that measurement. Do not reintroduce the retired rule.

**The Reserved Amber Rule.** `accent-500` means *a human should look at this* — a breached
threshold, an approval waiting on someone. It is spent on one element per page. If a second
amber appears, the first one has stopped meaning anything.

**The Not-A-Text-Colour Rule.** `ink-400` is not a text colour on light grounds (2.77:1 on
`surface-50`). Meta text is `ink-600`.

**The Two-Field Rule.** Full-bleed saturated colour is a marketing privilege and it is spent
twice: once on the deep-teal sovereignty section, once on the `ink-900` close. A third field
would turn a structural beat into wallpaper, and none of it is permitted on app surfaces.

## Typography

**Display Font:** Bricolage Grotesque (with Inter, then Noto Sans Myanmar)
**Body Font:** Inter (with Noto Sans Myanmar, system-ui)
**Label/Mono Font:** IBM Plex Mono (with ui-monospace, SFMono-Regular)
**Burmese:** Noto Sans Myanmar, reached through the stack and forced by the `.mm` class

**Character:** Bricolage Grotesque has enough irregularity to feel authored rather than
defaulted, and at 700 with -0.04em tracking it reads as a statement rather than a header. Inter
does all the arguing underneath it at a generous 1.65–1.7 line-height, which is a Burmese
requirement before it is a taste. IBM Plex Mono is not decoration: it marks the values that
belong to the record.

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
date, a score, an id — is IBM Plex Mono with `font-variant-numeric: tabular-nums`. Prose numbers
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
64/80 opening to 96/112. Section boundaries are a 1px `line-200` rule or a change of ground
(`surface-50` ↔ `surface-0` ↔ a full-bleed field) — never both at once.

**Spacing scale.** 4px base: 4 · 8 · 12 · 16 · 24 · 32 · 48 · 64 · 80 · 112. Card padding is 24px
rising to 32px at `sm`. Grid gutters between cards are 24px. Inline gaps are 8–14px.

**The hairline-gap grid.** Statistic and definition grids are built as a `line-200` background
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
table sits on a 1px `line-200` rule against a `surface-0` or `surface-50` ground; a card that
needs to read as raised gets a different ground, not a bigger shadow. Overlap — the hero's
spreadsheet sitting above and behind the requisition record — does the one piece of genuine
z-work on the page.

### Shadow Vocabulary
- **Card** (`box-shadow: 0 1px 2px rgba(22,35,43,0.06)`): a hairline seat under a primary button
  or a selected tour step. It is a contact shadow, not a lift.
- **Pop** (`box-shadow: 0 8px 24px rgba(22,35,43,0.12)`): transient overlays only — on this page,
  the skip link when focused.
- **Lift** (`box-shadow: 0 24px 60px -18px rgba(22,35,43,0.28)`): the marketing-only poster tier.
  Reserved for the single hero artifact that the page's whole argument resolves into. One
  element, one page.

### Named Rules

**The One Lift Rule.** `shadow-lift` exists to separate the hero record from the artifact
dissolving behind it. A second element carrying it makes neither of them the subject.

**The Border-First Rule.** If a surface needs definition, give it a `line-200` border and a
ground. Reach for a shadow only when something genuinely floats over scrolling content.

## Shapes

Rounded, but never soft. Radii step with scale rather than with importance: 8px on inputs,
pills-that-are-not-pills and small inline notes; 12px on buttons, inner cards and the mobile
nav; 16px on grouped panels and the tour step list; **20px on poster-scale containers** — the
hero record, the section cards, the tier table shell — which is the marketing extension above
the app's 16px ceiling; and fully round (999px) on status pills, chips, rail nodes, the language
toggle and the numeric step indices.

Two recurring silhouettes define the page. The **bordered card with a tinted foot**: a white
body carrying the claim, and a `surface-50` compartment below a `line-200` rule carrying the
evidence — a status flow, a widget, a bilingual pair. And the **hairline rail**: a 1px vertical
connector running between 31px round nodes, the same geometry on light and dark grounds.

Icons are drawn — Lucide line icons at 12–20px, plus a hand-built SVG logo mark. Typographic
glyphs are never used as icons: the tier table's included/not-included marks are drawn check and
minus icons with screen-reader text, not ✓ and ✗ characters.

## Components

### Buttons
- **Shape:** Gently rounded (12px), 48px tall at page scale, 40–44px inside widgets and nav.
- **Primary:** `primary-600` fill, white text, 15px/600, 24px horizontal padding, `shadow-card`.
- **Hover / Focus:** background to `primary-700` on light grounds and `primary-500` on dark
  grounds, over a 150ms colour transition. Focus is the global 2px `primary-600` ring at 3px
  offset — never a background change alone.
- **Secondary:** white fill, `line-200` border, `ink-900` text; border moves to `primary-200` on
  hover. Optional trailing Lucide icon in `primary-600`.
- **Inverse:** on a `primary-900` field, a white fill with `primary-900` text; its sibling is a
  bordered ghost with `ink-700` border and white text.

### Chips
- **Style:** Fully round, 28px tall, `surface-50` fill with a `line-200` border and `ink-600`
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
- **Background:** `surface-0` body; `surface-50` for the evidence foot below a `line-200` rule.
- **Shadow Strategy:** none by default — see Elevation & Depth. The hero record is the exception.
- **Border:** 1px `line-200` on light grounds, `primary-800` on the deep-teal field.
- **Internal Padding:** 24px, rising to 32px at `sm`. Never nested cards more than one deep.

### Navigation
- **Style:** Sticky, 64px, frosted — `rgba(246,249,249,.78)` under `saturate(1.6) blur(14px)` —
  with a `line-200` bottom rule. This is the page's only backdrop filter, justified by content
  scrolling beneath it.
- **Links:** 15px `ink-600`, moving to `primary-700` on hover, colour transition only. No
  underline, no active-state indicator.
- **Right cluster:** an EN/MM language toggle in a fully round bordered group, then the primary
  CTA. Below `lg` the links collapse into a bordered icon button opening a white panel that
  repeats every link, the toggle and the CTA at touch size (44px+).

### Governance Rail — signature component
The page's defining device, and the same one the product's design system documents as the
Approval Chain Rail. A 1px `line-200` connector runs from each node down to the next, suppressed
on the last; a completed node switches its connector to `primary-200`. Nodes are 31px circles:
**done** is `primary-100` with a `primary-200` border and a `primary-700` check; **waiting** is
white with a `line-300` border and an `ink-400` dot. Each node carries a name, a role and a mono
timestamp, and a threshold breach hangs off its node as an `accent-100` note bordered in
25%-opacity `accent-500`.

A `rail-dark` variant reuses the identical geometry on the `primary-900` field with a
`primary-700` connector and `primary-800` node fills. It is used there deliberately: those items
are sequential guarantees about one record's life, and rendering them as a rail rather than four
tiles is the claim.

**A rejected round is preserved beside its revision, never over it** — dimmed to `surface-50`
with its rejection comment intact. Never collapse history to a count.

### Interactive Demonstrations
Two widgets let a reader operate the product's actual rules rather than read about them: the
blind-panel scorecard (scores blurred at 7px under `[data-locked="true"]`, revealing on submit,
with the pill moving warning → success) and the five-step workflow tour (a `role="tablist"` of
bordered steps with full arrow/Home/End keyboard support; the selected step turns white with a
`primary-200` border, `shadow-card`, and a `primary-600` index disc). Selection is signalled by
ground, border and index fill together — never by colour alone.

## Do's and Don'ts

### Do:
- **Do** take every token from `packages/ui/tailwind-preset.js`. This surface is a register of
  "Clear Pipeline", not a second identity; a colour that is not in the preset is a fork.
- **Do** put `-700` text on a `-100` tint, from the same family (The Seven-Hundred Rule).
- **Do** set every checkable figure in IBM Plex Mono with `.tnum`.
- **Do** define surfaces with a 1px `line-200` border and a ground change (The Border-First Rule).
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
