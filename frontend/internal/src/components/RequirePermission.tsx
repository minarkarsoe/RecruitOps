import type { ReactNode } from 'react';
import { auth, hasPermission } from '../lib/auth';

interface RequirePermissionProps {
  permission: string;
  children: ReactNode;
}

export function RequirePermission({ permission, children }: RequirePermissionProps) {
  const session = auth.get();

  if (!hasPermission(session, permission)) {
    return (
      <div className="mx-auto max-w-md my-12 p-6 bg-white border border-critical-50 rounded-lg shadow-sm text-center">
        <div className="w-12 h-12 rounded-full bg-critical-50 text-critical-700 flex items-center justify-center mx-auto mb-3 font-bold text-xl">
          🚫
        </div>
        <h2 className="text-lg font-bold text-ink-900">403 Access Denied</h2>
        <p className="mt-2 text-sm text-ink-600">
          You do not have the required permission (<code className="font-mono text-xs bg-canvas px-1 py-0.5 rounded text-critical-700">{permission}</code>) to access this view.
        </p>
      </div>
    );
  }

  return <>{children}</>;
}
