import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { auth } from '../lib/auth';

describe('AppLayout Permission-Aware Navigation', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('renders all menu items for SuperAdmin', () => {
    auth.set({
      accessToken: 'token-super',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super User',
      userId: 'usr-super',
      isSuperAdmin: true,
      permissions: [],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.getByText('Requisitions')).toBeInTheDocument();
    expect(screen.getByText('Job postings')).toBeInTheDocument();
    expect(screen.getByText('Inbox')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Role Builder')).toBeInTheDocument();
  });

  it('filters menu items based on custom role granular permissions', () => {
    auth.set({
      accessToken: 'token-custom',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Recruiter',
      displayName: 'Custom Recruiter',
      userId: 'usr-custom',
      isSuperAdmin: false,
      permissions: ['permission:requisitions:requisitions:read'],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    // Should render Requisitions and JD templates (both check permission:requisitions:requisitions:read)
    expect(screen.getByText('Requisitions')).toBeInTheDocument();
    expect(screen.getByText('JD templates')).toBeInTheDocument();

    // Should hide links for ungranted permissions
    expect(screen.queryByText('Job postings')).not.toBeInTheDocument();
    expect(screen.queryByText('Inbox')).not.toBeInTheDocument();
    expect(screen.queryByText('Users')).not.toBeInTheDocument();
    expect(screen.queryByText('Role Builder')).not.toBeInTheDocument();
  });

  it('hides every gated link when the session carries no permissions array', () => {
    // Regression: this is the shape the API actually returned before `permissions` was added
    // to LoginResponse. `hasPermission()` read the missing array as "unknown, allow", so a
    // plain Recruiter was served the full admin sidebar — Users and Role Builder included.
    sessionStorage.setItem(
      'recruitops.session',
      JSON.stringify({
        accessToken: 'token-pre-rbac',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Pre-RBAC Recruiter',
        userId: 'usr-pre-rbac',
      })
    );

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.queryByText('Users')).not.toBeInTheDocument();
    expect(screen.queryByText('Role Builder')).not.toBeInTheDocument();
    expect(screen.queryByText('Requisitions')).not.toBeInTheDocument();
    expect(screen.queryByText('Job postings')).not.toBeInTheDocument();
  });

  it('renders Users and Role Builder when appropriate permissions are assigned', () => {
    auth.set({
      accessToken: 'token-custom-admin',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'CustomRole',
      displayName: 'User Admin Custom',
      userId: 'usr-user-admin',
      isSuperAdmin: false,
      permissions: ['permission:users:users:read', 'permission:roles:roles:read'],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Role Builder')).toBeInTheDocument();
    expect(screen.queryByText('Requisitions')).not.toBeInTheDocument();
    expect(screen.queryByText('Job postings')).not.toBeInTheDocument();
  });
});
