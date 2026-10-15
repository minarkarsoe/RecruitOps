import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { UsersPage } from './UsersPage';
import { userService } from '../services/userService';
import { roleService } from '../services/roleService';
import { auth } from '../lib/auth';
import { mockRoles, mockPagedUsers, adminDirectoryPermissions } from '../test/rbacFixtures';

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
      permissions: adminDirectoryPermissions,
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

  // ADR-0021: the API refuses SuperAdmin and custom roles on assignment, so the picker must
  // not offer them. The table's role *filter* is a different control and keeps every role.
  it('offers only assignable roles when creating a user', async () => {
    (roleService.getRoles as any).mockResolvedValue([
      ...mockRoles,
      { ...mockRoles[0], id: 'role-super', name: 'SuperAdmin', code: 'SuperAdmin', isSuperAdmin: true },
      { ...mockRoles[0], id: 'role-retired', name: 'Retired', code: 'Retired', isActive: false },
    ]);

    render(<UsersPage />);
    await screen.findByText('User Directory');
    fireEvent.click(screen.getByText('+ Create User'));

    const picker = screen.getByText(/Assigned Role/).parentElement!.querySelector('select')!;
    const offered = Array.from(picker.options).filter((o) => o.value !== '').map((o) => o.textContent);
    expect(offered).toEqual(['Admin']);
  });

  // Security review A5: a user whose current role cannot be assigned (a legacy custom role,
  // SuperAdmin) used to show the first option ("Admin") while holding the old id — and every
  // save resent a role, so even a rename failed with 409/400.
  it('renames a custom-role holder without touching their role', async () => {
    (userService.updateUser as any).mockResolvedValue({});
    render(<UsersPage />);
    await screen.findByText('User Directory');

    fireEvent.click(screen.getAllByRole('button', { name: 'Edit' })[1]); // Jane, custom role
    const picker = screen.getByText('Assigned Role').parentElement!.querySelector('select')!;
    expect(picker.selectedOptions[0].textContent).toMatch(/Custom Recruiter.*unchanged/);

    fireEvent.change(screen.getByDisplayValue('Jane Recruiter'), { target: { value: 'Jane R.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));

    await waitFor(() =>
      expect(userService.updateUser).toHaveBeenCalledWith('usr-2', { displayName: 'Jane R.', roleId: null, role: null }));
  });

  it('sends the new role when one is actually chosen', async () => {
    (userService.updateUser as any).mockResolvedValue({});
    (roleService.getRoles as any).mockResolvedValue([
      ...mockRoles,
      { ...mockRoles[0], id: 'role-recruiter', name: 'Recruiter', code: 'Recruiter' },
    ]);
    render(<UsersPage />);
    await screen.findByText('User Directory');

    fireEvent.click(screen.getAllByRole('button', { name: 'Edit' })[1]);
    const picker = screen.getByText('Assigned Role').parentElement!.querySelector('select')!;
    fireEvent.change(picker, { target: { value: 'role-recruiter' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));

    await waitFor(() =>
      expect(userService.updateUser).toHaveBeenCalledWith('usr-2',
        { displayName: 'Jane Recruiter', roleId: 'role-recruiter', role: 'Recruiter' }));
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
