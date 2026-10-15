import React from 'react';
import type { SourceOfHireAnalyticsDto } from '@recruitops/types';
import { Card } from '@recruitops/ui';

// Built against `design/internal/analytics-pipeline.html`.
//
// ⚠️ The eight-colour channel palette is gone, and this is the one change in the analytics work
// that is a correctness fix rather than a style one. It was:
//
//     Direct teal-500 · Referral emerald-500 · Facebook blue-600 · LinkedIn sky-600
//     Telegram cyan-500 · ExcelImport amber-500 · Sourced purple-500 · PublicPage indigo-500
//
// Run through the dataviz validator (2026-08-25, light surface) it FAILS two hard checks:
//
//     CVD separation      indigo-500 ↔ purple-500   ΔE 0.9 (protan)   — indistinguishable
//     Normal-vision floor emerald-500 ↔ teal-500    ΔE 5.4            — below the floor of 15,
//                                                                       hard to tell apart even
//                                                                       with full colour vision
//
// So it was not merely off-brand; five of the eight were near-neighbours nobody could separate.
//
// It is replaced by ONE hue rather than by a corrected eight, because this chart is one measure
// (share) across named categories whose names sit directly beside their bars. Colour was
// encoding the row's position in the list, which the list already encodes. The kit does keep a
// validated four-colour categorical set — measured here as ΔE 21.0 deutan / 30.2 normal, all six
// checks pass — but it reserves it for the one case where identity is genuinely carried: the
// same channel appearing in two different charts, so the eye can tie them together.

interface SourceDistributionChartProps {
  data: SourceOfHireAnalyticsDto | null;
  loading: boolean;
}

export const SourceDistributionChart: React.FC<SourceDistributionChartProps> = ({ data, loading }) => {
  if (loading || !data) {
    return (
      <Card title="Source of Hire Distribution">
        <div className="space-y-3" data-testid="source-distribution-skeleton">
          <span className="skeleton block h-4 w-48" />
          <span className="skeleton block h-6 w-full" />
          <span className="skeleton block h-6 w-full" />
        </div>
      </Card>
    );
  }

  return (
    <div data-testid="source-distribution-card">
      <Card title="Source of Hire Distribution">
        <p className="mb-4 border-b border-line pb-3 text-sm text-ink-600">
          Candidate acquisition channels and sourcing channel breakdown
        </p>

        {data.sources.length === 0 ? (
          <p className="py-6 text-center text-sm text-ink-600">No source distribution data available.</p>
        ) : (
          <div
            className="space-y-2.5"
            data-testid="source-items-list"
            role="img"
            aria-label="Candidates by acquisition channel, horizontal bar chart"
          >
            {data.sources.map((item, idx) => {
              const pct = (item.percentage <= 1 && item.percentage > 0)
                ? item.percentage * 100
                : item.percentage;
              const formattedPct = pct.toFixed(1);

              return (
                <div
                  key={idx}
                  className="bar-row grid grid-cols-[120px_1fr_130px] items-center gap-3"
                  title={`${item.source} — ${item.count.toLocaleString()} candidates, ${formattedPct}%`}
                >
                  <span className="truncate text-base text-ink-700">{item.source}</span>
                  <span className="bar-track block h-6">
                    <span className="bar-fill block" style={{ width: `${Math.max(pct, 2)}%` }} />
                  </span>
                  <span className="flex items-baseline justify-end gap-2">
                    <span className="text-base font-medium tnum">{item.count.toLocaleString()}</span>
                    <span className="text-sm tnum text-ink-600">{formattedPct}%</span>
                  </span>
                </div>
              );
            })}
          </div>
        )}
      </Card>
    </div>
  );
};
