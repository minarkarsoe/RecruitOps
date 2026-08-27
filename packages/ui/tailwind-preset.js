// The single source of truth for the product's design tokens. BOTH apps import this preset —
// tokens must never be redefined per app, or the two frontends drift apart (ADR-0012).
//
// ============================================================================================
// V1.0 (ADR-0025). These values are a copy of `design/internal/ds.js`, and that is the
// direction of truth: the design kit is authored first and this file follows it. If the two
// ever disagree, the kit is right and this file is stale.
//
// V1.0's hexes are Tailwind's own defaults, so the semantic names are aliases, not a custom
// palette:
//     ink      #0F172A  slate-900        canvas  #F8FAFC  slate-50
//     line     #E2E8F0  slate-200        brand   #0F766E  teal-700
//     positive #10B981  emerald-500      warn    #F59E0B  amber-500
//     critical #EF4444  red-500
//
// Text on a -50/-100 tint always uses the -700 step. A -500 used as text on a light fill is
// the failure that shipped in the previous system and was fixed by measurement, not by eye.
// ============================================================================================
export default {
  // ⚠️ V1.0 HAS NO DARK THEME, and this line is what makes that true rather than aspirational.
  //
  // Tailwind's default is `darkMode: 'media'`, so a stray `dark:` utility fires on any machine
  // whose OS is set to dark — no opt-in, no `.dark` class needed. `features/analytics` carried 97
  // of them while `index.css` declared `color-scheme: light` and the shell had none at all, so on
  // a dark-mode machine the analytics page painted `bg-zinc-800` panels and `text-zinc-400` labels
  // onto a light canvas. Measured live in the running app on 2026-08-25: body still
  // `rgb(248,250,252)`, labels `rgb(148,163,184)` — **2.45:1**, and the error banner rendered
  // near-black translucent red. Half an app in the wrong theme, visible to nobody who develops in
  // light mode.
  //
  // The 97 classes are gone. `'class'` means the next one that slips in is inert until someone
  // deliberately puts `.dark` on an ancestor, which is a decision rather than an accident. When a
  // real dark theme is designed, this is where it turns on.
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // ---------------------------------------------------------------- V1.0
        ink: {
          900: '#0F172A', 800: '#1E293B', 700: '#334155',
          600: '#475569', 500: '#64748B', 400: '#94A3B8',
        },
        canvas: '#F8FAFC',
        brand: {
          50: '#F0FDFA', 100: '#CCFBF1', 200: '#99F6E4',
          600: '#0D9488', 700: '#0F766E', 800: '#115E59', 900: '#134E4A',
        },
        positive: { 50: '#ECFDF5', 100: '#D1FAE5', 500: '#10B981', 700: '#047857' },
        warn: { 50: '#FFFBEB', 100: '#FEF3C7', 500: '#F59E0B', 700: '#B45309' },
        critical: { 50: '#FEF2F2', 100: '#FEE2E2', 500: '#EF4444', 700: '#B91C1C' },
        info: { 50: '#EFF6FF', 100: '#DBEAFE', 500: '#3B82F6', 700: '#1D4ED8' },

        line: {
          DEFAULT: '#E2E8F0',
          strong: '#CBD5E1',
          // COMPAT — numeric steps, see the block below. 300 and 100 were used by 33 classes
          // that emitted no CSS at all before this change, because the old preset defined
          // only `line-200`.
          100: '#F1F5F9',
          200: '#E2E8F0',
          300: '#CBD5E1',
        },

        // ======================================================================================
        // COMPATIBILITY LAYER — temporary, and it has an exit condition.
        //
        // ADR-0025 step 3 moves ~1,120 class usages in the two frontends off these names. Doing
        // that in one commit would be one enormous unreviewable diff; doing it without these
        // aliases would leave the apps unstyled between commits. So the palette flips once,
        // here, and the screens follow one area at a time.
        //
        // Every entry below points at its V1.0 equivalent, so the app is already ON V1.0 colours
        // — only the class NAMES are old. Nothing here introduces a colour the kit does not have.
        //
        // ⚠️ DELETE THIS BLOCK when the count is zero. To check:
        //     grep -rEo "(primary|success|warning|danger|accent|surface|zinc|cyan|teal)-[0-9]+" \
        //       --include=*.ts --include=*.tsx --include=*.css \
        //       frontend/internal/src frontend/public/app packages/ui/src | wc -l
        //
        // ⚠️⚠️ THAT COUNT OVER-REPORTS AND CANNOT REACH ZERO AS WRITTEN. It matches comment prose
        // as well as class names. Measured 2026-08-27 it says **43**, of which only **38** are
        // live classes; the other five are `//` lines in `features/analytics/` recording the old
        // chart palette that failed the CVD validator (`teal-500`, `emerald-500`, `zinc-100`).
        // Analytics itself is fully migrated. Check per-file before believing the total:
        //     ... | sed 's/:.*//' | sort | uniq -c | sort -rn
        //
        // The live 38 sit ENTIRELY inside the two orphan folders parked by the product owner on
        // 2026-08-25 — `features/interviews/BlindScorecardDrawer.tsx` (24) and
        // `features/requisitions/` (14). This block is blocked on that decision and nothing else.
        //
        // ⚠️ The `--include` filters and `public/app` are load-bearing. Pointed at
        // `frontend/public`, this grep also reads `.next/` — the build output, which contains
        // compiled copies of the same classes and can never reach zero however much source is
        // migrated. Measured 2026-08-25: `frontend/public` reports 78, of which 66 are build
        // artifacts and 12 are real. A count that cannot reach zero is not an exit condition.
        //
        // Do NOT add anything to this block. A new screen that needs a token it does not have is
        // a screen that should be using the V1.0 name.
        // ======================================================================================
        primary: { 700: '#0F766E', 600: '#0D9488', 100: '#CCFBF1' },   // → brand
        success: { 700: '#047857', 600: '#10B981', 100: '#D1FAE5' },   // → positive
        warning: { 700: '#B45309', 600: '#F59E0B', 100: '#FEF3C7' },   // → warn
        danger:  { 700: '#B91C1C', 600: '#EF4444', 100: '#FEE2E2' },   // → critical
        accent:  { 700: '#B45309', 500: '#F59E0B', 100: '#FEF3C7' },   // → warn (it always was)
        surface: {
          0: '#FFFFFF',
          50: '#F8FAFC',    // → canvas
          100: '#F1F5F9',   // 13 usages emitted no CSS before this change
          200: '#E2E8F0',   // 1 usage, same
        },
        // Three ramps aliased onto slate/teal. `zinc` had 147 usages; `cyan` and `teal` were
        // the old brand ramp under Tailwind's own names, which is why they existed at all.
        zinc: {
          50: '#F8FAFC', 100: '#F1F5F9', 200: '#E2E8F0', 300: '#CBD5E1', 400: '#94A3B8',
          500: '#64748B', 600: '#475569', 700: '#334155', 800: '#1E293B', 900: '#0F172A',
          950: '#020617',
        },
        cyan: {
          50: '#F0FDFA', 100: '#CCFBF1', 200: '#99F6E4',
          500: '#14B8A6', 600: '#0D9488', 700: '#0F766E', 800: '#115E59', 900: '#134E4A',
        },
        teal: {
          50: '#F0FDFA', 100: '#CCFBF1', 200: '#99F6E4',
          500: '#14B8A6', 600: '#0D9488', 700: '#0F766E', 800: '#115E59', 900: '#134E4A',
        },
      },

      // Operate mode: one family carries headings, labels, data and body. No display face — a
      // display font in a UI label is a product-slop tell, and V1.0 drops Bricolage Grotesque
      // for that reason.
      //
      // The `display` compat alias is GONE (2026-08-27). It existed so `font-display` kept
      // rendering as Inter during the migration; measured repo-wide on that date, **0 usages
      // remain**, so `font-display` now emits no CSS — which is the correct outcome for a class
      // that names a typeface this system does not have.
      fontFamily: {
        sans: ['Inter', '"Noto Sans Myanmar"', 'system-ui', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'monospace'],
      },

      // Fixed rem scale, ~1.15 ratio. Deliberately not fluid: users sit at a consistent DPI and
      // a heading that shrinks inside a panel looks broken.
      //
      // ⚠️ This REPLACES Tailwind's defaults for these names — `text-base` is 14px here, not 16.
      // That is the product's density, and it is why the kit's screens read as an operations
      // tool rather than a marketing page.
      fontSize: {
        '2xs': ['11px', { lineHeight: '16px' }],
        xs:    ['12px', { lineHeight: '16px' }],
        sm:    ['13px', { lineHeight: '20px' }],
        base:  ['14px', { lineHeight: '20px' }],
        md:    ['15px', { lineHeight: '22px' }],
        lg:    ['16px', { lineHeight: '24px' }],
        xl:    ['18px', { lineHeight: '26px' }],
        '2xl': ['20px', { lineHeight: '28px' }],
        '3xl': ['24px', { lineHeight: '32px' }],
      },

      borderRadius: {
        sm: '6px', DEFAULT: '8px', md: '10px', lg: '12px', xl: '16px', '2xl': '20px',
        full: '999px', // COMPAT — V1.0 has no `full`; pills use it and are not yet migrated
      },

      // Three tiers, and the app uses the first two. The `pop` compat alias (→ overlay) is GONE
      // (2026-08-27): measured repo-wide on that date, 0 usages remained.
      boxShadow: {
        sm:      '0 1px 2px 0 rgba(15,23,42,.05)',
        card:    '0 1px 3px 0 rgba(15,23,42,.07), 0 1px 2px -1px rgba(15,23,42,.05)',
        overlay: '0 10px 30px -8px rgba(15,23,42,.20), 0 4px 10px -4px rgba(15,23,42,.10)',
      },

      // 150–250ms. Users are mid-task; nobody wants to watch choreography.
      transitionDuration: { DEFAULT: '160ms', slow: '220ms' },
    },
  },
};
