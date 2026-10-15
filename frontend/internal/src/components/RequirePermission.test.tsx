import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RequirePermission } from './RequirePermission';
import { auth } from '../lib/auth';

describe('RequirePermission Route Guard Component', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('renders children when user has the required permission', () => {
    auth.set({
      accessToken: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Recruiter',
      displayName: 'Granted User',
      userId: 'usr-1',
      isSuperAdmin: false,
      permissions: ['permission:users:users:read'],
    });

    render(
      <RequirePermission permission="permission:users:users:read">
        <div data-testid="protected-content">Protected Content</div>
      </RequirePermission>
    );

    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
    expect(screen.queryByText('403 Access Denied')).not.toBeInTheDocument();
  });

  it('renders 403 Access Denied when user lacks required permission', () => {
    auth.set({
      accessToken: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Recruiter',
      displayName: 'Denied User',
      userId: 'usr-2',
      isSuperAdmin: false,
      permissions: ['permission:requisitions:requisitions:read'],
    });

    render(
      <RequirePermission permission="permission:users:users:read">
        <div data-testid="protected-content">Protected Content</div>
      </RequirePermission>
    );

    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.getByText('403 Access Denied')).toBeInTheDocument();
    expect(screen.getByText(/permission:users:users:read/)).toBeInTheDocument();
  });

  // ---- Fail-closed regressions -------------------------------------------------
  // Each of these rendered the protected content before the hasPermission() fix.

  it('denies when there is no session', () => {
    render(
      <RequirePermission permission="permission:users:users:read">
        <div data-testid="protected-content">Protected Content</div>
      </RequirePermission>
    );

    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.getByText('403 Access Denied')).toBeInTheDocument();
  });

  it('denies when the session carries no permissions array', () => {
    // The production shape before the API shipped `permissions` — a fully valid session
    // whose permission set is simply absent. This used to render the guarded view.
    sessionStorage.setItem(
      'recruitops.session',
      JSON.stringify({
        accessToken: 'token-123',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Pre-RBAC User',
        userId: 'usr-3',
      })
    );

    render(
      <RequirePermission permission="permission:users:users:read">
        <div data-testid="protected-content">Protected Content</div>
      </RequirePermission>
    );

    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.getByText('403 Access Denied')).toBeInTheDocument();
  });

  it('denies when the permissions array is empty', () => {
    auth.set({
      accessToken: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Recruiter',
      displayName: 'Ungranted User',
      userId: 'usr-4',
      isSuperAdmin: false,
      permissions: [],
    });

    render(
      <RequirePermission permission="permission:users:users:read">
        <div data-testid="protected-content">Protected Content</div>
      </RequirePermission>
    );

    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
    expect(screen.getByText('403 Access Denied')).toBeInTheDocument();
  });
});
