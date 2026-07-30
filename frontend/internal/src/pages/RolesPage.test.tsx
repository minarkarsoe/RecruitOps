import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RolesPage } from './RolesPage';
import { roleService } from '../services/roleService';
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

    const submitBtn = screen.getByRole('button', { name: 'Create Role' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(roleService.createRole).toHaveBeenCalledWith({
        name: 'Talent Acquisition Partner',
        code: 'TALENT_ACQUISITION_PARTNER',
        description: undefined,
        permissionCodes: [],
      });
    });
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
