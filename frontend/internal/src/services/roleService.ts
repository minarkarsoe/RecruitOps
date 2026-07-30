import { api } from '../lib/api';
import type {
  RoleListItem,
  RoleDetail,
  PermissionModule,
  CreateRoleRequest,
  UpdateRoleRequest,
} from '@recruitops/types';

export const roleService = {
  getPermissions(): Promise<PermissionModule[]> {
    return api<PermissionModule[]>('/permissions');
  },

  getRoles(): Promise<RoleListItem[]> {
    return api<RoleListItem[]>('/roles');
  },

  getRoleById(id: string): Promise<RoleDetail> {
    return api<RoleDetail>(`/roles/${id}`);
  },

  createRole(req: CreateRoleRequest): Promise<RoleDetail> {
    return api<RoleDetail>('/roles', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  updateRole(id: string, req: UpdateRoleRequest): Promise<RoleDetail> {
    return api<RoleDetail>(`/roles/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    });
  },

  deleteRole(id: string): Promise<void> {
    return api<void>(`/roles/${id}`, {
      method: 'DELETE',
    });
  },
};
