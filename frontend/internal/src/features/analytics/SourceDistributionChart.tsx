import React from 'react';
import type { SourceOfHireAnalyticsDto } from '@recruitops/types';
import { Card, SkeletonRow } from '@recruitops/ui';

interface SourceDistributionChartProps {
  data: SourceOfHireAnalyticsDto | null;
  loading: boolean;
}

export const SourceDistributionChart: React.FC<SourceDistributionChartProps> = ({ data, loading }) => {
  if (loading || !data) {
    return (
      <Card title="Source of Hire Distribution">
        <div data-testid="source-distribution-skeleton">
          <div className="h-6 w-48 bg-zinc-200 dark:bg-zinc-800 rounded mb-4 animate-pulse" />
          <SkeletonRow />
          <SkeletonRow />
        </div>
      </Card>
    );
  }

  const channelColors: Record<string, string> = {
    Direct: 'bg-teal-500 dark:bg-teal-400',
    Referral: 'bg-emerald-500 dark:bg-emerald-400',
    Facebook: 'bg-blue-600 dark:bg-blue-500',
    LinkedIn: 'bg-sky-600 dark:bg-sky-500',
    Telegram: 'bg-cyan-500 dark:bg-cyan-400',
    ExcelImport: 'bg-amber-500 dark:bg-amber-400',
    Sourced: 'bg-purple-500 dark:bg-purple-400',
    PublicPage: 'bg-indigo-500 dark:bg-indigo-400',
  };

  return (
    <div data-testid="source-distribution-card">
      <Card title="Source of Hire Distribution">
        <p className="text-xs text-zinc-500 dark:text-zinc-400 mb-4 pb-3 border-b border-zinc-100 dark:border-zinc-800">
          Candidate acquisition channels and sourcing channel breakdown
        </p>

        {data.sources.length === 0 ? (
          <div className="text-sm text-zinc-500 text-center py-6">No source distribution data available.</div>
        ) : (
          <div className="space-y-3" data-testid="source-items-list">
            {data.sources.map((item, idx) => {
              const pct = (item.percentage <= 1 && item.percentage > 0)
                ? item.percentage * 100
                : item.percentage;
              const formattedPct = pct.toFixed(1);
              const colorClass = channelColors[item.source] || 'bg-teal-500 dark:bg-teal-400';

              return (
                <div key={idx} className="flex items-center text-xs">
                  <span className="w-28 font-medium text-zinc-700 dark:text-zinc-300 truncate pr-2">
                    {item.source}
                  </span>

                  <div className="flex-1 bg-zinc-100 dark:bg-zinc-800 rounded-full h-4 overflow-hidden relative mr-3">
                    <div
                      className={`${colorClass} h-full rounded-full transition-all duration-300`}
                      style={{ width: `${Math.max(pct, 2)}%` }}
                    />
                  </div>

                  <div className="w-28 text-right flex items-center justify-end gap-2">
                    <span className="font-semibold text-zinc-900 dark:text-zinc-100">
                      {item.count.toLocaleString()}
                    </span>
                    <span className="px-1.5 py-0.5 rounded text-[10px] bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400 font-medium">
                      {formattedPct}%
                    </span>
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
