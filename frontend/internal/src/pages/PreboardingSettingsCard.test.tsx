import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PreboardingSettings, UserRole } from '@recruitops/types';
import { ApiError } from '../lib/api';
import { auth } from '../lib/auth';
import { PreboardingSettingsCard } from './PreboardingSettingsCard';

// The pre-boarding settings (ADR-0030 §5b), drawn in settings-integrations.html: every flag is sent,
// always; an address the API refuses says why; and without settings:update nothing looks editable.
const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));
vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

const SETTINGS: PreboardingSettings = {
  nrcRequired: true, bankAccountRequired: true, educationRequired: true, photoRequired: false,
  itHandoffEmail: 'it@ymb.test', adminHandoffEmail: null,
};

function signIn(permissions: string[], role: UserRole = 'Admin') {
  auth.set({
    accessToken: 'token', expiresAtUtc: '2099-01-01T00:00:00Z', role,
    displayName: 'Test User', userId: 'usr-1', isSuperAdmin: false, permissions,
  });
}

// A block body: a function returned from beforeEach is run as a teardown, and mockReset returns the mock.
beforeEach(() => {
  apiMock.mockReset();
});

describe('PreboardingSettingsCard', () => {
  it('sends every flag and both addresses, blank as none', async () => {
    signIn(['permission:settings:settings:read', 'permission:settings:settings:update']);
    let body: unknown;
    apiMock.mockImplementation((_path: string, init?: RequestInit) => {
      if (init?.method === 'PUT') { body = JSON.parse(String(init.body)); return Promise.resolve(body); }
      return Promise.resolve(SETTINGS);
    });
    render(<PreboardingSettingsCard />);

    const photo = await screen.findByRole('radiogroup', { name: 'Photo for the staff card' });
    await userEvent.click(photo.querySelector('[role=radio]')!); // "Required"
    await userEvent.clear(screen.getByLabelText('IT — email account and laptop'));
    await userEvent.type(screen.getByLabelText('Admin — desk and staff card'), '  admin@ymb.test ');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(body).toEqual({ ...SETTINGS, photoRequired: true, itHandoffEmail: null, adminHandoffEmail: 'admin@ymb.test' });
    expect(await screen.findByRole('status')).toHaveTextContent('Saved.');
  });

  it('shows the API’s reason when an address is refused', async () => {
    signIn(['permission:settings:settings:read', 'permission:settings:settings:update']);
    apiMock.mockImplementation((_path: string, init?: RequestInit) => init?.method === 'PUT'
      ? Promise.reject(new ApiError(400, 'The IT address must be one email address.'))
      : Promise.resolve(SETTINGS));
    render(<PreboardingSettingsCard />);
    await userEvent.click(await screen.findByRole('button', { name: 'Save' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('The IT address must be one email address.');
  });

  it('is read-only without settings:update, and absent when the read is refused', async () => {
    signIn(['permission:settings:settings:read'], 'HrDirector');
    apiMock.mockResolvedValue(SETTINGS);
    const { unmount } = render(<PreboardingSettingsCard />);
    expect(await screen.findByLabelText('IT — email account and laptop')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
    unmount();
    apiMock.mockReset();

    apiMock.mockImplementation(() => Promise.reject(new ApiError(403, 'Forbidden')));
    const { container } = render(<PreboardingSettingsCard />);
    await waitFor(() => expect(apiMock).toHaveBeenCalledTimes(1));
    expect(container).toBeEmptyDOMElement();
  });
});
