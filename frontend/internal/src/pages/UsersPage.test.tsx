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

  it('disables self-deactivation button for current session user', async () => {
    render(<UsersPage />);
    await screen.findByText('User Directory');

    // usr-1 is "You" (current logged in user)
    const deactivateBtns = screen.getAllByRole('button', { name: 'Deactivate' });
    expect(deactivateBtns[0]).toBeDisabled();
    expect(deactivateBtns[0]).toHaveAttribute('title', 'You cannot deactivate your own account.');
  });
});
