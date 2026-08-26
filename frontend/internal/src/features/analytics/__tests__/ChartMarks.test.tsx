import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type {
  ConversionFunnelAnalyticsDto,
  SourceOfHireAnalyticsDto,
  TimeToHireAnalyticsDto,
} from '@recruitops/types';
import { FunnelChart } from '../FunnelChart';
import { SourceDistributionChart } from '../SourceDistributionChart';
import { TimeToHireChart } from '../TimeToHireChart';

// These tests guard the two chart decisions that are easy to undo by accident, because both look
// like improvements when you make them: giving each row its own colour, and giving each tab its
// own colour. Both were in this code and both were wrong — measured, not judged:
//
//   the eight channel colours   indigo-500 ↔ purple-500  ΔE 0.9 (protan)  — indistinguishable
//                               emerald-500 ↔ teal-500   ΔE 5.4 (normal)  — below the floor of 15
//
// A bar in this product is single-series magnitude. Colour per row encodes the row's position in
// the list, which the list already encodes, and it spends the one channel that could have carried
// something. `.bar-fill` is the single hue; if a chart wants a different colour it must say why.

const funnel: ConversionFunnelAnalyticsDto = {
  funnel: [
    { stage: 'Applied', count: 400, dropOffRate: 0 },
    { stage: 'Screening', count: 180, dropOffRate: 0.55 },
    { stage: 'Interview', count: 60, dropOffRate: 0.667 },
    { stage: 'Offer', count: 20, dropOffRate: 0.667 },
  ],
} as ConversionFunnelAnalyticsDto;

const sources: SourceOfHireAnalyticsDto = {
  sources: [
    { source: 'LinkedIn', count: 120, percentage: 0.4 },
    { source: 'Referral', count: 90, percentage: 0.3 },
    { source: 'Facebook', count: 60, percentage: 0.2 },
    { source: 'Telegram', count: 30, percentage: 0.1 },
  ],
} as SourceOfHireAnalyticsDto;

const timeToHire: TimeToHireAnalyticsDto = {
  stageDurations: [
    { stage: 'Screening', avgDays: 4.2 },
    { stage: 'Interview', avgDays: 9.6 },
  ],
  departmentBreakdown: [{ departmentName: 'Credit Risk', avgDays: 31.4, hiredCount: 3 }],
  postingBreakdown: [{ postingTitle: 'Senior Credit Analyst', avgDays: 28.1, hiredCount: 2 }],
} as TimeToHireAnalyticsDto;

/**
 * Every bar mark's full colour signature — utility classes AND the inline `background`.
 *
 * ⚠️ This deliberately includes the inline style, and the first version of these tests did not.
 * A mutation that painted row 0 `#7C3AED` via `style` passed all six tests, because the classes
 * were still identical. That is precisely the shape a per-row colour takes when it comes back —
 * it is how the kit's own categorical chart sets its hues — so a class-only check guards the one
 * route nobody would use.
 */
function fillSignatures(container: HTMLElement): string[] {
  return [...container.querySelectorAll('.bar-fill')].map((el) => {
    const style = (el as HTMLElement).style;
    return `${el.className.trim()}|${style.background}|${style.backgroundColor}`;
  });
}

describe('chart marks are one hue', () => {
  it('the funnel draws every stage with the same fill', () => {
    const { container } = render(<FunnelChart data={funnel} loading={false} />);

    const fills = fillSignatures(container);
    expect(fills).toHaveLength(4);
    expect(new Set(fills).size).toBe(1);
    // And no inline colour at all — not merely a consistent one.
    container.querySelectorAll('.bar-fill').forEach((el) => {
      expect((el as HTMLElement).style.background).toBe('');
      expect((el as HTMLElement).style.backgroundColor).toBe('');
    });
  });

  it('the source chart draws every channel with the same fill', () => {
    const { container } = render(<SourceDistributionChart data={sources} loading={false} />);

    const fills = fillSignatures(container);
    expect(fills).toHaveLength(4);
    expect(new Set(fills).size).toBe(1);
    container.querySelectorAll('.bar-fill').forEach((el) => {
      expect((el as HTMLElement).style.background).toBe('');
      expect((el as HTMLElement).style.backgroundColor).toBe('');
    });
  });

  it('time-to-hire keeps one fill across all three breakdowns', async () => {
    const user = userEvent.setup();
    const { container } = render(<TimeToHireChart data={timeToHire} loading={false} />);

    const seen = new Set<string>();
    fillSignatures(container).forEach((c) => seen.add(c));

    await user.click(screen.getByRole('button', { name: 'By Department' }));
    fillSignatures(container).forEach((c) => seen.add(c));

    await user.click(screen.getByRole('button', { name: 'By Job Posting' }));
    fillSignatures(container).forEach((c) => seen.add(c));

    // Three tabs, one measure — average days. The tab already says which breakdown you are
    // looking at, so a hue per tab encodes the tab twice and the measure zero times.
    expect(seen.size).toBe(1);
  });

  it('the selected breakdown is announced, not merely coloured', async () => {
    const user = userEvent.setup();
    render(<TimeToHireChart data={timeToHire} loading={false} />);

    expect(screen.getByRole('button', { name: 'Pipeline Stages' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'By Department' })).toHaveAttribute('aria-pressed', 'false');

    await user.click(screen.getByRole('button', { name: 'By Department' }));

    expect(screen.getByRole('button', { name: 'Pipeline Stages' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'By Department' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('every chart is reachable as an image with a label, not an unnamed pile of divs', () => {
    const { container: a } = render(<FunnelChart data={funnel} loading={false} />);
    const { container: b } = render(<SourceDistributionChart data={sources} loading={false} />);

    for (const container of [a, b]) {
      const img = container.querySelector('[role="img"]');
      expect(img).not.toBeNull();
      expect(img?.getAttribute('aria-label')).toMatch(/bar chart/i);
    }
  });
});

describe('the app has no dark theme, so charts must not carry dark variants', () => {
  // `darkMode` is `'class'` in the preset now, so a stray `dark:` is inert — but inert classes in
  // the markup are a standing invitation to "finish" a dark theme one component at a time, which
  // is how 97 of them accumulated in this folder while the rest of the app had none.
  it('renders no dark: utilities in any chart', () => {
    const { container: a } = render(<FunnelChart data={funnel} loading={false} />);
    const { container: b } = render(<SourceDistributionChart data={sources} loading={false} />);
    const { container: c } = render(<TimeToHireChart data={timeToHire} loading={false} />);

    for (const container of [a, b, c]) {
      expect(container.querySelectorAll('[class*="dark:"]')).toHaveLength(0);
    }
  });
});
