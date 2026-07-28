import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

/**
 * Kept separate from `vite.config.ts` so the dev-server proxy config and the test
 * environment cannot drift into each other — the proxy is meaningless here, and a test run
 * that quietly depended on it would be lying about what the component does.
 */
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    // No `globals` — `describe`/`it`/`expect` are imported per file, so `tsc --noEmit`
    // checks the tests with the same config as the app instead of needing an ambient
    // `types` entry that would change what the app itself sees.
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
});
