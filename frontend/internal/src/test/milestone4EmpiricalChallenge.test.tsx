import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PermissionMatrixGrid } from '../components/PermissionMatrixGrid';
import { RolesPage } from '../pages/RolesPage';
import { UsersPage } from '../pages/UsersPage';
import { TenantSwitcherBar } from '../components/TenantSwitcherBar';
import { roleService } from '../services/roleService';
import { userService } from '../services/userService';
import { auth } from '../lib/auth';
import { api } from '../lib/api';
import { mockPermissionsGrouped, mockRoles, mockRoleDetail, mockPagedUsers } from './rbacFixtures';

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
    (roleService.getRoles as any).mockResolvedValue(mockRoles);
    (roleService.getPermissions as any).mockResolvedValue(mockPermissionsGrouped);
    (roleService.getRoleById as any).mockResolvedValue(mockRoleDetail);
    (userService.getUsers as any).mockResolvedValue(mockPagedUsers);
  });

  /* ---------------------------------------------------------------- border
   * 1. PERMISSION MATRIX GRID & ROLE BUILDER UI
   * ---------------------------------------------------------------- */
  describe('Permission Matrix Grid Toggles & Custom Role Submissions', () => {
    beforeEach(() => {
      // The outer beforeEach clears sessionStorage, so these role-builder scenarios ran with
      // no session. They passed only because hasPermission() granted a null session
      // everything; the create/edit/delete controls they drive are permission-gated.
      auth.set({
        accessToken: 'token-role-admin',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Role Manager',
        userId: 'usr-role-admin',
        isSuperAdmin: false,
        permissions: [
          'permission:roles:roles:read',
          'permission:roles:roles:create',
          'permission:roles:roles:update',
          'permission:roles:roles:delete',
        ],
      });
    });

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
        permissions: [],
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

      // Set logged in user as usr-recruiter (so usr-1 Deactivate button is not disabled by self safeguard)
      auth.set({
        accessToken: 'token-recruiter',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Recruiter User',
        userId: 'usr-recruiter',
        isSuperAdmin: false,
        // Needed explicitly now: this overrides the Admin session from the enclosing
        // beforeEach, and a Recruiter no longer inherits the directory controls by default.
        permissions: [
          'permission:users:users:read',
          'permission:users:users:update',
          'permission:users:users:delete',
        ],
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

  /* ---------------------------------------------------------------- border
   * 3. SUPER-ADMIN TENANT SWITCHER & X-TENANT-ID HEADER
   * ---------------------------------------------------------------- */
  describe('Super-Admin Tenant Switcher & X-Tenant-Id Request Headers', () => {
    // Real GUIDs, because a tenant id IS a Company.Id. The four hard-coded strings this file used
    // to assert on (`tenant-stark`, `tenant-acme-corp`, …) could never have been real companies.
    const SUPER_TENANT_A = '11111111-1111-1111-1111-111111111111';
    const SUPER_TENANT_B = '22222222-2222-2222-2222-222222222222';

    /** The switcher fetches GET /api/tenants through the real `api`, so stub the transport
     *  rather than the module — the header test below needs the genuine article. */
    function stubTenantFetch(rows: unknown[]) {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          status: 200,
          text: () => Promise.resolve(JSON.stringify(rows)),
        })
      );
    }

    afterEach(() => vi.unstubAllGlobals());

    it('updates session active tenant when switching to a company the server lists', async () => {
      // ⚠️ Rewritten 2026-08-26. This used to click "Stark Industries" and assert the session
      // held `tenant-stark` — one of four hard-coded names in the component. A tenant id is a
      // `Company.Id`, i.e. a GUID, so none of those four could ever be a real company, and the
      // switcher could not switch to any of them once the server started reading X-Tenant-Id.
      // The list now comes from GET /api/tenants.
      stubTenantFetch([
        { id: SUPER_TENANT_A, name: 'Alpha Company', code: 'alpha', isActive: true },
        { id: SUPER_TENANT_B, name: 'Bravo Company', code: 'bravo', isActive: true },
      ]);

      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Global Super Admin',
        userId: 'usr-super',
        isSuperAdmin: true,
        tenantId: SUPER_TENANT_A,
        permissions: [],
      });

      const handleTenantChange = vi.fn();
      render(<TenantSwitcherBar onTenantChange={handleTenantChange} />);

      fireEvent.click(screen.getByRole('button', { name: 'Switch company' }));
      fireEvent.click(await screen.findByText('Bravo Company'));

      expect(handleTenantChange).toHaveBeenCalledWith(SUPER_TENANT_B, 'Bravo Company');
      expect(auth.get()?.activeTenantId).toBe(SUPER_TENANT_B);
      expect(auth.get()?.activeTenantName).toBe('Bravo Company');
    });

    it('offers no free-text tenant box, because the list is now complete', async () => {
      // The "Custom Tenant ID" input existed to work around a fake list — with four invented
      // companies on offer, typing an id was the only way to reach a real one. GET /api/tenants
      // returns every active company, so a hand-typed GUID can only ever name something already
      // on the list. What it could still do is send an id the server rejects with a 400.
      stubTenantFetch([{ id: SUPER_TENANT_A, name: 'Alpha Company', code: 'alpha', isActive: true }]);

      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Global Super Admin',
        userId: 'usr-super',
        isSuperAdmin: true,
        tenantId: SUPER_TENANT_A,
        permissions: [],
      });

      render(<TenantSwitcherBar />);
      fireEvent.click(screen.getByRole('button', { name: 'Switch company' }));
      await screen.findByRole('group', { name: 'Companies in this database' });

      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Set' })).not.toBeInTheDocument();
    });

    it('sends X-Tenant-Id header in fetch calls when activeTenantId is set in session', async () => {
      const globalFetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve(JSON.stringify({ success: true })),
      });
      vi.stubGlobal('fetch', globalFetch);

      auth.set({
        accessToken: 'token-super-xyz',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super Admin',
        userId: 'usr-super',
        isSuperAdmin: true,
        tenantId: SUPER_TENANT_B,
        permissions: [],
      });

      await api('/roles');

      expect(globalFetch).toHaveBeenCalledWith(
        '/api/roles',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Bearer token-super-xyz',
            'X-Tenant-Id': SUPER_TENANT_B,
          }),
        })
      );

      vi.unstubAllGlobals();
    });
  });
});
