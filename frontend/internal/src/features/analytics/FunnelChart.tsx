import React from 'react';
import type { ConversionFunnelAnalyticsDto } from '@recruitops/types';
import { Card, SkeletonRow } from '@recruitops/ui';

interface FunnelChartProps {
  data: ConversionFunnelAnalyticsDto | null;
  loading: boolean;
}

export const FunnelChart: React.FC<FunnelChartProps> = ({ data, loading }) => {
  if (loading || !data) {
    return (
      <Card title="Pipeline Conversion Funnel">
        <div data-testid="funnel-chart-skeleton">
          <div className="h-6 w-48 bg-zinc-200 dark:bg-zinc-800 rounded mb-4 animate-pulse" />
          <SkeletonRow />
          <SkeletonRow />
        </div>
      </Card>
    );
  }

  const maxCount = Math.max(...data.funnel.map((item) => item.count), 1);

  return (
    <div data-testid="funnel-chart-card">
      <Card title="Pipeline Conversion Funnel">
        <p className="text-xs text-zinc-500 dark:text-zinc-400 mb-4 pb-3 border-b border-zinc-100 dark:border-zinc-800">
          Candidate volume progression and drop-off rate between recruitment stages
        </p>

        {data.funnel.length === 0 ? (
          <div className="text-sm text-zinc-500 text-center py-6">No funnel conversion data available.</div>
        ) : (
          <div className="space-y-3" data-testid="funnel-items-list">
            {data.funnel.map((item, idx) => {
              const widthPct = Math.min(Math.max((item.count / maxCount) * 100, 6), 100);
              const dropOffPercent = (item.dropOffRate <= 1 && item.dropOffRate > 0)
                ? (item.dropOffRate * 100).toFixed(1)
                : item.dropOffRate.toFixed(1);

              return (
                <div key={idx} className="flex flex-col sm:flex-row sm:items-center text-xs gap-1 sm:gap-3">
                  <div className="w-28 font-medium text-zinc-800 dark:text-zinc-200">
                    {item.stage}
                  </div>

                  <div className="flex-1 bg-zinc-100 dark:bg-zinc-800 rounded-lg h-6 overflow-hidden relative flex items-center px-2">
                    <div
                      className="bg-indigo-500 dark:bg-indigo-400 h-full absolute left-0 top-0 rounded-lg transition-all duration-300 opacity-85"
                      style={{ width: `${widthPct}%` }}
                    />
                    <span className="relative z-10 font-bold text-zinc-900 dark:text-zinc-100 text-xs ml-1">
                      {item.count.toLocaleString()} candidates
                    </span>
                  </div>

                  <div className="w-24 text-right flex items-center justify-end">
                    {idx === 0 ? (
                      <span className="text-[10px] px-2 py-0.5 rounded font-medium bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300">
                        Entry Point
                      </span>
                    ) : (
                      <span className="text-[10px] px-2 py-0.5 rounded font-medium bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300">
                        -{dropOffPercent}% drop-off
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Card>
    </div>
  );
};
