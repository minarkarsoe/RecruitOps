import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { auth } from '../lib/auth';

/**
 * Client-side route guard. This is a UX affordance, NOT a security boundary —
 * every endpoint is independently authorised on the server (fallback policy +
 * department scoping). Never rely on this to protect data.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const location = useLocation();
  const session = auth.get();

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }
  return <>{children}</>;
}
