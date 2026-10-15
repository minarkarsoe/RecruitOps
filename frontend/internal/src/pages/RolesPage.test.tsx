import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RolesPage } from './RolesPage';
import { roleService } from '../services/roleService';
import { auth } from '../lib/auth';
import { mockRoles, mockPermissionsGrouped, mockRoleDetail } from '../test/rbacFixtures';

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

describe('RolesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    // The page's create/edit/delete controls are permission-gated. This suite used to run
    // with no session at all and still saw them, because hasPermission() returned true for
    // a null session; it now has to grant what the scenarios exercise.
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
    (roleService.getRoles as any).mockResolvedValue(mockRoles);
    (roleService.getPermissions as any).mockResolvedValue(mockPermissionsGrouped);
    (roleService.getRoleById as any).mockResolvedValue(mockRoleDetail);
  });

  it('renders list of system and custom roles', async () => {
    render(<RolesPage />);

    expect(await screen.findByText('Role Builder & Permissions')).toBeInTheDocument();
    expect(screen.getAllByText('Admin')[0]).toBeInTheDocument();
    expect(screen.getByText('Custom Recruiter')).toBeInTheDocument();
    expect(screen.getByText('System Role')).toBeInTheDocument();
    expect(screen.getByText('Custom Role')).toBeInTheDocument();
  });

  it('opens Create Custom Role modal and submits form payload', async () => {
    (roleService.createRole as any).mockResolvedValue(mockRoleDetail);

    render(<RolesPage />);
    await screen.findByText('Role Builder & Permissions');

    // Click Create button
    fireEvent.click(screen.getByText('+ Create Custom Role'));

    expect(screen.getByText('Create Custom Role')).toBeInTheDocument();

    // Fill form
    fireEvent.change(screen.getByPlaceholderText('e.g. Talent Acquisition Partner'), {
      target: { value: 'Talent Acquisition Partner' },
    });
    fireEvent.change(screen.getByLabelText(/Base role/), { target: { value: 'HiringManager' } });

    const submitBtn = screen.getByRole('button', { name: 'Create Role' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(roleService.createRole).toHaveBeenCalledWith({
        name: 'Talent Acquisition Partner',
        code: 'TALENT_ACQUISITION_PARTNER',
        description: undefined,
        permissionCodes: [],
        baseRole: 'HiringManager',
      });
    });
  });

  // ADR-0021 option A: a custom role's holders carry its base role. Until 2026-10-15 they all
  // carried Recruiter, so the Role Builder never said what a custom role could reach.
  it('preselects no base role, and will not create a role until one is chosen', async () => {
    render(<RolesPage />);
    await screen.findByText('Role Builder & Permissions');
    fireEvent.click(screen.getByText('+ Create Custom Role'));

    expect((screen.getByLabelText(/Base role/) as HTMLSelectElement).value).toBe('');

    fireEvent.change(screen.getByPlaceholderText('e.g. Talent Acquisition Partner'), {
      target: { value: 'Panel Observer' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Create Role' }));

    expect(await screen.findByText('Choose a base role for this custom role.')).toBeInTheDocument();
    expect(roleService.createRole).not.toHaveBeenCalled();
  });

  it('offers every base role below Admin, and says what the chosen one reaches', async () => {
    render(<RolesPage />);
    await screen.findByText('Role Builder & Permissions');
    fireEvent.click(screen.getByText('+ Create Custom Role'));

    const picker = screen.getByLabelText(/Base role/) as HTMLSelectElement;
    const offered = Array.from(picker.options).filter((o) => o.value !== '').map((o) => o.value);
    expect(offered).toEqual(['HrDirector', 'Recruiter', 'HiringManager', 'Approver', 'Interviewer']);

    fireEvent.change(picker, { target: { value: 'Approver' } });
    expect(screen.getByText('Requisitions to approve; no candidate data.')).toBeInTheDocument();
  });

  it('shows a custom role’s base role, and sends a changed one on save', async () => {
    (roleService.updateRole as any).mockResolvedValue(mockRoleDetail);
    render(<RolesPage />);
    await screen.findByText('Role Builder & Permissions');

    expect(screen.getByText('Base: Recruiter')).toBeInTheDocument();

    fireEvent.click(screen.getByText('Edit Matrix'));
    const picker = (await screen.findByLabelText(/Base role/)) as HTMLSelectElement;
    expect(picker.value).toBe('Recruiter');

    fireEvent.change(picker, { target: { value: 'Interviewer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));

    await waitFor(() =>
      expect(roleService.updateRole).toHaveBeenCalledWith(
        'role-custom',
        expect.objectContaining({ baseRole: 'Interviewer' }),
      ),
    );
  });

  it('opens view matrix for system role in read-only mode', async () => {
    render(<RolesPage />);
    await screen.findByText('Role Builder & Permissions');
    const adminElems = await screen.findAllByText('Admin');
    expect(adminElems[0]).toBeInTheDocument();

    const viewButtons = screen.getAllByText('View Matrix');
    fireEvent.click(viewButtons[0]);

    await waitFor(() => {
      expect(screen.getByText(/System Protected Role/)).toBeInTheDocument();
    });
  });


});
