/* ===========================================================================
   RecruitOps — Internal app design system (V1.0 tokens, ADR-0025)
   ---------------------------------------------------------------------------
   Shared by every static design page in this folder. Change a token here and
   every screen follows, which is the whole reason these are not one-off files.

   Load order in each page:
     <script src="https://cdn.tailwindcss.com"></script>
     <script src="ds.js"></script>
     <link rel="stylesheet" href="ds.css" />

   V1.0's hexes are Tailwind's own defaults, so the semantic names below are
   aliases rather than a custom palette:
     ink      #0F172A  slate-900      canvas  #F8FAFC  slate-50
     line     #E2E8F0  slate-200      brand   #0F766E  teal-700
     positive #10B981  emerald-500    warn    #F59E0B  amber-500
     critical #EF4444  red-500
   Text on a -50/-100 tint always uses the -700 step. A -500 used as text on a
   light fill is the failure that shipped in the previous system.
   =========================================================================== */

tailwind.config = {
  theme: {
    extend: {
      colors: {
        ink: {
          900: '#0F172A', 800: '#1E293B', 700: '#334155',
          600: '#475569', 500: '#64748B', 400: '#94A3B8',
        },
        canvas: '#F8FAFC',
        line: { DEFAULT: '#E2E8F0', strong: '#CBD5E1' },
        brand: {
          50: '#F0FDFA', 100: '#CCFBF1', 200: '#99F6E4',
          600: '#0D9488', 700: '#0F766E', 800: '#115E59', 900: '#134E4A',
        },
        positive: { 50: '#ECFDF5', 100: '#D1FAE5', 500: '#10B981', 700: '#047857' },
        warn:     { 50: '#FFFBEB', 100: '#FEF3C7', 500: '#F59E0B', 700: '#B45309' },
        critical: { 50: '#FEF2F2', 100: '#FEE2E2', 500: '#EF4444', 700: '#B91C1C' },
        info:     { 50: '#EFF6FF', 100: '#DBEAFE', 500: '#3B82F6', 700: '#1D4ED8' },
      },

      /* Operate mode: one family carries headings, labels, data and body.
         No display face — a display font in a UI label is a product-slop tell. */
      fontFamily: {
        sans: ['Inter', '"Noto Sans Myanmar"', 'system-ui', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'monospace'],
      },

      /* Fixed rem scale, ~1.15 ratio. Deliberately not fluid: users sit at a
         consistent DPI and a heading that shrinks inside a panel looks broken. */
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

      borderRadius: { sm: '6px', DEFAULT: '8px', md: '10px', lg: '12px', xl: '16px', '2xl': '20px' },

      boxShadow: {
        sm:      '0 1px 2px 0 rgba(15,23,42,.05)',
        card:    '0 1px 3px 0 rgba(15,23,42,.07), 0 1px 2px -1px rgba(15,23,42,.05)',
        overlay: '0 10px 30px -8px rgba(15,23,42,.20), 0 4px 10px -4px rgba(15,23,42,.10)',
      },

      /* 150–250ms. Users are mid-task; nobody wants to watch choreography. */
      transitionDuration: { DEFAULT: '160ms', slow: '220ms' },
    },
  },
};
