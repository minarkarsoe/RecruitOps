import { useState, useEffect } from 'react';
import { apiFetch } from './api';
import type { VersionInfo } from '@recruitops/types';

let cachedVersionInfo: VersionInfo | null = null;
let fetchPromise: Promise<VersionInfo | null> | null = null;

export async function getVersionInfo(): Promise<VersionInfo | null> {
  if (cachedVersionInfo) return cachedVersionInfo;
  if (!fetchPromise) {
    fetchPromise = apiFetch<VersionInfo>('/version')
      .then((data: VersionInfo) => {
        cachedVersionInfo = data;
        return data;
      })
      .catch(() => null);
  }
  return fetchPromise;
}

export function isFeatureFlagEnabled(flagName: string): boolean {
  if (!cachedVersionInfo || !cachedVersionInfo.featureFlags) return true; // default enabled
  const value = cachedVersionInfo.featureFlags[flagName];
  return value !== undefined ? value : true;
}

export function useFeatureFlags() {
  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(cachedVersionInfo);
  const [loading, setLoading] = useState<boolean>(!cachedVersionInfo);

  useEffect(() => {
    let isMounted = true;
    if (!cachedVersionInfo) {
      getVersionInfo().then((info) => {
        if (isMounted) {
          setVersionInfo(info);
          setLoading(false);
        }
      });
    } else {
      setLoading(false);
    }
    return () => {
      isMounted = false;
    };
  }, []);

  const isFeatureEnabled = (flagName: string): boolean => {
    if (!versionInfo || !versionInfo.featureFlags) return true;
    const value = versionInfo.featureFlags[flagName];
    return value !== undefined ? value : true;
  };

  return {
    versionInfo,
    loading,
    isFeatureEnabled,
  };
}
