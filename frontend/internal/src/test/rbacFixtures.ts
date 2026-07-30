import type { PermissionModule, RoleListItem, UserListItem, RoleDetail, PagedResult } from '@recruitops/types';

export const mockPermissionsGrouped: PermissionModule[] = [
  {
    module: 'users',
    features: [
      {
        feature: 'users',
        permissions: [
          { id: '1', code: 'permission:users:users:read', name: 'Read Users', description: 'Read user directory', module: 'users', feature: 'users', action: 'read' },
          { id: '2', code: 'permission:users:users:create', name: 'Create Users', description: 'Create user account', module: 'users', feature: 'users', action: 'create' },
          { id: '3', code: 'permission:users:users:update', name: 'Update Users', description: 'Update user account', module: 'users', feature: 'users', action: 'update' },
          { id: '4', code: 'permission:users:users:delete', name: 'Delete Users', description: 'Delete user account', module: 'users', feature: 'users', action: 'delete' },
        ],
      },
    ],
  },
  {
    module: 'roles',
    features: [
      {
        feature: 'roles',
        permissions: [
          { id: '5', code: 'permission:roles:roles:read', name: 'Read Roles', description: 'Read system & custom roles', module: 'roles', feature: 'roles', action: 'read' },
          { id: '6', code: 'permission:roles:roles:create', name: 'Create Roles', description: 'Create custom roles', module: 'roles', feature: 'roles', action: 'create' },
          { id: '7', code: 'permission:roles:roles:update', name: 'Update Roles', description: 'Update role permissions', module: 'roles', feature: 'roles', action: 'update' },
          { id: '8', code: 'permission:roles:roles:delete', name: 'Delete Roles', description: 'Delete custom roles', module: 'roles', feature: 'roles', action: 'delete' },
        ],
      },
    ],
  },
  {
    module: 'requisitions',
    features: [
      {
        feature: 'requisitions',
        permissions: [
          { id: '9', code: 'permission:requisitions:requisitions:read', name: 'Read Requisitions', description: 'Read requisitions', module: 'requisitions', feature: 'requisitions', action: 'read' },
          { id: '10', code: 'permission:requisitions:requisitions:approve', name: 'Approve Requisitions', description: 'Approve or reject requisitions', module: 'requisitions', feature: 'requisitions', action: 'approve' },
        ],
      },
    ],
  },
];

export const mockRoles: RoleListItem[] = [
  {
    id: 'role-admin',
    name: 'Admin',
    code: 'Admin',
    description: 'System Administrator',
    isSystemRole: true,
    isSuperAdmin: false,
    isActive: true,
    userCount: 2,
    permissionCount: 25,
  },
  {
    id: 'role-custom',
    name: 'Custom Recruiter',
    code: 'CUSTOM_RECRUITER',
    description: 'Customized recruiter role',
    isSystemRole: false,
    isSuperAdmin: false,
    isActive: true,
    userCount: 0,
    permissionCount: 12,
  },
];

export const mockRoleDetail: RoleDetail = {
  id: 'role-custom',
  name: 'Custom Recruiter',
  code: 'CUSTOM_RECRUITER',
  description: 'Customized recruiter role',
  isSystemRole: false,
  isSuperAdmin: false,
  isActive: true,
  assignedPermissions: mockPermissionsGrouped[0].features[0].permissions,
  assignedPermissionCodes: ['permission:users:users:read', 'permission:users:users:create'],
  userCount: 0,
  createdAt: '2026-07-30T00:00:00Z',
  updatedAt: null,
};

export const mockUserList: UserListItem[] = [
  {
    id: 'usr-1',
    email: 'admin@recruitops.io',
    displayName: 'Admin User',
    role: 'Admin',
    roleId: 'role-admin',
    roleName: 'Admin',
    isActive: true,
    createdAt: '2026-07-30T00:00:00Z',
  },
  {
    id: 'usr-2',
    email: 'recruiter@recruitops.io',
    displayName: 'Jane Recruiter',
    role: 'CUSTOM_RECRUITER',
    roleId: 'role-custom',
    roleName: 'Custom Recruiter',
    isActive: true,
    createdAt: '2026-07-30T00:00:00Z',
  },
];

export const mockPagedUsers: PagedResult<UserListItem> = {
  items: mockUserList,
  page: 1,
  pageSize: 20,
  totalCount: 2,
  totalPages: 1,
};
