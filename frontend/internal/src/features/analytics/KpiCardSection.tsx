import React from 'react';
import type { KpiMetricsDto } from '@recruitops/types';
import { Card, SkeletonCard } from '@recruitops/ui';

interface KpiCardSectionProps {
  kpis: KpiMetricsDto | null;
  loading: boolean;
}

export const KpiCardSection: React.FC<KpiCardSectionProps> = ({ kpis, loading }) => {
  if (loading || !kpis) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6" data-testid="kpi-skeleton-grid">
        <SkeletonCard />
        <SkeletonCard />
        <SkeletonCard />
        <SkeletonCard />
      </div>
    );
  }

  const hireRatePercent = (kpis.overallHireRate <= 1 && kpis.overallHireRate > 0)
    ? (kpis.overallHireRate * 100).toFixed(1)
    : kpis.overallHireRate.toFixed(1);

  const cards = [
    {
      title: 'Average Time-to-Hire',
      value: `${kpis.avgTimeToHireDays.toFixed(1)} days`,
      subtitle: 'From application to offer acceptance',
      colorClass: 'text-teal-600 dark:text-teal-400',
      badge: 'Speed Metric',
    },
    {
      title: 'Active Requisitions',
      value: kpis.activeRequisitions.toLocaleString(),
      subtitle: 'Open positions in hiring pipeline',
      colorClass: 'text-cyan-600 dark:text-cyan-400',
      badge: 'Demand',
    },
    {
      title: 'Total Applications',
      value: kpis.totalApplications.toLocaleString(),
      subtitle: 'Across active tenant scope',
      colorClass: 'text-blue-600 dark:text-blue-400',
      badge: 'Volume',
    },
    {
      title: 'Overall Hire Rate',
      value: `${hireRatePercent}%`,
      subtitle: 'Hired candidates vs total applicants',
      colorClass: 'text-emerald-600 dark:text-emerald-400',
      badge: 'Conversion',
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6" data-testid="kpi-cards-grid">
      {cards.map((c, idx) => (
        <Card key={idx}>
          <div className="flex flex-col justify-between h-full">
            <div>
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                  {c.title}
                </span>
                <span className="text-[10px] px-2 py-0.5 rounded-full font-medium bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-300">
                  {c.badge}
                </span>
              </div>
              <div className={`text-2xl font-bold ${c.colorClass} mt-2`} data-testid={`kpi-val-${idx}`}>
                {c.value}
              </div>
            </div>
            <div className="mt-3 text-xs text-zinc-500 dark:text-zinc-400 border-t border-zinc-100 dark:border-zinc-800 pt-2">
              {c.subtitle}
            </div>
          </div>
        </Card>
      ))}
    </div>
  );
};
