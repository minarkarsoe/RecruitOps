/** Formatting and rules shared by the offer dashboard and the offer page (Module 4). */

export function money(n: number): string {
  return n.toLocaleString('en-US');
}

/** `21 Oct` for a timestamp. */
export function day(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
}

/** A `YYYY-MM-DD` date, read as a calendar date and never shifted by the browser's zone. */
export function calendarDate(date: string, withYear = true): string {
  return new Date(`${date}T00:00:00Z`).toLocaleDateString(undefined, {
    day: 'numeric', month: 'short', ...(withYear ? { year: 'numeric' } : {}), timeZone: 'UTC',
  });
}

const DAY_MS = 24 * 60 * 60 * 1000;

/** A reminder went out less than a day ago — the API refuses another until then (ADR-0030 §5a). */
export function remindedRecently(lastRemindedAt: string | null, now = Date.now()): boolean {
  return lastRemindedAt !== null && now - Date.parse(lastRemindedAt) < DAY_MS;
}
