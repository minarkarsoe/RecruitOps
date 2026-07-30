import { api } from '../lib/api';
import type {
  UserListItem,
  UserDetail,
  PagedResult,
  UserQueryParameters,
  CreateUserRequest,
  UpdateUserRequest,
} from '@recruitops/types';

export const userService = {
  getUsers(params: UserQueryParameters = {}): Promise<PagedResult<UserListItem>> {
    const query = new URLSearchParams();
    if (params.page !== undefined) query.set('page', params.page.toString());
    if (params.pageSize !== undefined) query.set('pageSize', params.pageSize.toString());
    if (params.search) query.set('search', params.search);
    if (params.roleId) query.set('roleId', params.roleId);
    if (params.isActive !== undefined && params.isActive !== null) {
      query.set('isActive', params.isActive.toString());
    }
    const queryString = query.toString();
    return api<PagedResult<UserListItem>>(`/users${queryString ? `?${queryString}` : ''}`);
  },

  getUserById(id: string): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}`);
  },

  createUser(req: CreateUserRequest): Promise<UserDetail> {
    return api<UserDetail>('/users', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  updateUser(id: string, req: UpdateUserRequest): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    });
  },

  deactivateUser(id: string): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}/deactivate`, {
      method: 'PUT',
    });
  },

  reactivateUser(id: string): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}/reactivate`, {
      method: 'PUT',
    });
  },
};
