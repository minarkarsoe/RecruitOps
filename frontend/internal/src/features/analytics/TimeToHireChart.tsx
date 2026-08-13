import React, { useState } from 'react';
import type { TimeToHireAnalyticsDto } from '@recruitops/types';
import { Card, SkeletonRow } from '@recruitops/ui';

interface TimeToHireChartProps {
  data: TimeToHireAnalyticsDto | null;
  loading: boolean;
}

export const TimeToHireChart: React.FC<TimeToHireChartProps> = ({ data, loading }) => {
  const [activeTab, setActiveTab] = useState<'stages' | 'departments' | 'postings'>('stages');

  if (loading || !data) {
    return (
      <Card title="Time-to-Hire Analytics">
        <div data-testid="time-to-hire-skeleton">
          <div className="h-6 w-48 bg-zinc-200 dark:bg-zinc-800 rounded mb-4 animate-pulse" />
          <SkeletonRow />
          <SkeletonRow />
          <SkeletonRow />
        </div>
      </Card>
    );
  }

  const maxStageDays = Math.max(...data.stageDurations.map((s) => s.avgDays), 1);
  const maxDeptDays = Math.max(...data.departmentBreakdown.map((d) => d.avgDays), 1);
  const maxPostingDays = Math.max(...data.postingBreakdown.map((p) => p.avgDays), 1);

  return (
    <div data-testid="time-to-hire-chart-card">
      <Card title="Time-to-Hire Analytics">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-5 pb-3 border-b border-zinc-100 dark:border-zinc-800">
          <p className="text-xs text-zinc-500 dark:text-zinc-400">
            Average days spent in each pipeline stage and across departments &amp; job postings
          </p>

          <div className="inline-flex p-1 bg-zinc-100 dark:bg-zinc-800 rounded-lg text-xs font-medium">
            <button
              type="button"
              onClick={() => setActiveTab('stages')}
              className={`px-3 py-1 rounded-md transition-colors ${
                activeTab === 'stages'
                  ? 'bg-white dark:bg-zinc-900 text-zinc-900 dark:text-zinc-100 shadow-sm'
                  : 'text-zinc-600 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100'
              }`}
            >
              Pipeline Stages
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('departments')}
              className={`px-3 py-1 rounded-md transition-colors ${
                activeTab === 'departments'
                  ? 'bg-white dark:bg-zinc-900 text-zinc-900 dark:text-zinc-100 shadow-sm'
                  : 'text-zinc-600 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100'
              }`}
            >
              By Department
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('postings')}
              className={`px-3 py-1 rounded-md transition-colors ${
                activeTab === 'postings'
                  ? 'bg-white dark:bg-zinc-900 text-zinc-900 dark:text-zinc-100 shadow-sm'
                  : 'text-zinc-600 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100'
              }`}
            >
              By Job Posting
            </button>
          </div>
        </div>

        {activeTab === 'stages' && (
          <div className="space-y-4" data-testid="tth-stages-view">
            {data.stageDurations.length === 0 ? (
              <div className="text-sm text-zinc-500 text-center py-6">No stage duration data available.</div>
            ) : (
              data.stageDurations.map((item, idx) => {
                const widthPct = Math.min(Math.max((item.avgDays / maxStageDays) * 100, 4), 100);
                return (
                  <div key={idx} className="flex items-center text-xs">
                    <span className="w-28 font-medium text-zinc-700 dark:text-zinc-300 truncate pr-2">
                      {item.stage}
                    </span>
                    <div className="flex-1 bg-zinc-100 dark:bg-zinc-800 rounded-full h-4 overflow-hidden relative mr-3">
                      <div
                        className="bg-teal-500 dark:bg-teal-400 h-full rounded-full transition-all duration-300"
                        style={{ width: `${widthPct}%` }}
                      />
                    </div>
                    <span className="w-16 text-right font-semibold text-zinc-900 dark:text-zinc-100">
                      {item.avgDays.toFixed(1)} days
                    </span>
                  </div>
                );
              })
            )}
          </div>
        )}

        {activeTab === 'departments' && (
          <div className="space-y-4" data-testid="tth-departments-view">
            {data.departmentBreakdown.length === 0 ? (
              <div className="text-sm text-zinc-500 text-center py-6">No department breakdown data available.</div>
            ) : (
              data.departmentBreakdown.map((item, idx) => {
                const widthPct = Math.min(Math.max((item.avgDays / maxDeptDays) * 100, 4), 100);
                return (
                  <div key={idx} className="flex items-center text-xs">
                    <div className="w-36 font-medium text-zinc-700 dark:text-zinc-300 truncate pr-2">
                      {item.departmentName}
                    </div>
                    <div className="flex-1 bg-zinc-100 dark:bg-zinc-800 rounded-full h-4 overflow-hidden relative mr-3">
                      <div
                        className="bg-cyan-500 dark:bg-cyan-400 h-full rounded-full transition-all duration-300"
                        style={{ width: `${widthPct}%` }}
                      />
                    </div>
                    <div className="w-28 text-right flex items-center justify-end gap-2">
                      <span className="font-semibold text-zinc-900 dark:text-zinc-100">
                        {item.avgDays.toFixed(1)} days
                      </span>
                      <span className="px-1.5 py-0.5 rounded text-[10px] bg-zinc-200 dark:bg-zinc-700 text-zinc-700 dark:text-zinc-300">
                        {item.hiredCount} hired
                      </span>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        )}

        {activeTab === 'postings' && (
          <div className="space-y-4" data-testid="tth-postings-view">
            {data.postingBreakdown.length === 0 ? (
              <div className="text-sm text-zinc-500 text-center py-6">No job posting breakdown data available.</div>
            ) : (
              data.postingBreakdown.map((item, idx) => {
                const widthPct = Math.min(Math.max((item.avgDays / maxPostingDays) * 100, 4), 100);
                return (
                  <div key={idx} className="flex items-center text-xs">
                    <div className="w-44 font-medium text-zinc-700 dark:text-zinc-300 truncate pr-2">
                      {item.postingTitle}
                    </div>
                    <div className="flex-1 bg-zinc-100 dark:bg-zinc-800 rounded-full h-4 overflow-hidden relative mr-3">
                      <div
                        className="bg-blue-500 dark:bg-blue-400 h-full rounded-full transition-all duration-300"
                        style={{ width: `${widthPct}%` }}
                      />
                    </div>
                    <div className="w-28 text-right flex items-center justify-end gap-2">
                      <span className="font-semibold text-zinc-900 dark:text-zinc-100">
                        {item.avgDays.toFixed(1)} days
                      </span>
                      <span className="px-1.5 py-0.5 rounded text-[10px] bg-zinc-200 dark:bg-zinc-700 text-zinc-700 dark:text-zinc-300">
                        {item.hiredCount} hired
                      </span>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        )}
      </Card>
    </div>
  );
};
