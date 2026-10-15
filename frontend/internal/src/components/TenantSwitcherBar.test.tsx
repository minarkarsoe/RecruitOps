import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TenantSwitcherBar } from './TenantSwitcherBar';
import { auth } from '../lib/auth';
import { api } from '../lib/api';

vi.mock('../lib/api', () => ({ api: vi.fn() }));

const apiMock = api as unknown as ReturnType<typeof vi.fn>;

const ALPHA = '11111111-1111-1111-1111-111111111111';
const BRAVO = '22222222-2222-2222-2222-222222222222';

const TENANTS = [
  { id: ALPHA, name: 'Alpha Company', code: 'alpha', isActive: true },
  { id: BRAVO, name: 'Bravo Company', code: 'bravo', isActive: true },
];

function signIn(overrides: Partial<Parameters<typeof auth.set>[0]> = {}) {
  auth.set({
    accessToken: 'token-123',
    expiresAtUtc: '2099-01-01T00:00:00Z',
    role: 'SuperAdmin',
    displayName: 'Super Admin',
    userId: 'usr-super',
    isSuperAdmin: true,
    tenantId: ALPHA,
    permissions: [],
    ...overrides,
  } as Parameters<typeof auth.set>[0]);
}

describe('TenantSwitcherBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    apiMock.mockResolvedValue(TENANTS);
  });

  it('renders nothing for a user who is not a super-admin', () => {
    signIn({ role: 'Admin', isSuperAdmin: false, displayName: 'Normal Admin', userId: 'usr-admin' });

    const { container } = render(<TenantSwitcherBar />);

    expect(container.firstChild).toBeNull();
  });

  it('renders the banner for a super-admin', () => {
    signIn();

    render(<TenantSwitcherBar />);

    expect(screen.getByText('Super admin')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Switch company' })).toBeInTheDocument();
  });

  it('does not ask the server for companies until the menu is opened', () => {
    signIn();

    render(<TenantSwitcherBar />);

    // This bar is on every authenticated page. A super-admin who has not asked which other
    // companies exist should not be sending a request on every screen to find out.
    expect(apiMock).not.toHaveBeenCalled();
  });

  it('lists the real companies the server returns', async () => {
    // ⚠️ The whole point of the rewrite. This used to offer four hard-coded names
    // (tenant-acme, tenant-globex, …) that were not GUIDs and did not exist, so the switcher
    // could not switch to any of them once the server started reading the header.
    signIn();
    const user = userEvent.setup();

    render(<TenantSwitcherBar />);
    await user.click(screen.getByRole('button', { name: 'Switch company' }));

    expect(apiMock).toHaveBeenCalledWith('/tenants');

    // Scoped to the menu: the banner also names the company being viewed, which is the point of
    // the banner and not a duplicate to design away.
    const menu = within(await screen.findByRole('group', { name: 'Companies in this database' }));
    expect(menu.getByText('Bravo Company')).toBeInTheDocument();
    expect(menu.getByText('Alpha Company')).toBeInTheDocument();
    expect(screen.queryByText('Acme Corporation')).not.toBeInTheDocument();
  });

  it('stores the chosen company as the active tenant', async () => {
    signIn();
    const user = userEvent.setup();
    const onTenantChange = vi.fn();

    render(<TenantSwitcherBar onTenantChange={onTenantChange} />);
    await user.click(screen.getByRole('button', { name: 'Switch company' }));
    await user.click(await screen.findByText('Bravo Company'));

    expect(onTenantChange).toHaveBeenCalledWith(BRAVO, 'Bravo Company');
    // This is what becomes the X-Tenant-Id header, so it has to be the GUID the server knows.
    expect(auth.get()?.activeTenantId).toBe(BRAVO);
  });

  it('says so when the company list cannot be loaded', async () => {
    signIn();
    apiMock.mockRejectedValue(new Error('Request failed (500)'));
    const user = userEvent.setup();

    render(<TenantSwitcherBar />);
    await user.click(screen.getByRole('button', { name: 'Switch company' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Request failed (500)');
  });

  it('explains an empty list rather than showing an empty menu', async () => {
    signIn();
    apiMock.mockResolvedValue([]);
    const user = userEvent.setup();

    render(<TenantSwitcherBar />);
    await user.click(screen.getByRole('button', { name: 'Switch company' }));

    await waitFor(() =>
      expect(screen.getByText(/One database per company is the normal deployment/)).toBeInTheDocument()
    );
  });
});

describe('the tenant override is a super-admin thing only', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('does not put an activeTenantId on an ordinary session', () => {
    // ⚠️ `activeTenantId` becomes the X-Tenant-Id header. Until 2026-08-26 every signed-in user
    // got one, so every request carried a tenant id the server was trusted to ignore. The server
    // does gate on the token — but there is no reason for an ordinary user to send it at all,
    // and "harmless header nobody reads" is how it stayed unnoticed that nothing read it.
    auth.set({
      accessToken: 't',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Recruiter',
      displayName: 'Ordinary',
      userId: 'usr-1',
      isSuperAdmin: false,
      tenantId: ALPHA,
      permissions: [],
    } as Parameters<typeof auth.set>[0]);

    expect(auth.get()?.activeTenantId).toBeUndefined();
  });

  it('keeps a super-admin on the company they switched to across a token refresh', () => {
    auth.set({
      accessToken: 't1',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super',
      userId: 'usr-super',
      isSuperAdmin: true,
      tenantId: ALPHA,
      permissions: [],
    } as Parameters<typeof auth.set>[0]);

    auth.setActiveTenant(BRAVO, 'Bravo Company');

    // A refresh re-runs auth.set with the login shape, which names their OWN tenant. Without the
    // carry-over the super-admin would be bounced back to Alpha mid-task, silently.
    auth.set({
      accessToken: 't2',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super',
      userId: 'usr-super',
      isSuperAdmin: true,
      tenantId: ALPHA,
      permissions: [],
    } as Parameters<typeof auth.set>[0]);

    expect(auth.get()?.activeTenantId).toBe(BRAVO);
    expect(auth.get()?.activeTenantName).toBe('Bravo Company');
  });

  it('does not carry one super-admin’s override into another’s session', () => {
    auth.set({
      accessToken: 't1',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super One',
      userId: 'usr-super-1',
      isSuperAdmin: true,
      tenantId: ALPHA,
      permissions: [],
    } as Parameters<typeof auth.set>[0]);
    auth.setActiveTenant(BRAVO, 'Bravo Company');

    auth.set({
      accessToken: 't2',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'SuperAdmin',
      displayName: 'Super Two',
      userId: 'usr-super-2',
      isSuperAdmin: true,
      tenantId: ALPHA,
      permissions: [],
    } as Parameters<typeof auth.set>[0]);

    expect(auth.get()?.activeTenantId).toBe(ALPHA);
  });
});
