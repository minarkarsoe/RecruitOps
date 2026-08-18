// The single source of truth for the "Clear Pipeline" design system
// (see /RecruitOps_Design_System.md). BOTH apps import this preset — tokens must
// never be redefined per app, or the two frontends drift apart (ADR-0012).
export default {
  theme: {
    extend: {
      colors: {
        ink: { 900: '#16232B', 600: '#4A5B66', 400: '#8A99A3' },
        line: { 200: '#E3E9EC' },
        surface: { 0: '#FFFFFF', 50: '#F6F9F9' },
        primary: { 700: '#0B5654', 600: '#0E6E6B', 100: '#DCEFEE' },
        // The -700 steps are the text colour on a matching -100 tint. The -600 steps stay for
        // solid fills, icons and borders. Measured 2026-08-17: -600 on -100 does NOT reach
        // 4.5:1 at pill size (warning 2.97, success 3.62, danger 4.08, info 4.23), so the
        // design system's old "-600 on tint is AA guaranteed" rule was simply untrue.
        accent: { 700: '#8A5A08', 500: '#F2A33C', 100: '#FCF0DC' },
        success: { 700: '#146B43', 600: '#1E8E5A', 100: '#E2F4EA' },
        warning: { 700: '#8A5A08', 600: '#C97A0A', 100: '#FCF0DC' },
        danger: { 700: '#A63423', 600: '#C94430', 100: '#FBE8E4' },
        info: { 700: '#22528F', 600: '#2E6ECF', 100: '#E6EEFB' },
        zinc: {
          50: '#F6F9F9',
          100: '#E3E9EC',
          200: '#E3E9EC',
          300: '#C2D0D6',
          400: '#8A99A3',
          500: '#687A85',
          600: '#4A5B66',
          700: '#2F3D46',
          800: '#1F2B32',
          900: '#16232B',
          950: '#0F181E',
        },
        cyan: {
          50: '#F0F9F9',
          100: '#DCEFEE',
          200: '#B8E0DE',
          500: '#149B97',
          600: '#0E6E6B',
          700: '#0B5654',
          800: '#08403E',
          900: '#052A29',
        },
        teal: {
          50: '#F0F9F9',
          100: '#DCEFEE',
          200: '#B8E0DE',
          500: '#149B97',
          600: '#0E6E6B',
          700: '#0B5654',
          800: '#08403E',
          900: '#052A29',
        },
      },
      fontFamily: {
        sans: ['Inter', '"Noto Sans Myanmar"', 'system-ui', 'sans-serif'],
        display: ['"Bricolage Grotesque"', 'Inter', '"Noto Sans Myanmar"', 'sans-serif'],
        mono: ['"IBM Plex Mono"', 'monospace'],
      },
      borderRadius: { sm: '8px', md: '12px', lg: '16px', full: '999px' },
      boxShadow: {
        card: '0 1px 2px rgba(22,35,43,0.06)',
        pop: '0 8px 24px rgba(22,35,43,0.12)',
      },
    },
  },
};
