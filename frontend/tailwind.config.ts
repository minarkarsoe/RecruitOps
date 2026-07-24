import type { Config } from 'tailwindcss';

// Encodes the "Clear Pipeline" design system (RecruitOps_Design_System.md).
// Saturated colors are for pills/badges/buttons only — never large fills.
const config: Config = {
  content: ['./app/**/*.{ts,tsx}', './components/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        ink: { 900: '#16232B', 600: '#4A5B66', 400: '#8A99A3' },
        line: { 200: '#E3E9EC' },
        surface: { 0: '#FFFFFF', 50: '#F6F9F9' },
        primary: { 700: '#0B5654', 600: '#0E6E6B', 100: '#DCEFEE' },
        accent: { 500: '#F2A33C', 100: '#FCF0DC' },
        success: { 600: '#1E8E5A', 100: '#E2F4EA' },
        warning: { 600: '#C97A0A', 100: '#FCF0DC' },
        danger: { 600: '#C94430', 100: '#FBE8E4' },
        info: { 600: '#2E6ECF', 100: '#E6EEFB' },
        tier: {
          gold: '#D9A441', 'gold-bg': '#FBF3E1',
          silver: '#8F9CA8', 'silver-bg': '#EFF2F5',
          bronze: '#B0784A', 'bronze-bg': '#F6ECE3',
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
  plugins: [],
};
export default config;
