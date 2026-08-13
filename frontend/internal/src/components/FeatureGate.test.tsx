import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FeatureGate } from './FeatureGate';
import * as useFeatureFlagsModule from '../lib/useFeatureFlags';

describe('FeatureGate Component', () => {
  it('renders children when feature flag is enabled', () => {
    vi.spyOn(useFeatureFlagsModule, 'useFeatureFlags').mockReturnValue({
      versionInfo: null,
      loading: false,
      isFeatureEnabled: () => true,
    });

    render(
      <FeatureGate flag="EnableAnalytics">
        <div data-testid="gated-content">Analytics Content</div>
      </FeatureGate>
    );

    expect(screen.getByTestId('gated-content')).toBeInTheDocument();
  });

  it('renders fallback when feature flag is disabled', () => {
    vi.spyOn(useFeatureFlagsModule, 'useFeatureFlags').mockReturnValue({
      versionInfo: null,
      loading: false,
      isFeatureEnabled: (flag) => flag !== 'EnableAnalytics',
    });

    render(
      <FeatureGate
        flag="EnableAnalytics"
        fallback={<div data-testid="fallback-notice">Feature Disabled</div>}
      >
        <div data-testid="gated-content">Analytics Content</div>
      </FeatureGate>
    );

    expect(screen.queryByTestId('gated-content')).not.toBeInTheDocument();
    expect(screen.getByTestId('fallback-notice')).toBeInTheDocument();
  });
});
