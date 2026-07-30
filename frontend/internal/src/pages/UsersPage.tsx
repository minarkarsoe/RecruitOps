import { useEffect, useState, useMemo } from 'react';
import type { UserListItem, RoleListItem, PagedResult } from '@recruitops/types';
import { userService } from '../services/userService';
import { roleService } from '../services/roleService';
import { auth, hasPermission } from '../lib/auth';

export function UsersPage() {
  const currentSession = auth.get();
  const currentUserId = currentSession?.userId;

  const [pagedData, setPagedData] = useState<PagedResult<UserListItem>>({
    items: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
  });

  const [roles, setRoles] = useState<RoleListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState<string>(''); // '', 'true', 'false'
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  // Modals state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [selectedUser, setSelectedUser] = useState<UserListItem | null>(null);

  // Create Form state
  const [createEmail, setCreateEmail] = useState('');
  const [createDisplayName, setCreateDisplayName] = useState('');
  const [createPassword, setCreatePassword] = useState('');
  const [createRoleId, setCreateRoleId] = useState('');
  const [createRoleName, setCreateRoleName] = useState('Recruiter');

  // Edit Form state
  const [editDisplayName, setEditDisplayName] = useState('');
  const [editRoleId, setEditRoleId] = useState('');

  // Safeguard modal state
  const [deactivateUserTarget, setDeactivateUserTarget] = useState<UserListItem | null>(null);
  const [deactivateWarning, setDeactivateWarning] = useState<string | null>(null);

  // State flags
  const [submitting, setSubmitting] = useState(false);
  const [modalError, setModalError] = useState<string | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Load roles list for filter & modal dropdown
  useEffect(() => {
    roleService
      .getRoles()
      .then(setRoles)
      .catch(() => {});
  }, []);

  // Fetch users whenever filters/pagination change
  useEffect(() => {
    loadUsers();
  }, [page, pageSize, searchQuery, roleFilter, activeFilter]);

  async function loadUsers() {
    setLoading(true);
    setError(null);
    try {
      const result = await userService.getUsers({
        page,
        pageSize,
        search: searchQuery.trim() || undefined,
        roleId: roleFilter || undefined,
        isActive: activeFilter === '' ? undefined : activeFilter === 'true',
      });
      setPagedData(result);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch user directory.');
    } finally {
      setLoading(false);
    }
  }

  function triggerToast(msg: string) {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 4000);
  }

  // Count active admin users in directory
  const activeAdminCount = useMemo(() => {
    return pagedData.items.filter(
      (u) => (u.role === 'Admin' || u.role === 'SuperAdmin') && u.isActive !== false
    ).length;
  }, [pagedData.items]);

  function openCreateModal() {
    setModalError(null);
    setCreateEmail('');
    setCreateDisplayName('');
    setCreatePassword('');
    setCreateRoleId(roles[0]?.id || '');
    setCreateRoleName(roles[0]?.name || 'Recruiter');
    setIsCreateModalOpen(true);
  }

  async function openEditModal(user: UserListItem) {
    setModalError(null);
    setSelectedUser(user);
    setEditDisplayName(user.displayName);
    setEditRoleId(user.roleId || '');
    setIsEditModalOpen(true);
  }

  async function handleCreateSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!createEmail.trim() || !createDisplayName.trim() || !createPassword.trim()) {
      setModalError('All required fields must be filled out.');
      return;
    }
    if (createPassword.length < 8) {
      setModalError('Password must be at least 8 characters long.');
      return;
    }

    setSubmitting(true);
    setModalError(null);
    try {
      const selectedRoleObj = roles.find((r) => r.id === createRoleId);
      await userService.createUser({
        email: createEmail.trim(),
        displayName: createDisplayName.trim(),
        password: createPassword,
        roleId: createRoleId || null,
        role: selectedRoleObj ? selectedRoleObj.code : createRoleName,
      });
      triggerToast('User account created successfully.');
      setIsCreateModalOpen(false);
      loadUsers();
    } catch (err: any) {
      setModalError(err.message || 'Failed to create user account.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleEditSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedUser) return;
    if (!editDisplayName.trim()) {
      setModalError('Display name is required.');
      return;
    }

    setSubmitting(true);
    setModalError(null);
    try {
      const selectedRoleObj = roles.find((r) => r.id === editRoleId);
      await userService.updateUser(selectedUser.id, {
        displayName: editDisplayName.trim(),
        roleId: editRoleId || null,
        role: selectedRoleObj ? selectedRoleObj.code : selectedUser.role,
      });
      triggerToast('User details updated successfully.');
      setIsEditModalOpen(false);
      loadUsers();
    } catch (err: any) {
      setModalError(err.message || 'Failed to update user.');
    } finally {
      setSubmitting(false);
    }
  }

  function handleDeactivateClick(user: UserListItem) {
    if (user.id === currentUserId) {
      return; // Safeguard caught in UI
    }

    const isUserAdmin = user.role === 'Admin' || user.role === 'SuperAdmin';
    if (isUserAdmin && activeAdminCount <= 1) {
      setDeactivateWarning('Cannot deactivate the last active Administrator account.');
    } else {
      setDeactivateWarning(null);
    }

    setDeactivateUserTarget(user);
  }

  async function confirmToggleDeactivate() {
    if (!deactivateUserTarget) return;
    setSubmitting(true);
    try {
      if (deactivateUserTarget.isActive !== false) {
        await userService.deactivateUser(deactivateUserTarget.id);
        triggerToast(`User ${deactivateUserTarget.displayName} deactivated.`);
      } else {
        await userService.reactivateUser(deactivateUserTarget.id);
        triggerToast(`User ${deactivateUserTarget.displayName} reactivated.`);
      }
      setDeactivateUserTarget(null);
      loadUsers();
    } catch (err: any) {
      setError(err.message || 'Failed to change user active status.');
      setDeactivateUserTarget(null);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-6">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed bottom-4 right-4 z-50 bg-primary-700 text-white px-4 py-3 rounded-lg shadow-lg text-sm font-medium flex items-center gap-2">
          <span>✓</span>
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold text-ink-900">User Directory</h1>
          <p className="text-sm text-ink-600">Manage organization user accounts, roles, and platform access</p>
        </div>
        {hasPermission(currentSession, 'permission:users:users:create') && (
          <button
            onClick={openCreateModal}
            className="inline-flex items-center gap-2 rounded-md bg-primary-600 px-4 py-2 text-sm font-semibold text-white hover:bg-primary-700 shadow-sm"
          >
            + Create User
          </button>
        )}
      </div>

      {/* Toolbar Filters */}
      <div className="bg-white border border-line-200 rounded-lg p-4 shadow-sm flex flex-col md:flex-row gap-4 items-center justify-between">
        <div className="flex flex-col md:flex-row gap-3 w-full md:w-auto items-center">
          <div className="relative w-full md:w-64">
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setPage(1);
              }}
              placeholder="Search email or name..."
              className="w-full text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500"
            />
          </div>

          <select
            value={roleFilter}
            onChange={(e) => {
              setRoleFilter(e.target.value);
              setPage(1);
            }}
            className="w-full md:w-44 text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500 bg-white"
          >
            <option value="">All Roles</option>
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>

          <select
            value={activeFilter}
            onChange={(e) => {
              setActiveFilter(e.target.value);
              setPage(1);
            }}
            className="w-full md:w-36 text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500 bg-white"
          >
            <option value="">All Statuses</option>
            <option value="true">Active Only</option>
            <option value="false">Inactive Only</option>
          </select>
        </div>

        <div className="text-xs text-ink-500 font-medium shrink-0">
          Showing {pagedData.items.length} of {pagedData.totalCount} users
        </div>
      </div>

      {error && (
        <div className="rounded-md bg-danger-50 p-4 border border-danger-200 text-sm text-danger-700">
          {error}
        </div>
      )}

      {/* User Directory Table */}
      <div className="bg-white border border-line-200 rounded-lg shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="border-b border-line-200 bg-surface-50 text-ink-500 font-medium text-[11px] uppercase tracking-wider">
                <th className="py-3 px-4">User</th>
                <th className="py-3 px-4">Role</th>
                <th className="py-3 px-4">Status</th>
                <th className="py-3 px-4">Created Date</th>
                <th className="py-3 px-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line-100">
              {loading ? (
                <tr>
                  <td colSpan={5} className="py-12 text-center text-ink-500">
                    Loading users directory...
                  </td>
                </tr>
              ) : pagedData.items.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-12 text-center text-ink-500">
                    No user accounts found matching your filters.
                  </td>
                </tr>
              ) : (
                pagedData.items.map((user) => {
                  const isSelf = user.id === currentUserId;
                  const isActive = user.isActive !== false;

                  return (
                    <tr key={user.id} className="hover:bg-surface-50 transition-colors">
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-full bg-primary-100 text-primary-700 flex items-center justify-center font-bold text-xs">
                            {user.displayName ? user.displayName.charAt(0).toUpperCase() : 'U'}
                          </div>
                          <div>
                            <div className="font-semibold text-ink-900 flex items-center gap-1.5">
                              <span>{user.displayName}</span>
                              {isSelf && (
                                <span className="text-[10px] bg-primary-50 text-primary-700 px-1.5 py-0.2 rounded border border-primary-200 font-medium">
                                  You
                                </span>
                              )}
                            </div>
                            <div className="text-ink-500 font-mono text-[11px]">{user.email}</div>
                          </div>
                        </div>
                      </td>

                      <td className="py-3 px-4">
                        <span className="inline-block px-2 py-0.5 rounded text-[11px] font-semibold bg-surface-100 text-ink-800 border border-line-200">
                          {user.roleName || user.role}
                        </span>
                      </td>

                      <td className="py-3 px-4">
                        {isActive ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                            Active
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-rose-50 text-rose-700 border border-rose-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-rose-500"></span>
                            Inactive
                          </span>
                        )}
                      </td>

                      <td className="py-3 px-4 text-ink-500">
                        {user.createdAt ? new Date(user.createdAt).toLocaleDateString() : '—'}
                      </td>

                      <td className="py-3 px-4 text-right">
                        <div className="flex items-center justify-end gap-2">
                          {hasPermission(currentSession, 'permission:users:users:update') && (
                            <button
                              onClick={() => openEditModal(user)}
                              className="text-xs font-semibold text-primary-600 hover:text-primary-800 px-2 py-1 rounded hover:bg-primary-50"
                            >
                              Edit
                            </button>
                          )}

                          {hasPermission(currentSession, 'permission:users:users:delete') && (
                            <button
                              onClick={() => handleDeactivateClick(user)}
                              disabled={isSelf}
                              title={isSelf ? 'You cannot deactivate your own account.' : undefined}
                              className={`text-xs font-semibold px-2 py-1 rounded ${
                                isSelf
                                  ? 'opacity-40 cursor-not-allowed text-ink-400'
                                  : isActive
                                  ? 'text-danger-600 hover:text-danger-800 hover:bg-danger-50'
                                  : 'text-emerald-600 hover:text-emerald-800 hover:bg-emerald-50'
                              }`}
                            >
                              {isActive ? 'Deactivate' : 'Reactivate'}
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination Bar */}
        <div className="px-4 py-3 border-t border-line-200 bg-surface-50 flex items-center justify-between text-xs text-ink-600">
          <div className="flex items-center gap-2">
            <span>Rows per page:</span>
            <select
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setPage(1);
              }}
              className="px-2 py-1 border border-line-300 rounded bg-white"
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>

          <div className="flex items-center gap-3">
            <span>
              Page {pagedData.page} of {pagedData.totalPages || 1}
            </span>
            <div className="flex gap-1">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="px-2.5 py-1 border border-line-300 rounded bg-white disabled:opacity-40 font-medium"
              >
                Previous
              </button>
              <button
                onClick={() => setPage((p) => Math.min(pagedData.totalPages || 1, p + 1))}
                disabled={page >= (pagedData.totalPages || 1)}
                className="px-2.5 py-1 border border-line-300 rounded bg-white disabled:opacity-40 font-medium"
              >
                Next
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Create User Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-xl shadow-2xl max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-line-200 pb-3">
              <h3 className="text-lg font-bold text-ink-900">Create New User</h3>
              <button
                onClick={() => setIsCreateModalOpen(false)}
                className="text-ink-400 hover:text-ink-700 text-lg font-bold"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleCreateSubmit} className="space-y-4">
              {modalError && (
                <div className="p-3 bg-danger-50 border border-danger-200 rounded text-xs text-danger-700">
                  {modalError}
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">
                  Email Address <span className="text-danger-600">*</span>
                </label>
                <input
                  type="email"
                  required
                  value={createEmail}
                  onChange={(e) => setCreateEmail(e.target.value)}
                  placeholder="colleague@company.com"
                  className="w-full text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">
                  Display Name <span className="text-danger-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={createDisplayName}
                  onChange={(e) => setCreateDisplayName(e.target.value)}
                  placeholder="Jane Doe"
                  className="w-full text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">
                  Password <span className="text-danger-600">*</span>
                </label>
                <input
                  type="password"
                  required
                  minLength={8}
                  value={createPassword}
                  onChange={(e) => setCreatePassword(e.target.value)}
                  placeholder="Minimum 8 characters"
                  className="w-full text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">
                  Assigned Role <span className="text-danger-600">*</span>
                </label>
                <select
                  value={createRoleId}
                  onChange={(e) => {
                    setCreateRoleId(e.target.value);
                    const selectedRoleObj = roles.find((r) => r.id === e.target.value);
                    if (selectedRoleObj) setCreateRoleName(selectedRoleObj.name);
                  }}
                  className="w-full text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500 bg-white"
                >
                  {roles.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name} {r.isSystemRole ? '(System)' : '(Custom)'}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-3 pt-2 border-t border-line-200">
                <button
                  type="button"
                  onClick={() => setIsCreateModalOpen(false)}
                  className="px-4 py-2 text-xs font-semibold text-ink-600 hover:text-ink-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-4 py-2 text-xs font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-md shadow-sm disabled:opacity-50"
                >
                  {submitting ? 'Creating...' : 'Create Account'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit User Modal */}
      {isEditModalOpen && selectedUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-xl shadow-2xl max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-line-200 pb-3">
              <h3 className="text-lg font-bold text-ink-900">Edit User Details</h3>
              <button
                onClick={() => setIsEditModalOpen(false)}
                className="text-ink-400 hover:text-ink-700 text-lg font-bold"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleEditSubmit} className="space-y-4">
              {modalError && (
                <div className="p-3 bg-danger-50 border border-danger-200 rounded text-xs text-danger-700">
                  {modalError}
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">Email Address</label>
                <input
                  type="text"
                  disabled
                  value={selectedUser.email}
                  className="w-full text-xs px-3 py-2 border border-line-300 rounded-md bg-surface-100 text-ink-500 font-mono"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">
                  Display Name <span className="text-danger-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={editDisplayName}
                  onChange={(e) => setEditDisplayName(e.target.value)}
                  className="w-full text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">Assigned Role</label>
                <select
                  value={editRoleId}
                  onChange={(e) => setEditRoleId(e.target.value)}
                  className="w-full text-xs px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500 bg-white"
                >
                  {roles.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name} {r.isSystemRole ? '(System)' : '(Custom)'}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex justify-end gap-3 pt-2 border-t border-line-200">
                <button
                  type="button"
                  onClick={() => setIsEditModalOpen(false)}
                  className="px-4 py-2 text-xs font-semibold text-ink-600 hover:text-ink-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-4 py-2 text-xs font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-md shadow-sm disabled:opacity-50"
                >
                  {submitting ? 'Saving...' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Deactivate/Reactivate Confirmation Dialog with Safeguard Warning */}
      {deactivateUserTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-bold text-ink-900">
              Confirm Account {deactivateUserTarget.isActive !== false ? 'Deactivation' : 'Reactivation'}
            </h3>

            {deactivateWarning ? (
              <div className="p-3 bg-amber-50 border border-amber-300 rounded text-xs text-amber-900 space-y-2">
                <div className="font-semibold flex items-center gap-1.5">
                  <span>⚠️</span> Safeguard Warning
                </div>
                <p>{deactivateWarning}</p>
              </div>
            ) : (
              <p className="text-sm text-ink-600">
                Are you sure you want to {deactivateUserTarget.isActive !== false ? 'deactivate' : 'reactivate'}{' '}
                <strong>{deactivateUserTarget.displayName}</strong> ({deactivateUserTarget.email})?
              </p>
            )}

            <div className="flex justify-end gap-3 pt-2">
              <button
                onClick={() => setDeactivateUserTarget(null)}
                className="px-4 py-2 text-xs font-semibold text-ink-600 hover:text-ink-800"
              >
                Cancel
              </button>
              <button
                onClick={confirmToggleDeactivate}
                disabled={submitting || !!deactivateWarning}
                className={`px-4 py-2 text-xs font-semibold text-white rounded-md shadow-sm disabled:opacity-50 ${
                  deactivateUserTarget.isActive !== false
                    ? 'bg-danger-600 hover:bg-danger-700'
                    : 'bg-emerald-600 hover:bg-emerald-700'
                }`}
              >
                {submitting
                  ? 'Processing...'
                  : deactivateUserTarget.isActive !== false
                  ? 'Confirm Deactivation'
                  : 'Confirm Reactivation'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
