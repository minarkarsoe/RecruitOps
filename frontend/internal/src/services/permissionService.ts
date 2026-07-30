import { api } from '../lib/api';
import type { PermissionModule } from '@recruitops/types';

export const permissionService = {
  getPermissions(): Promise<PermissionModule[]> {
    return api<PermissionModule[]>('/permissions');
  },
};
