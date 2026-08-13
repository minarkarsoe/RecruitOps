import React from 'react';
import { useFeatureFlags } from '../lib/useFeatureFlags';

interface FeatureGateProps {
  flag: string;
  fallback?: React.ReactNode;
  children: React.ReactNode;
}

export const FeatureGate: React.FC<FeatureGateProps> = ({
  flag,
  fallback = null,
  children,
}) => {
  const { isFeatureEnabled, loading } = useFeatureFlags();

  if (loading) {
    return <>{children}</>;
  }

  if (!isFeatureEnabled(flag)) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
};
