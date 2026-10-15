import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PermissionMatrixGrid } from '../components/PermissionMatrixGrid';
import { RolesPage } from '../pages/RolesPage';
import { UsersPage } from '../pages/UsersPage';
import { roleService } from '../services/roleService';
import { userService } from '../services/userService';
import { auth } from '../lib/auth';
import { mockPermissionsGrouped, mockRoles, mockRoleDetail, mockPagedUsers, adminDirectoryPermissions } from './rbacFixtures';

vi.mock('../services/roleService', () => ({
  roleService: {
    getRoles: vi.fn(),
    getPermissions: vi.fn(),
    getRoleById: vi.fn(),
    createRole: vi.fn(),
    updateRole: vi.fn(),
    deleteRole: vi.fn(),
  },
}));

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

describe('Milestone 4 Empirical Challenge Test Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    // An Admin with the directory and role-builder permissions; sections below override it.
    auth.set({
      accessToken: 'token-admin',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Admin',
      displayName: 'Admin User',
      userId: 'usr-1',
      isSuperAdmin: false,
      permissions: adminDirectoryPermissions,
    });
    (roleService.getRoles as any).mockResolvedValue(mockRoles);
    (roleService.getPermissions as any).mockResolvedValue(mockPermissionsGrouped);
    (roleService.getRoleById as any).mockResolvedValue(mockRoleDetail);
    (userService.getUsers as any).mockResolvedValue(mockPagedUsers);
  });

  /* ---------------------------------------------------------------- border
   * 1. PERMISSION MATRIX GRID & ROLE BUILDER UI
   * ---------------------------------------------------------------- */
  describe('Permission Matrix Grid Toggles & Custom Role Submissions', () => {
    it('toggles an entire module permissions when module checkbox is clicked', () => {
      const handleChange = vi.fn();
      render(
        <PermissionMatrixGrid
          modules={mockPermissionsGrouped}
          selectedPermissionCodes={[]}
          onChange={handleChange}
        />
      );

      // Module level checkbox for 'users' module
      const moduleCheckboxes = screen.getAllByRole('checkbox');
      // The module level checkbox is the first checkbox in module header
      fireEvent.click(moduleCheckboxes[0]);

      // Should select all permission codes in users module
      expect(handleChange).toHaveBeenCalledWith([
        'permission:users:users:read',
        'permission:users:users:create',
        'permission:users:users:update',
        'permission:users:users:delete',
      ]);
    });

    it('toggles feature permissions when feature checkbox is clicked', () => {
      const handleChange = vi.fn();
      render(
        <PermissionMatrixGrid
          modules={mockPermissionsGrouped}
          selectedPermissionCodes={['permission:users:users:read']}
          onChange={handleChange}
        />
      );

      const featureCheckboxes = screen.getAllByTitle('Toggle feature permissions');
      fireEvent.click(featureCheckboxes[0]); // users feature

      expect(handleChange).toHaveBeenCalledWith([
        'permission:users:users:read',
        'permission:users:users:create',
        'permission:users:users:update',
        'permission:users:users:delete',
      ]);
    });

    it('toggles special action permissions (e.g. Approve Requisitions)', () => {
      const handleChange = vi.fn();
      render(
        <PermissionMatrixGrid
          modules={mockPermissionsGrouped}
          selectedPermissionCodes={[]}
          onChange={handleChange}
        />
      );

      const approveCheckbox = screen.getByLabelText('Approve Requisitions');
      fireEvent.click(approveCheckbox);

      expect(handleChange).toHaveBeenCalledWith(['permission:requisitions:requisitions:approve']);
    });

    it('allows editing an existing custom role and submitting updated permissions', async () => {
      (roleService.updateRole as any).mockResolvedValue({
        ...mockRoleDetail,
        assignedPermissionCodes: ['permission:users:users:read'],
      });

      render(<RolesPage />);
      await screen.findByText('Role Builder & Permissions');

      // Click Edit Matrix on Custom Recruiter role
      const editButtons = screen.getAllByText('Edit Matrix');
      fireEvent.click(editButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Edit Role: Custom Recruiter')).toBeInTheDocument();
      });

      // Submit changes
      const saveBtn = screen.getByRole('button', { name: 'Save Changes' });
      fireEvent.click(saveBtn);

      await waitFor(() => {
        expect(roleService.updateRole).toHaveBeenCalledWith('role-custom', {
          name: 'Custom Recruiter',
          description: 'Customized recruiter role',
          isActive: true,
          permissionCodes: ['permission:users:users:read', 'permission:users:users:create'],
        });
      });
    });

    it('prevents deleting custom role when userCount > 0', async () => {
      const rolesWithUsers = [
        {
          ...mockRoles[1],
          userCount: 5, // Assigned to 5 users
        },
      ];
      (roleService.getRoles as any).mockResolvedValue(rolesWithUsers);

      render(<RolesPage />);
      await screen.findByText('Role Builder & Permissions');

      const deleteBtn = screen.getByRole('button', { name: 'Delete' });
      expect(deleteBtn).toBeDisabled();
      expect(deleteBtn).toHaveAttribute('title', 'Cannot delete role assigned to active users');
    });

    it('confirms and executes role deletion when userCount is 0', async () => {
      (roleService.deleteRole as any).mockResolvedValue(undefined);

      render(<RolesPage />);
      await screen.findByText('Role Builder & Permissions');

      const deleteBtn = screen.getByRole('button', { name: 'Delete' });
      fireEvent.click(deleteBtn);

      expect(screen.getByText('Confirm Role Deletion')).toBeInTheDocument();

      const confirmBtn = screen.getByRole('button', { name: 'Delete Role' });
      fireEvent.click(confirmBtn);

      await waitFor(() => {
        expect(roleService.deleteRole).toHaveBeenCalledWith('role-custom');
      });
    });
  });

  /* ---------------------------------------------------------------- border
   * 2. USER TABLE PAGINATION & FILTERING
   * ---------------------------------------------------------------- */
  describe('User Table Search, Filters, Pagination, & Modals', () => {
    beforeEach(() => {
      auth.set({
        accessToken: 'token-admin',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Admin',
        displayName: 'Admin User',
        userId: 'usr-1',
        isSuperAdmin: false,
        permissions: adminDirectoryPermissions,
      });
    });

    it('triggers search query filtering when typing in search input', async () => {
      render(<UsersPage />);
      await screen.findByText('User Directory');

      const searchInput = screen.getByPlaceholderText('Search email or name...');
      fireEvent.change(searchInput, { target: { value: 'Jane' } });

      await waitFor(() => {
        expect(userService.getUsers).toHaveBeenLastCalledWith({
          page: 1,
          pageSize: 20,
          search: 'Jane',
          roleId: undefined,
          isActive: undefined,
        });
      });
    });

    it('triggers role filter query when selecting a role dropdown option', async () => {
      render(<UsersPage />);
      await screen.findByText('User Directory');

      const selects = screen.getAllByRole('combobox');
      // selects[0] is role filter select
      fireEvent.change(selects[0], { target: { value: 'role-admin' } });

      await waitFor(() => {
        expect(userService.getUsers).toHaveBeenLastCalledWith({
          page: 1,
          pageSize: 20,
          search: undefined,
          roleId: 'role-admin',
          isActive: undefined,
        });
      });
    });

    it('triggers active status query when selecting active/inactive dropdown', async () => {
      render(<UsersPage />);
      await screen.findByText('User Directory');

      const selects = screen.getAllByRole('combobox');
      // selects[1] is active filter select
      fireEvent.change(selects[1], { target: { value: 'false' } });

      await waitFor(() => {
        expect(userService.getUsers).toHaveBeenLastCalledWith({
          page: 1,
          pageSize: 20,
          search: undefined,
          roleId: undefined,
          isActive: false,
        });
      });
    });

    it('handles pagination controls and page size changing', async () => {
      const multiPageData = {
        ...mockPagedUsers,
        totalCount: 45,
        totalPages: 3,
      };
      (userService.getUsers as any).mockResolvedValue(multiPageData);

      render(<UsersPage />);
      await screen.findByText('User Directory');

      // Click Next Page button
      const nextBtn = screen.getByRole('button', { name: 'Next' });
      fireEvent.click(nextBtn);

      await waitFor(() => {
        expect(userService.getUsers).toHaveBeenLastCalledWith({
          page: 2,
          pageSize: 20,
          search: undefined,
          roleId: undefined,
          isActive: undefined,
        });
      });

      // Change page size dropdown (selects[2] is rows per page)
      const selects = screen.getAllByRole('combobox');
      const pageSizeSelect = selects[2];
      fireEvent.change(pageSizeSelect, { target: { value: '50' } });

      await waitFor(() => {
        expect(userService.getUsers).toHaveBeenLastCalledWith({
          page: 1,
          pageSize: 50,
          search: undefined,
          roleId: undefined,
          isActive: undefined,
        });
      });
    });

    it('executes user deactivation after confirmation modal', async () => {
      (userService.deactivateUser as any).mockResolvedValue({
        ...mockPagedUsers.items[1],
        isActive: false,
      });

      render(<UsersPage />);
      await screen.findByText('User Directory');

      // Click Deactivate on Jane Recruiter (usr-2)
      const deactivateBtns = screen.getAllByRole('button', { name: 'Deactivate' });
      // usr-1 is 'You' (disabled), usr-2 is index 1
      fireEvent.click(deactivateBtns[1]);

      expect(screen.getByText('Confirm Account Deactivation')).toBeInTheDocument();

      const confirmBtn = screen.getByRole('button', { name: 'Confirm Deactivation' });
      fireEvent.click(confirmBtn);

      await waitFor(() => {
        expect(userService.deactivateUser).toHaveBeenCalledWith('usr-2');
      });
    });

    it('displays safeguard warning when trying to deactivate the last active administrator', async () => {
      const singleAdminOnlyPaged = {
        items: [
          {
            id: 'usr-1',
            email: 'myadmin@recruitops.io',
            displayName: 'My Admin',
            role: 'Admin',
            roleId: 'role-admin',
            isActive: true,
          },
          {
            id: 'usr-recruiter',
            email: 'rec@recruitops.io',
            displayName: 'Recruiter User',
            role: 'Recruiter',
            roleId: 'role-recruiter',
            isActive: true,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
      };
      (userService.getUsers as any).mockResolvedValue(singleAdminOnlyPaged);

      // Signed in as a *second* administrator, so usr-1's Deactivate button is not disabled by
      // the self-deactivation safeguard. This used to sign in as a Recruiter — a session the
      // real app never produces on this screen (GET /api/users is AdminOnly), and one that only
      // rendered the button because hasPermission failed open on a session with no list.
      auth.set({
        accessToken: 'token-admin-2',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Admin',
        displayName: 'Second Admin',
        userId: 'usr-admin-2',
        isSuperAdmin: false,
        permissions: adminDirectoryPermissions,
      });

      render(<UsersPage />);
      await screen.findByText('User Directory');

      // Now usr-1 (the single Admin) has an enabled Deactivate button
      const deactivateBtns = screen.getAllByRole('button', { name: 'Deactivate' });
      fireEvent.click(deactivateBtns[0]); // usr-1

      expect(screen.getByText(/Safeguard Warning/)).toBeInTheDocument();
      expect(screen.getByText(/Cannot deactivate the last active Administrator account/)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Confirm Deactivation' })).toBeDisabled();
    });
  });
});
