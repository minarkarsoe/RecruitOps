import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PermissionMatrixGrid } from './PermissionMatrixGrid';
import { mockPermissionsGrouped } from '../test/rbacFixtures';

describe('PermissionMatrixGrid', () => {
  it('renders modules, features and operation checkboxes', () => {
    render(
      <PermissionMatrixGrid
        modules={mockPermissionsGrouped}
        selectedPermissionCodes={['permission:users:users:read']}
      />
    );

    expect(screen.getByText('Permission Matrix')).toBeInTheDocument();
    expect(screen.getAllByText('users')[0]).toBeInTheDocument();
    expect(screen.getAllByText('roles')[0]).toBeInTheDocument();
    expect(screen.getAllByText('requisitions')[0]).toBeInTheDocument();
    expect(screen.getByText('Approve Requisitions')).toBeInTheDocument();
  });

  it('triggers onChange when toggling individual checkbox', () => {
    const handleChange = vi.fn();
    render(
      <PermissionMatrixGrid
        modules={mockPermissionsGrouped}
        selectedPermissionCodes={['permission:users:users:read']}
        onChange={handleChange}
      />
    );

    // Find checkbox for Create Users
    const createCheckbox = screen.getByTitle('Create Users (permission:users:users:create)');
    fireEvent.click(createCheckbox);

    expect(handleChange).toHaveBeenCalledWith([
      'permission:users:users:read',
      'permission:users:users:create',
    ]);
  });

  it('disables all checkboxes when in read-only mode or system role mode', () => {
    render(
      <PermissionMatrixGrid
        modules={mockPermissionsGrouped}
        selectedPermissionCodes={['permission:users:users:read']}
        isSystemRole={true}
      />
    );

    expect(screen.getByText(/System Protected Role/)).toBeInTheDocument();

    const readCheckbox = screen.getByTitle('Read Users (permission:users:users:read)');
    expect(readCheckbox).toBeDisabled();
  });

  it('toggles all permissions globally when clicking Select All', () => {
    const handleChange = vi.fn();
    render(
      <PermissionMatrixGrid
        modules={mockPermissionsGrouped}
        selectedPermissionCodes={[]}
        onChange={handleChange}
      />
    );

    const selectAllBtn = screen.getByText('Select All Permissions');
    fireEvent.click(selectAllBtn);

    expect(handleChange).toHaveBeenCalledWith([
      'permission:users:users:read',
      'permission:users:users:create',
      'permission:users:users:update',
      'permission:users:users:delete',
      'permission:roles:roles:read',
      'permission:roles:roles:create',
      'permission:roles:roles:update',
      'permission:roles:roles:delete',
      'permission:requisitions:requisitions:read',
      'permission:requisitions:requisitions:approve',
    ]);
  });
});
