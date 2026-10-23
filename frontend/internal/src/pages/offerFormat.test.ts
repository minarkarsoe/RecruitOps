import { describe, expect, it } from 'vitest';
import { calendarDate, remindedRecently } from './offerFormat';

describe('remindedRecently', () => {
  const now = Date.parse('2026-10-23T12:00:00Z');

  it('is true for less than a day, false from a day on', () => {
    expect(remindedRecently(null, now)).toBe(false);
    expect(remindedRecently('2026-10-23T00:00:00Z', now)).toBe(true);
    expect(remindedRecently('2026-10-22T12:00:01Z', now)).toBe(true);
    expect(remindedRecently('2026-10-22T12:00:00Z', now)).toBe(false);
  });
});

describe('calendarDate', () => {
  it('reads a YYYY-MM-DD as that day, whatever the browser zone', () => {
    expect(calendarDate('2026-11-01')).toMatch(/1/);
    expect(calendarDate('2026-11-01')).toMatch(/2026/);
    expect(calendarDate('2026-11-01', false)).not.toMatch(/2026/);
  });
});
