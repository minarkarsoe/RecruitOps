# RecruitOps Design System — "Clear Pipeline"

Design system for a B2B Recruitment Agency Platform (RAaaS).
Import this file into Claude Design as the source of truth for all screens.

---

## 1. Brand Foundation

**Personality:** Trustworthy · Fast · Modern · Calm under pressure
**One-line thesis:** "Your agency, running on rails." Every screen should feel like the pipeline is moving.

**Two surface moods (one system):**
| Surface | Audience | Mood |
|---|---|---|
| Internal App (Dashboard, CRM, ATS) | Recruiters, 8 hrs/day | Calm, low-saturation, dense but scannable |
| Client Portal (no-login shareable link) | Agency clients | Polished, spacious, premium — this is the agency's sales face |

**Design principles:**
1. **Status is always visible.** A user should know the state of any candidate, contract, or job post within 1 second — via the pill system, never by reading paragraphs.
2. **One primary action per screen.** Everything else is secondary or ghost.
3. **Spacious over clever.** Whitespace does the work; no decorative gradients, no glassmorphism, no heavy shadows.
4. **Bilingual-safe.** All text areas must render mixed English + Burmese without clipping (line-height ≥ 1.6 for Burmese script).

---

## 2. Color Tokens

### Core palette
| Token | Hex | Usage |
|---|---|---|
| `ink-900` | `#16232B` | Primary text, headings |
| `ink-600` | `#4A5B66` | Secondary text, labels |
| `ink-400` | `#8A99A3` | Placeholder, disabled, meta text |
| `line-200` | `#E3E9EC` | Borders, dividers, table rules |
| `surface-0` | `#FFFFFF` | Cards, panels |
| `surface-50` | `#F6F9F9` | App background |
| `primary-700` | `#0B5654` | Primary hover, active nav |
| `primary-600` | `#0E6E6B` | **Primary brand** — buttons, links, focus rings, active states |
| `primary-100` | `#DCEFEE` | Primary tint — selected rows, active tab bg, info chips |
| `accent-500` | `#F2A33C` | **Amber accent** — highlights, "attention" moments, Gold tier |
| `accent-100` | `#FCF0DC` | Amber tint backgrounds |

### Semantic (status) colors
| Token | Hex | Tint bg | Meaning |
|---|---|---|---|
| `success-600` | `#1E8E5A` | `#E2F4EA` | Accepted, Placed, Paid, Active contract |
| `warning-600` | `#C97A0A` | `#FCF0DC` | Need More Info, Expiring soon, Pending |
| `danger-600` | `#C94430` | `#FBE8E4` | Rejected, Overdue, Contract expired |
| `info-600` | `#2E6ECF` | `#E6EEFB` | Sent to Client, In Review, New applicant |

### Client tier colors
| Tier | Hex | Tint bg |
|---|---|---|
| Gold | `#D9A441` | `#FBF3E1` |
| Silver | `#8F9CA8` | `#EFF2F5` |
| Bronze | `#B0784A` | `#F6ECE3` |

**Rules:**
- Saturated colors appear ONLY in pills, badges, buttons, and small indicators — never as large background fills.
- Text on tint backgrounds always uses the matching `-600` color (WCAG AA guaranteed).
- Never use pure black `#000` or pure gray `#808080`.

---

## 3. Typography

| Role | Font | Fallback | Notes |
|---|---|---|---|
| Display / Headings | **Bricolage Grotesque** | Inter | Character without being loud; weights 600–700 only |
| Body / UI | **Inter** | system-ui | 400 / 500 / 600 |
| Burmese content | **Noto Sans Myanmar** | — | Auto-fallback in font stack; line-height 1.7 |
| Data / IDs / Mono | **IBM Plex Mono** | monospace | Candidate IDs, phone numbers, dates in tables |

**Font stack (use everywhere):**
`Inter, "Noto Sans Myanmar", system-ui, sans-serif`
Headings: `"Bricolage Grotesque", Inter, "Noto Sans Myanmar", sans-serif`

