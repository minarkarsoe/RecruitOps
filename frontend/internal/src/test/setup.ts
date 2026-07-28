import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

// jsdom keeps one document for the whole file, so an un-unmounted tree from the previous
// test is still queryable in the next one — which turns a broken assertion into a passing
// one for the wrong reason.
afterEach(() => {
  cleanup();
  sessionStorage.clear();
});
