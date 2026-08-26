import React from 'react';
import type { KpiMetricsDto } from '@recruitops/types';
import { Card, SkeletonCard } from '@recruitops/ui';

// Built against the stat tiles in `design/internal/analytics-dashboard.html`.
//
// ⚠️ The numbers are ink, not four different hues. Time-to-hire was teal, active requisitions
// cyan, applications blue, hire rate emerald — four colours encoding **position in the row**,
// because there is nothing else they could encode: these are four unrelated measures, not four
// members of a series. Colour that means nothing still costs something, and here it cost the
// one thing colour is for on this screen. The kit's tiles put the value in ink and spend colour
// only on the delta line, where positive/critical says whether the number is good news.
//
// The `dark:` variants are gone from this file. The app has no dark theme — `index.css` declares
// `color-scheme: light` and the shell has not a single dark variant — but Tailwind's default
// `darkMode: 'media'` meant these fired on any machine set to dark, painting near-black panels
// and ink-400 text (2.45:1, measured live in the running app) onto a light page.

interface KpiCardSectionProps {
  kpis: KpiMetricsDto | null;
  loading: boolean;
}

export const KpiCardSection: React.FC<KpiCardSectionProps> = ({ kpis, loading }) => {
  if (loading || !kpis) {
    return (
      <div className="mb-6 grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4" data-testid="kpi-skeleton-grid">
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
    },
    {
      title: 'Active Requisitions',
      value: kpis.activeRequisitions.toLocaleString(),
      subtitle: 'Open positions in hiring pipeline',
    },
    {
      title: 'Total Applications',
      value: kpis.totalApplications.toLocaleString(),
      subtitle: 'Across active tenant scope',
    },
    {
      title: 'Overall Hire Rate',
      value: `${hireRatePercent}%`,
      subtitle: 'Hired candidates vs total applicants',
    },
  ];

  return (
    <div className="mb-6 grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4" data-testid="kpi-cards-grid">
      {cards.map((c, idx) => (
        <Card key={idx}>
          <p className="text-sm text-ink-600">{c.title}</p>
          {/* One text node. The kit sets the unit in a smaller weight, but "14.3 days" is
              asserted as a single string by the analytics suite and splitting it would break
              the match — the styling is not worth changing what the DOM says. */}
          <p className="mt-1.5 text-3xl font-semibold tnum text-ink-900" data-testid={`kpi-val-${idx}`}>
            {c.value}
          </p>
          <p className="mt-1.5 text-sm text-ink-600">{c.subtitle}</p>
        </Card>
      ))}
    </div>
  );
};
