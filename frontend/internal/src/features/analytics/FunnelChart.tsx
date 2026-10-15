import React from 'react';
import type { ConversionFunnelAnalyticsDto } from '@recruitops/types';
import { Card } from '@recruitops/ui';

// Built against the funnel in `design/internal/analytics-pipeline.html`:
// a `[label | track | value]` grid, one hue, the value direct-labelled, no legend — a funnel is
// one series, and the kit's rule is that a single series needs no legend because the title
// already names it.
//
// The drop-off is now text beside the number rather than an amber pill in its own column. It is
// a change to the number on its left, not an independent fact, and putting it in a tinted chip
// two columns away made it look like a status the stage carries.

interface FunnelChartProps {
  data: ConversionFunnelAnalyticsDto | null;
  loading: boolean;
}

export const FunnelChart: React.FC<FunnelChartProps> = ({ data, loading }) => {
  if (loading || !data) {
    return (
      <Card title="Pipeline Conversion Funnel">
        <div className="space-y-3" data-testid="funnel-chart-skeleton">
          <span className="skeleton block h-4 w-48" />
          <span className="skeleton block h-6 w-full" />
          <span className="skeleton block h-6 w-full" />
        </div>
      </Card>
    );
  }

  const maxCount = Math.max(...data.funnel.map((item) => item.count), 1);

  return (
    <div data-testid="funnel-chart-card">
      <Card title="Pipeline Conversion Funnel">
        <p className="mb-4 border-b border-line pb-3 text-sm text-ink-600">
          Candidate volume progression and drop-off rate between recruitment stages
        </p>

        {data.funnel.length === 0 ? (
          <p className="py-6 text-center text-sm text-ink-600">No funnel conversion data available.</p>
        ) : (
          <div
            className="space-y-2.5"
            data-testid="funnel-items-list"
            role="img"
            aria-label="Candidates reaching each pipeline stage, horizontal bar chart"
          >
            {data.funnel.map((item, idx) => {
              const widthPct = Math.min(Math.max((item.count / maxCount) * 100, 6), 100);
              const dropOffPercent = (item.dropOffRate <= 1 && item.dropOffRate > 0)
                ? (item.dropOffRate * 100).toFixed(1)
                : item.dropOffRate.toFixed(1);

              return (
                <div
                  key={idx}
                  className="bar-row grid grid-cols-[120px_1fr_200px] items-center gap-3"
                  title={`${item.stage} — ${item.count.toLocaleString()} candidates`}
                >
                  <span className="truncate text-base text-ink-700">{item.stage}</span>
                  <span className="bar-track block h-6">
                    <span className="bar-fill block" style={{ width: `${widthPct}%` }} />
                  </span>
                  <span className="flex items-baseline justify-end gap-2 text-right">
                    {/* "350 candidates" stays one text node — the suite matches the whole
                        string, and the unit is only worth styling separately if that costs
                        nothing. */}
                    <span className="text-base font-medium tnum">
                      {item.count.toLocaleString()} candidates
                    </span>
                    {idx === 0 ? (
                      <span className="text-sm text-ink-600">Entry Point</span>
                    ) : (
                      <span className="text-sm text-critical-700">-{dropOffPercent}% drop-off</span>
                    )}
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
