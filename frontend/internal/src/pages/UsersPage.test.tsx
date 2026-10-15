import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { UsersPage } from './UsersPage';
import { userService } from '../services/userService';
import { roleService } from '../services/roleService';
import { auth } from '../lib/auth';
import { mockRoles, mockPagedUsers } from '../test/rbacFixtures';

vi.mock('../services/userService', () => ({
  userService: {
    getUsers: vi.fn(),
    getUserById: vi.fn(),
    createUser: vi.fn(),
    updateUser: vi.fn(),
    deactivateUser: vi.fn(),
    reactivateUser: vi.fn(),
  },
}));

vi.mock('../services/roleService', () => ({
  roleService: {
    getRoles: vi.fn(),
  },
}));

describe('UsersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    auth.set({
      accessToken: 'token-123',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Admin',
      displayName: 'Admin User',
      userId: 'usr-1',
      isSuperAdmin: false,
      permissions: [],
    });

    (userService.getUsers as any).mockResolvedValue(mockPagedUsers);
    (roleService.getRoles as any).mockResolvedValue(mockRoles);
  });

  it('renders user directory table with pagination and role filters', async () => {
    render(<UsersPage />);

    expect(await screen.findByText('User Directory')).toBeInTheDocument();
    expect(screen.getByText('Admin User')).toBeInTheDocument();
    expect(screen.getByText('Jane Recruiter')).toBeInTheDocument();
    expect(screen.getByText('admin@recruitops.io')).toBeInTheDocument();
  });

  it('opens Create User modal and submits user data', async () => {
    (userService.createUser as any).mockResolvedValue({
      id: 'usr-3',
      email: 'newuser@recruitops.io',
      displayName: 'New User',
      role: 'Admin',
      roleId: 'role-admin',
      roleDetails: null,
      permissions: [],
      isActive: true,
      isSuperAdmin: false,
      createdAt: '2026-07-30T00:00:00Z',
      updatedAt: null,
    });

    render(<UsersPage />);
    await screen.findByText('User Directory');

    fireEvent.click(screen.getByText('+ Create User'));

    expect(screen.getByText('Create New User')).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText('colleague@company.com'), {
      target: { value: 'newuser@recruitops.io' },
    });
    fireEvent.change(screen.getByPlaceholderText('Jane Doe'), {
      target: { value: 'New User' },
    });
    fireEvent.change(screen.getByPlaceholderText('Minimum 8 characters'), {
      target: { value: 'password123' },
    });

    // No role is preselected (it used to default to "Admin", alphabetically first) — choose one.
    fireEvent.change(screen.getByText(/Assigned Role/).parentElement!.querySelector('select')!, {
      target: { value: 'role-admin' },
    });

    fireEvent.click(screen.getByRole('button', { name: 'Create Account' }));

    await waitFor(() => {
      expect(userService.createUser).toHaveBeenCalledWith({
        email: 'newuser@recruitops.io',
        displayName: 'New User',
        password: 'password123',
        roleId: 'role-admin',
        role: 'Admin',
      });
    });
  });

  // Second security review (b)-4: the picker defaulted to the first assignable role — "Admin",
  // alphabetically — so a hurried create made an Admin. A role is now always a deliberate choice.
  it('preselects no role, and will not create a user until one is chosen', async () => {
    render(<UsersPage />);
    await screen.findByText('User Directory');
    fireEvent.click(screen.getByText('+ Create User'));

    const picker = screen.getByText(/Assigned Role/).parentElement!.querySelector('select')!;
    expect(picker.value).toBe('');

    fireEvent.change(screen.getByPlaceholderText('colleague@company.com'), { target: { value: 'x@recruitops.io' } });
    fireEvent.change(screen.getByPlaceholderText('Jane Doe'), { target: { value: 'X' } });
    fireEvent.change(screen.getByPlaceholderText('Minimum 8 characters'), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create Account' }));

    expect(await screen.findByText('Choose a role for this user.')).toBeInTheDocument();
    expect(userService.createUser).not.toHaveBeenCalled();
  });

  // ADR-0021: the API refuses SuperAdmin on assignment, so the picker must not offer it. Custom
  // roles stay on offer (ADR-0022); inactive ones cannot be assigned.
  it('offers custom and system roles but never SuperAdmin or an inactive role', async () => {
    (roleService.getRoles as any).mockResolvedValue([
      ...mockRoles,
      { ...mockRoles[0], id: 'role-super', name: 'SuperAdmin', code: 'SuperAdmin', isSuperAdmin: true },
      { ...mockRoles[0], id: 'role-retired', name: 'Retired', code: 'Retired', isActive: false },
    ]);

    render(<UsersPage />);
    await screen.findByText('User Directory');
    fireEvent.click(screen.getByText('+ Create User'));

    const picker = screen.getByText(/Assigned Role/).parentElement!.querySelector('select')!;
    const offered = Array.from(picker.options).filter((o) => o.value !== '').map((o) => o.value);
    expect(offered).toEqual(mockRoles.filter((r) => r.isActive && !r.isSuperAdmin).map((r) => r.id));
  });

  // Security review A5 + second review: a rename must not resend the role. Resending it is how an
  // edit of a SuperAdmin failed (400), and an unchanged role is the most common edit there is.
  it('renames a user without sending their unchanged role', async () => {
    (userService.updateUser as any).mockResolvedValue({});
    render(<UsersPage />);
    await screen.findByText('User Directory');

    fireEvent.click(screen.getAllByRole('button', { name: 'Edit' })[1]);
    fireEvent.change(screen.getByDisplayValue('Jane Recruiter'), { target: { value: 'Jane R.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));

    await waitFor(() =>
      expect(userService.updateUser).toHaveBeenCalledWith('usr-2', { displayName: 'Jane R.', roleId: null, role: null }));
  });

  it('disables self-deactivation button for current session user', async () => {
    render(<UsersPage />);
    await screen.findByText('User Directory');

    // usr-1 is "You" (current logged in user)
    const deactivateBtns = screen.getAllByRole('button', { name: 'Deactivate' });
    expect(deactivateBtns[0]).toBeDisabled();
    expect(deactivateBtns[0]).toHaveAttribute('title', 'You cannot deactivate your own account.');
  });
});
