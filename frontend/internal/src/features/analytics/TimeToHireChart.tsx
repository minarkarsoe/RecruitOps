import React, { useState } from 'react';
import type { TimeToHireAnalyticsDto } from '@recruitops/types';
import { Card } from '@recruitops/ui';

// Built against `design/internal/analytics-dashboard.html`.
//
// ⚠️ All three tabs draw the SAME hue now. They used to be teal for stages, cyan for departments
// and blue for postings — so the colour encoded which tab you were on, which the tab already
// encodes, while implying the three views measure different things. They measure exactly one
// thing: average days. Two of those three hues were also 5.4 ΔE apart, which is below the point
// where full-colour vision can separate them, so the distinction was not even legible.
//
// The tab strip is the kit's detented filter group — one bordered track, active segment filled
// with the ink-900 token. It replaces a grey pill (the old zinc-100 alias) holding a white
// "raised" tab, which is the opposite polarity to every other selected control in the app.
//
// ⚠️ Both token names in that sentence used to be written as literal utility class strings.
// Tailwind's content scanner is a regex over the file, not a parser — it cannot tell a comment
// from JSX — so it kept emitting a background rule for the old grey step that no element has
// ever carried. That single phantom was the entire remaining "usage" blocking the preset's
// compat block, and it survived one attempt to remove it because the removal comment quoted
// the class name again. Describe tokens in prose; never spell a live utility class in one.

type Tab = 'stages' | 'departments' | 'postings';

interface TimeToHireChartProps {
  data: TimeToHireAnalyticsDto | null;
  loading: boolean;
}

/** One row of the chart: name, bar, value. The three views differ only in how wide the name
 *  column needs to be and whether a hired count rides along, so they share this. */
function BarRow({
  label,
  labelWidth,
  widthPct,
  value,
  meta,
  title,
}: {
  label: string;
  labelWidth: string;
  widthPct: number;
  value: string;
  meta?: string;
  title: string;
}) {
  return (
    <div
      className="bar-row grid items-center gap-3"
      style={{ gridTemplateColumns: `${labelWidth} 1fr 130px` }}
      title={title}
    >
      <span className="truncate text-base text-ink-700">{label}</span>
      <span className="bar-track block h-6">
        <span className="bar-fill block" style={{ width: `${widthPct}%` }} />
      </span>
      <span className="flex items-baseline justify-end gap-2">
        <span className="text-base font-medium tnum">{value}</span>
        {meta && <span className="text-sm tnum text-ink-600">{meta}</span>}
      </span>
    </div>
  );
}

export const TimeToHireChart: React.FC<TimeToHireChartProps> = ({ data, loading }) => {
  const [activeTab, setActiveTab] = useState<Tab>('stages');

  if (loading || !data) {
    return (
      <Card title="Time-to-Hire Analytics">
        <div className="space-y-3" data-testid="time-to-hire-skeleton">
          <span className="skeleton block h-4 w-48" />
          <span className="skeleton block h-6 w-full" />
          <span className="skeleton block h-6 w-full" />
          <span className="skeleton block h-6 w-full" />
        </div>
      </Card>
    );
  }

  const maxStageDays = Math.max(...data.stageDurations.map((s) => s.avgDays), 1);
  const maxDeptDays = Math.max(...data.departmentBreakdown.map((d) => d.avgDays), 1);
  const maxPostingDays = Math.max(...data.postingBreakdown.map((p) => p.avgDays), 1);

  const tabs: { value: Tab; label: string }[] = [
    { value: 'stages', label: 'Pipeline Stages' },
    { value: 'departments', label: 'By Department' },
    { value: 'postings', label: 'By Job Posting' },
  ];

  const width = (days: number, max: number) => Math.min(Math.max((days / max) * 100, 4), 100);

  return (
    <div data-testid="time-to-hire-chart-card">
      <Card title="Time-to-Hire Analytics">
        <div className="mb-5 flex flex-col justify-between gap-3 border-b border-line pb-3 sm:flex-row sm:items-center">
          <p className="text-sm text-ink-600">
            Average days spent in each pipeline stage and across departments &amp; job postings
          </p>

          <div
            className="flex shrink-0 items-center rounded-md border border-line p-0.5"
            role="group"
            aria-label="Time-to-hire breakdown"
          >
            {tabs.map((tab) => (
              <button
                key={tab.value}
                type="button"
                aria-pressed={activeTab === tab.value}
                onClick={() => setActiveTab(tab.value)}
                className={`h-7 rounded px-2.5 text-sm transition-colors ${
                  activeTab === tab.value
                    ? 'bg-ink-900 font-medium text-white'
                    : 'text-ink-600 hover:text-ink-900'
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>
        </div>

        {activeTab === 'stages' && (
          <div className="space-y-2.5" data-testid="tth-stages-view">
            {data.stageDurations.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-600">No stage duration data available.</p>
            ) : (
              data.stageDurations.map((item, idx) => (
                <BarRow
                  key={idx}
                  label={item.stage}
                  labelWidth="120px"
                  widthPct={width(item.avgDays, maxStageDays)}
                  value={`${item.avgDays.toFixed(1)} days`}
                  title={`${item.stage} — ${item.avgDays.toFixed(1)} days on average`}
                />
              ))
            )}
          </div>
        )}

        {activeTab === 'departments' && (
          <div className="space-y-2.5" data-testid="tth-departments-view">
            {data.departmentBreakdown.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-600">No department breakdown data available.</p>
            ) : (
              data.departmentBreakdown.map((item, idx) => (
                <BarRow
                  key={idx}
                  label={item.departmentName}
                  labelWidth="160px"
                  widthPct={width(item.avgDays, maxDeptDays)}
                  value={`${item.avgDays.toFixed(1)} days`}
                  meta={`${item.hiredCount} hired`}
                  title={`${item.departmentName} — ${item.avgDays.toFixed(1)} days, ${item.hiredCount} hired`}
                />
              ))
            )}
          </div>
        )}

        {activeTab === 'postings' && (
          <div className="space-y-2.5" data-testid="tth-postings-view">
            {data.postingBreakdown.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-600">No job posting breakdown data available.</p>
            ) : (
              data.postingBreakdown.map((item, idx) => (
                <BarRow
                  key={idx}
                  label={item.postingTitle}
                  labelWidth="200px"
                  widthPct={width(item.avgDays, maxPostingDays)}
                  value={`${item.avgDays.toFixed(1)} days`}
                  meta={`${item.hiredCount} hired`}
                  title={`${item.postingTitle} — ${item.avgDays.toFixed(1)} days, ${item.hiredCount} hired`}
                />
              ))
            )}
          </div>
        )}
      </Card>
    </div>
  );
};