### Type scale
| Token | Size / Line | Weight | Usage |
|---|---|---|---|
| `display` | 32 / 40 | 700 | Portal hero, empty states |
| `h1` | 24 / 32 | 700 | Page titles |
| `h2` | 19 / 28 | 600 | Card titles, section heads |
| `h3` | 16 / 24 | 600 | Sub-sections, modal titles |
| `body` | 15 / 24 | 400 | Default text |
| `body-strong` | 15 / 24 | 600 | Emphasis, names |
| `small` | 13 / 20 | 400 | Meta, timestamps, helper text |
| `overline` | 11 / 16 | 600 | ALL-CAPS labels, +0.08em tracking |

---

## 4. Spacing, Radius, Elevation, Grid

**Spacing scale (4px base):** 4 · 8 · 12 · 16 · 24 · 32 · 48 · 64
- Card padding: 24. Section gap: 32. Form field gap: 16. Inline gap: 8.

**Radius:**
| Token | Value | Usage |
|---|---|---|
| `r-sm` | 8px | Inputs, pills, chips |
| `r-md` | 12px | Buttons, cards |
| `r-lg` | 16px | Modals, portal cards |
| `r-full` | 999px | Status pills, avatars, tier badges |

**Elevation (use sparingly):**
- `shadow-card`: `0 1px 2px rgba(22,35,43,0.06)` — default cards sit on border, not shadow
- `shadow-pop`: `0 8px 24px rgba(22,35,43,0.12)` — dropdowns, modals, toasts only

**Grid:**
- Internal app: fixed left sidebar 240px + fluid content, max-width 1280, 24px gutters
- Client portal: single centered column, max-width 760px, generous 48px vertical rhythm

---

## 5. Core Components (10 only — keep it this small)

### 5.1 Button
- **Primary:** `primary-600` bg, white text, radius 12, height 40, weight 600. Hover → `primary-700`.
- **Secondary:** white bg, `line-200` border, `ink-900` text.
- **Ghost:** transparent, `primary-600` text.
- **Danger:** `danger-600` bg, white text (destructive confirms only).
- One primary button per view. Icon-left optional, 16px icons.

### 5.2 Status Pill  ★ SIGNATURE COMPONENT
Radius-full, tint background + `-600` text + 6px dot, height 24, padding 4×10, `small` weight 600.
Fixed vocabulary — never invent new labels:
- Candidate pipeline: `Sourced` (ink) · `Shortlisted` (info) · `Sent to Client` (info) · `Interview` (warning) · `Placed` (success) · `Rejected` (danger)
- Client feedback: `Accepted` (success) · `Need More Info` (warning) · `Rejected` (danger)
- Contract: `Active` (success) · `Expiring Soon` (warning) · `Expired` (danger)
- Job post: `Live` (success) · `Draft` (ink) · `Closed` (ink)

### 5.3 Tier Badge
Radius-full, tier tint bg, tier color text + small crown/medal icon. Sizes: 20 (table) / 24 (profile). Appears next to client name everywhere.

### 5.4 Card
White bg, `line-200` 1px border, radius 12, padding 24, `shadow-card`. Optional header row: h2 title left, action right. No nested cards.

### 5.5 Input & Select
Height 40, radius 8, `line-200` border, white bg. Focus: 2px `primary-600` ring. Label above (small, 600), helper/error below (small). Error state: `danger-600` border + message. Never placeholder-as-label.

### 5.6 Table (simple)
Header row: `overline` style, `ink-600`, `surface-50` bg. Rows: 48px min-height, `line-200` bottom rule only (no vertical rules, no zebra). Hover: `surface-50`. Selected: `primary-100`. First column = entity (avatar + name, weight 600); status pill column always right-aligned before actions.

### 5.7 Avatar
Radius-full, sizes 24 / 32 / 40. Fallback: initials on `primary-100` bg, `primary-700` text. Candidate photos never cropped tight — 40px min in lists.

