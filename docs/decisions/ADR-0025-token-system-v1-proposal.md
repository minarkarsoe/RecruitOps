# ADR-0025 — Token system "RecruitOps V1.0" (proposed)

- **Date:** 2026-08-17
- **Status:** ✅ **Accepted 2026-08-17.** V1.0 replaces "Clear Pipeline" as the product's token
  system. Superseded sections of `RecruitOps_Design_System.md` are listed under *Decision* below.
  **Adoption is staged — see the sequencing note. No shipped code has moved yet.**
- **Related:** [ADR-0012](ADR-0012-frontend-split.md) (shared preset is the anti-drift mechanism),
  [ADR-0007](ADR-0007-productization-and-addons.md) (no per-surface forks),
  `RecruitOps_Design_System.md` (the adopted system), `DESIGN.md` (the built marketing world)

## Context

A token specification labelled **"DESIGN SYSTEM TOKENS — RECRUITOPS V1.0"** was supplied on
2026-08-17, after the "Clear Pipeline" system had been pivoted to the in-house product and the
marketing landing page had been built, reviewed and documented against it the same day.

It was supplied without an instruction, so **nothing was changed**. This ADR exists so the
specification is not lost and so the conflicts are stated before anyone acts on it.

## The proposed specification, as supplied

- **Theme:** Enterprise B2B SaaS (clean light canvas with slate accents)
- **Colors:** Canvas `#F8FAFC` · Card background `#FFFFFF` · Primary accent `#0F766E` ·
  Secondary/status emerald `#10B981` · Amber warning `#F59E0B` · Crimson alert `#EF4444` ·
  Slate typography `#0F172A` · Subdued border `#E2E8F0`
- **Typography:** Inter or Plus Jakarta Sans (headers SemiBold/Bold, `tracking-tight`; body
  Regular/Medium)
- **Containers:** Clean bento grids, `rounded-2xl` / `rounded-xl`, 1px `#E2E8F0` border,
  subtle ambient `shadow-sm`
- **Status chips:** Rounded pills, 12px, soft background fills (e.g. `bg-emerald-50
  text-emerald-700`)
- **Iconography:** Lucide / Phosphor outline, 16px / 20px
- **Language:** Bilingual English + Myanmar Unicode toggle

## Conflicts with what is currently shipped

| Role | V1.0 proposes | Shipped (`packages/ui/tailwind-preset.js`) |
|---|---|---|
| Ink / typography | `#0F172A` | `#16232B` |
| Canvas | `#F8FAFC` | `#F6F9F9` |
| Primary | `#0F766E` | `#0E6E6B` (hover `#0B5654`) |
| Attention | emerald `#10B981` + amber `#F59E0B` | amber `#F2A33C`, **reserved to one moment per page** |
| Alert | `#EF4444` | `#C94430` |
| Border | `#E2E8F0` | `#E3E9EC` |
| Display face | Inter / Plus Jakarta Sans | **Bricolage Grotesque** |
| Radius | `rounded-2xl` (24px) | 8 / 12 / 16px, with 20px allowed at marketing page scale only |
| Elevation | `shadow-sm` ambient | border-first; `shadow-card`, `shadow-pop`, one `shadow-lift` |

Three of these are not cosmetic:

1. **It forks the design system.** `packages/ui` is consumed by `frontend/internal` and
   `frontend/public`; ADR-0012 makes the shared preset the anti-drift mechanism, and a third
   palette reopens precisely the doc-versus-token divergence fixed earlier the same day.
2. **It removes the amber reservation.** In the shipped system amber means *a human should
   look at this* — a breached budget threshold, an approval waiting. V1.0 lists both an emerald
   status colour and an amber warning colour, which turns a reserved signal into an ordinary
   palette entry.
3. **Plus Jakarta Sans is a saturated default.** Bricolage Grotesque is already loaded by both
   apps, carries more character, and costs nothing to keep.

## What V1.0 gets right, and should survive whatever is decided

- **`bg-emerald-50` / `text-emerald-700` is the correct pattern** — text on a tint at the `-700`
  step. That is the same correction applied on 2026-08-17 after the old `-600`-on-`-100` pairs
  were measured failing WCAG AA.
- Lucide / Phosphor outline icons and the bilingual toggle both match what is built.

## ⚠️ Contrast warning if this palette is adopted

`#F59E0B` on a light amber tint repeats the failure just fixed: the previous amber pair measured
**2.97:1** at pill size against a 4.5:1 floor. `#EF4444` on a light red tint is likely to fail the
same way. **Any adoption must add darker `-700` text steps and measure them**, not assume the
pairs are safe. `RecruitOps_Design_System.md` asserted its pairs were "AA guaranteed" for a year
and was wrong about five of them.

