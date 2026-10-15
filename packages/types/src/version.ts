export interface VersionInfo {
  version: string;
  informationalVersion: string;
  environment: string;
  deploymentTier: string;
  timestamp: string;
  featureFlags: Record<string, boolean>;
}
