import { describe, it, expect } from 'vitest';
import { StatusPill } from '../components/ui/StatusPill';

describe('StatusPill', () => {
  it('is a function component', () => {
    // TODO: render with @testing-library/react and assert label + tint.
    expect(typeof StatusPill).toBe('function');
  });
});
