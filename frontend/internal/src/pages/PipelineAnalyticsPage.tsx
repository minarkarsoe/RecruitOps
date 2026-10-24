import { Button } from '@recruitops/ui';
import {
  usePipelineAnalytics,
  TimeToHireChart,
  FunnelChart,
  SourceDistributionChart,
} from '../features/analytics';

/**
 * Module 5.2 — Pipeline & source (`/analytics/pipeline`), the second of the three analytics screens
 * the design draws (`design/internal/analytics-pipeline.html`). Its August content, moved here
 * unchanged from the old combined page by ADR-0031 §5 — the design's own layout (hires by source,
 * the recruiter leaderboard) is not built yet, and neither are its per-role rules.
 */
export function PipelineAnalyticsPage() {
  const { timeToHire, conversion, sourceOfHire, loading, error, refreshDashboard } = usePipelineAnalytics();

  return (
    <div className="space-y-6" data-testid="analytics-page-container">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Pipeline &amp; source analytics</h1>
          <p className="mt-1 text-sm text-ink-600">
            Time in each pipeline stage, where candidates drop off, and where they come from.
          </p>
        </div>

        <Button
          type="button"
          variant="secondary"
          onClick={refreshDashboard}
          disabled={loading}
          data-testid="refresh-dashboard-btn"
        >
          {loading ? 'Refreshing...' : 'Refresh Data'}
        </Button>
      </div>

      {error && (
        <div role="alert" className="rounded-lg border border-critical-100 bg-critical-50 p-4 text-sm text-critical-700">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <TimeToHireChart data={timeToHire} loading={loading} />
        <FunnelChart data={conversion} loading={loading} />
      </div>

      <SourceDistributionChart data={sourceOfHire} loading={loading} />
    </div>
  );
}

export default PipelineAnalyticsPage;