### 5.8 Tabs
Underline style only: active = `ink-900` text + 2px `primary-600` underline; inactive = `ink-600`. Height 44, gap 24. No pill/segmented tabs.

### 5.9 Toast
Bottom-right, white bg, radius 12, `shadow-pop`, left 3px status color bar, auto-dismiss 4s. Message pattern: past-tense verb — "Feedback sent." "Contract renewed."

### 5.10 Empty State
Centered in card: simple line icon (48, `ink-400`), h3 title, one-line small text, one primary button. Copy is an invitation: "No candidates yet — Import from Excel to get started."

---

## 6. Signature Patterns (product-specific)

### 6.1 Pipeline Stage Rail
Horizontal row of stage counts at top of every job order:
`Sourced 24 → Shortlisted 8 → Sent 5 → Interview 2 → Placed 1`
Each stage = tappable chip (count in mono font); active stage uses its pill color. This is the app's identity — always present, always same order.

### 6.2 Client Feedback Bar (Portal)
The three feedback buttons on each candidate card in the client portal:
- `Accept for Interview` — success-600 solid
- `Need More Info` — secondary with warning-600 text
- `Reject` — ghost, danger-600 text
Full-width row, 44px height (thumb-friendly), instant confirmation state after tap (button collapses into its matching status pill).

### 6.3 Portal Candidate Card
The premium surface. White card, radius 16, padding 32, 48px gap between cards. Layout: avatar 56 + name (h2) + role + key facts as quiet chips (experience, salary ask, notice period) → skills row → attached CV button (secondary) → Feedback Bar. No agency-internal data (no source channel, no recruiter notes) ever leaks here.

### 6.4 Expiry Attention Card
Dashboard card listing contracts nearing expiry. Row = client name + tier badge + countdown in mono ("21 days") colored by urgency: >30d ink · 8–30d warning · ≤7d danger. Primary action per row: "Renew".

---

## 7. Screen Patterns

**Recruiter Dashboard:** greeting + today's date → 4 stat cards (Active Jobs, Awaiting Feedback, Interviews This Week, Expiring Contracts) → Pipeline Rail per active job → Expiry Attention Card → recent client feedback feed (real-time, pill-driven).

**Client Portal (no login):** agency logo + "Candidates for [Job Title]" display heading → count line → stack of Portal Candidate Cards → footer "Powered by [Agency]". No nav, no sidebar, nothing clickable except candidate content.

**Candidate Profile:** header (avatar 40, name, current stage pill, dedup notice if merged) → tabs: Profile / Applications / History. Duplicate merge shown as info-tinted banner, never a modal interruption.

**CRM Client List:** table with tier badge column, contract status pill column, sorted by expiry ascending by default.

---

## 8. UX Writing

- Sentence case everywhere. No ALL CAPS except `overline` labels.
- Buttons say the outcome: "Generate portal link", "Send to client", "Renew contract" — never "Submit"/"OK".
- Same word through a flow: button "Send to client" → toast "Sent to client" → pill `Sent to Client`.
- Errors state fix, not fault: "This phone number already belongs to Aung Ko — view profile or merge."
- Numbers in mono font inside tables and countdowns.

---

## 9. Accessibility & Quality Floor

- All text ≥ 4.5:1 contrast (token pairs above are pre-checked).
- Focus ring: 2px `primary-600`, visible on every interactive element.
- Touch targets ≥ 44px on portal (clients open the link on phones).
- Status never by color alone — pill always carries a text label.
- `prefers-reduced-motion` respected; motion limited to 150–200ms ease-out fades/slides.

---

## 10. Do / Don't

**Do:** border-first cards · one accent moment per screen · pills for every status · mono for IDs/dates · generous portal whitespace.
**Don't:** gradients on surfaces · more than 2 font families visible at once · zebra tables · icon-only buttons without labels · saturated color as page background · new status labels outside the fixed vocabulary.