## Decision (2026-08-17)

**V1.0 is adopted as the product's token system.** Clear Pipeline is superseded.

### The contrast warning above, corrected

The warning was written against a misreading and is **withdrawn as stated**. V1.0 lists
`#F59E0B` and `#EF4444` as *colours*, and separately specifies status chips as
`bg-emerald-50 text-emerald-700` — the `-700`-on-`-50` pattern, which is the same correction
made to Clear Pipeline earlier the same day. Measured on Tailwind's own scale that pattern
clears the 4.5:1 floor comfortably (emerald ≈5.3, amber ≈5.3, red ≈6.2, teal ≈5.9).

**The real rule, which V1.0 already implies:** `-500` steps are fills, icons and borders;
`-700` steps are text on a `-50`/`-100` tint. A `-500` used as text on a light fill is the
failure mode, and it is what actually happened in Clear Pipeline. Every pair still gets
measured before it ships rather than assumed.

### Sequencing — design first, then code

The rebrand is **not** executed by editing the preset first. Order:

1. **Design in V1.0** as static HTML: a component kit, then the internal SPA's core screens.
   These are the specification.
2. **Approve the designs.**
3. **Then** move `packages/ui/tailwind-preset.js`, the two frontends, and
   `RecruitOps_Design_System.md` onto V1.0, implementing against the approved screens.
4. `marketing/landing.html` and `DESIGN.md` move last, since the landing page is already
   shipped and reviewed and has no dependents.

> **Step 4 done for the landing page — 2026-08-26.** `marketing/landing.html` and `DESIGN.md`
> are on V1.0. 235 class occurrences and 30 hexes moved; the page's structure, copy and motion
> are untouched, so the finish review still stands. Verified in the browser: **198 rendered text
> nodes measured against their actual painted backgrounds, 0 below the AA floor**, and all 37
> distinct token utilities confirmed to emit real CSS.
>
> Three notes for whoever reads this next:
>
> - **The `accent` / `warning` conflict this ADR worried about was not real.** The Context
>   section above objected that V1.0 "removes the amber reservation" by listing both an emerald
>   status colour and an amber warning colour, turning a reserved signal into an ordinary palette
>   entry. Measured at migration time, Clear Pipeline's `accent` and `warning` families had
>   **identical hexes at every step** (`-700 #8A5A08`, `-600 #C97A0A`, `-100 #FCF0DC`; only
>   `accent` carried a `-500`). The reservation was already a convention, not a colour. Merging
>   them into `warn` cost a name and nothing else. The objection was right about the risk and
>   wrong about the mechanism.
> - **One rule died.** The marketing surface's *page-scale radii* extension (20px poster
>   containers above the app's 16px ceiling) is gone: V1.0's re-cut ramp puts `xl` at 16px, so
>   marketing and app containers are now the same radius. Recorded in `DESIGN.md`.
> - **`RecruitOps_Design_System.md` is still on Clear Pipeline** — 27 references, and its title
>   is "Clear Pipeline". That file belongs to **step 3**, not step 4, and step 3 was closed
>   without it. Step 4 does not close it and this note does not either.

Rebranding the preset before the screens exist would mean redesigning against a moving target,
and would break both running frontends for no delivered benefit.

### What this costs, recorded honestly

- `RecruitOps_Design_System.md` was rewritten off the agency model earlier the same day. Its
  **structure, vocabulary and signature patterns survive** — Approval Chain Rail, Blind Panel
  Scorecard, department-scope `404`, the status vocabulary tied to the four backend enums. Only
  the **colour and type tokens** are superseded.
- `DESIGN.md` and `marketing/landing.html` document and implement Clear Pipeline. Both go stale
  the moment step 3 lands, and step 4 is what closes that.
- Bricolage Grotesque is dropped in favour of Inter / Plus Jakarta Sans. Noted at the time that
  Plus Jakarta Sans is a saturated default face; the brief pins it and the brief wins.

## Options considered, and the one taken

1. **Record only** — what happened. No code touched.
2. **Reskin the marketing surface** to V1.0, forking it from `packages/ui`, and rewrite
   `DESIGN.md`, which documents the current built world.
3. **Rebrand the product** — replace Clear Pipeline in the preset, both frontends, the design
   system doc and the landing page. Supersedes the 2026-08-17 pivot.
4. **Reconcile** — diff the two and propose one merged system for approval before any code
   changes.

## Consequences of leaving this proposed

The specification is captured and discoverable, and nothing shipped is at risk. The cost is that
**two token systems are now written down in this repository**, which is the condition this
project has already been bitten by once. If V1.0 is not going to be adopted, this ADR should be
marked Rejected and closed rather than left open indefinitely.
