import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

/**
 * The public app had **no tests at all** until 2026-08-27, which is how
 * `next.config.mjs` shipped a rewrite pointing at `localhost:5080` — unreachable from inside
 * a container, so every browser-side API call 500'd while SSR kept working and the page
 * still looked perfect. Nothing exercised the difference.
 *
 * This app is a stranger's only view of the product, so its floor is the two things that
 * fail silently: where `api()` sends a request, and what the form does when it goes wrong.
 *
 * Mirrors `frontend/internal/vitest.config.ts` deliberately — a second testing idiom in one
 * repo is a thing to keep in sync forever.
 */
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    // No `globals` — imported per file, so `tsc --noEmit` checks the tests with the same
    // config as the app rather than needing an ambient `types` entry.
    setupFiles: ['./test/setup.ts'],
    include: ['{app,lib,test}/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
});
