import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { TenantSwitcherBar } from './TenantSwitcherBar';
import { auth } from '../lib/auth';

describe('TenantSwitcherBar', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('renders null when user is not super-admin', () => {
    auth.set({
      accessToken: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Admin',
      displayName: 'Normal Admin',
      userId: 'usr-admin',
      isSuperAdmin: false,
      permissions: [],
    });

    const { container } = render(<TenantSwitcherBar />);
    expect(container.firstChild).toBeNull();
  });

  it('renders banner when user is super-admin', () => {
    auth.set({
      accessToken: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super Admin',
      userId: 'usr-super',
      isSuperAdmin: true,
      permissions: [],
    });

    render(<TenantSwitcherBar />);
    expect(screen.getByText(/SUPER-ADMIN CONTEXT/i)).toBeInTheDocument();
    expect(screen.getByText('Switch Tenant Context ▾')).toBeInTheDocument();
  });

  it('allows switching tenant context via callback', () => {
    auth.set({
      accessToken: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super Admin',
      userId: 'usr-super',
      isSuperAdmin: true,
      permissions: [],
    });

    const handleTenantChange = vi.fn();
    render(<TenantSwitcherBar onTenantChange={handleTenantChange} />);

    // Click toggle button
    fireEvent.click(screen.getByText('Switch Tenant Context ▾'));

    // Click Acme Corp
    fireEvent.click(screen.getByText('Acme Corporation'));

    expect(handleTenantChange).toHaveBeenCalledWith('tenant-acme', 'Acme Corporation');
    expect(auth.get()?.activeTenantId).toBe('tenant-acme');
  });
});
