import { useEffect, useState } from 'react';
import type { RoleListItem, RoleDetail, PermissionModule } from '@recruitops/types';
import { roleService } from '../services/roleService';
import { PermissionMatrixGrid } from '../components/PermissionMatrixGrid';

import { auth, hasPermission } from '../lib/auth';

export function RolesPage() {
  const currentSession = auth.get();
  const [roles, setRoles] = useState<RoleListItem[]>([]);
  const [modules, setModules] = useState<PermissionModule[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modal State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState<'create' | 'edit' | 'view'>('view');
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);

  // Form State
  const [formName, setFormName] = useState('');
  const [formCode, setFormCode] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formPermissionCodes, setFormPermissionCodes] = useState<string[]>([]);
  const [formIsActive, setFormIsActive] = useState(true);
  const [isSystemRoleModal, setIsSystemRoleModal] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [successToast, setSuccessToast] = useState<string | null>(null);

  // Delete modal state
  const [roleToDelete, setRoleToDelete] = useState<RoleListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    setError(null);
    try {
      const [rolesData, permsData] = await Promise.all([
        roleService.getRoles(),
        roleService.getPermissions(),
      ]);
      setRoles(rolesData);
      setModules(permsData);
    } catch (err: any) {
      setError(err.message || 'Failed to load roles and permissions.');
    } finally {
      setLoading(false);
    }
  }

  function showToast(msg: string) {
    setSuccessToast(msg);
    setTimeout(() => setSuccessToast(null), 4000);
  }

  function openCreateModal() {
    setModalMode('create');
    setSelectedRoleId(null);
    setFormName('');
    setFormCode('');
    setFormDescription('');
    setFormPermissionCodes([]);
    setFormIsActive(true);
    setIsSystemRoleModal(false);
    setFormError(null);
    setIsModalOpen(true);
  }

  async function openEditOrViewModal(roleItem: RoleListItem, mode: 'edit' | 'view') {
    setFormError(null);
    setSelectedRoleId(roleItem.id);
    setModalMode(mode);
    setIsSystemRoleModal(roleItem.isSystemRole);
    setFormName(roleItem.name);
    setFormCode(roleItem.code);
    setFormDescription(roleItem.description || '');
    setFormIsActive(roleItem.isActive);

    try {
      const detail: RoleDetail = await roleService.getRoleById(roleItem.id);
      setFormPermissionCodes(detail.assignedPermissionCodes || []);
      setIsModalOpen(true);
    } catch (err: any) {
      setError(err.message || 'Failed to load role details.');
    }
  }

  function handleNameChange(name: string) {
    setFormName(name);
    if (modalMode === 'create') {
      const generatedCode = name
        .toUpperCase()
        .trim()
        .replace(/[^A-Z0-9]/g, '_');
      setFormCode(generatedCode);
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!formName.trim()) {
      setFormError('Role name is required.');
      return;
    }

    setSaving(true);
    setFormError(null);
    try {
      if (modalMode === 'create') {
        await roleService.createRole({
          name: formName.trim(),
          code: formCode.trim() || undefined,
          description: formDescription.trim() || undefined,
          permissionCodes: formPermissionCodes,
        });
        showToast('Custom role created successfully.');
      } else if (modalMode === 'edit' && selectedRoleId) {
        await roleService.updateRole(selectedRoleId, {
          name: formName.trim(),
          description: formDescription.trim() || undefined,
          isActive: formIsActive,
          permissionCodes: formPermissionCodes,
        });
        showToast('Role permissions updated successfully.');
      }
      setIsModalOpen(false);
      loadData();
    } catch (err: any) {
      setFormError(err.message || 'Failed to save role.');
    } finally {
      setSaving(false);
    }
  }

  async function handleDeleteConfirm() {
    if (!roleToDelete) return;
    setDeleting(true);
    try {
      await roleService.deleteRole(roleToDelete.id);
      showToast(`Role "${roleToDelete.name}" deleted successfully.`);
      setRoleToDelete(null);
      loadData();
    } catch (err: any) {
      setError(err.message || 'Failed to delete role.');
      setRoleToDelete(null);
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div className="space-y-6">
      {/* Toast Notification */}
      {successToast && (
        <div className="fixed bottom-4 right-4 z-50 bg-primary-700 text-white px-4 py-3 rounded-lg shadow-lg text-sm font-medium flex items-center gap-2">
          <span>✓</span>
          <span>{successToast}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold text-ink-900">Role Builder & Permissions</h1>
          <p className="text-sm text-ink-600">Manage system roles and create granular custom permission matrices</p>
        </div>
        {hasPermission(currentSession, 'permission:roles:roles:create') && (
          <button
            onClick={openCreateModal}
            className="inline-flex items-center gap-2 rounded-md bg-primary-600 px-4 py-2 text-sm font-semibold text-white hover:bg-primary-700 shadow-sm"
          >
            + Create Custom Role
          </button>
        )}
      </div>

      {error && (
        <div className="rounded-md bg-danger-50 p-4 border border-danger-200 text-sm text-danger-700">
          {error}
        </div>
      )}

      {loading ? (
        <div className="p-12 text-center text-sm text-ink-500">Loading roles and permission matrix...</div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {roles.map((roleItem) => (
            <div
              key={roleItem.id}
              className="border border-line-200 rounded-lg p-5 bg-white shadow-sm hover:shadow-md transition-shadow flex flex-col justify-between"
            >
              <div>
                <div className="flex items-start justify-between gap-2 mb-2">
                  <div>
                    <h3 className="font-semibold text-base text-ink-900">{roleItem.name}</h3>
                    <p className="font-mono text-xs text-ink-500">{roleItem.code}</p>
                  </div>
                  <div className="flex flex-col items-end gap-1">
                    {roleItem.isSystemRole ? (
                      <span className="inline-block px-2 py-0.5 rounded text-[11px] font-semibold bg-surface-200 text-ink-700">
                        System Role
                      </span>
                    ) : (
                      <span className="inline-block px-2 py-0.5 rounded text-[11px] font-semibold bg-primary-100 text-primary-800">
                        Custom Role
                      </span>
                    )}
                    {roleItem.isSuperAdmin && (
                      <span className="inline-block px-2 py-0.5 rounded text-[10px] font-bold bg-amber-100 text-amber-900">
                        Super-Admin
                      </span>
                    )}
                  </div>
                </div>

                <p className="text-xs text-ink-600 line-clamp-2 min-h-[32px] mb-4">
                  {roleItem.description || 'No description provided.'}
                </p>

                <div className="flex items-center gap-4 text-xs text-ink-500 border-t border-line-100 pt-3 mb-4">
                  <div>
                    <span className="font-semibold text-ink-900">{roleItem.userCount}</span> assigned users
                  </div>
                  <div>
                    <span className="font-semibold text-ink-900">{roleItem.permissionCount}</span> permissions
                  </div>
                </div>
              </div>

              <div className="flex items-center justify-end gap-2 border-t border-line-200 pt-3">
                {roleItem.isSystemRole ? (
                  <button
                    onClick={() => openEditOrViewModal(roleItem, 'view')}
                    className="text-xs font-semibold text-primary-600 hover:text-primary-800 px-2 py-1 rounded hover:bg-primary-50"
                  >
                    View Matrix
                  </button>
                ) : (
                  <>
                    {hasPermission(currentSession, 'permission:roles:roles:update') ? (
                      <button
                        onClick={() => openEditOrViewModal(roleItem, 'edit')}
                        className="text-xs font-semibold text-primary-600 hover:text-primary-800 px-2 py-1 rounded hover:bg-primary-50"
                      >
                        Edit Matrix
                      </button>
                    ) : (
                      <button
                        onClick={() => openEditOrViewModal(roleItem, 'view')}
                        className="text-xs font-semibold text-primary-600 hover:text-primary-800 px-2 py-1 rounded hover:bg-primary-50"
                      >
                        View Matrix
                      </button>
                    )}
                    {hasPermission(currentSession, 'permission:roles:roles:delete') && (
                      <button
                        onClick={() => setRoleToDelete(roleItem)}
                        disabled={roleItem.userCount > 0}
                        title={roleItem.userCount > 0 ? 'Cannot delete role assigned to active users' : 'Delete Role'}
                        className="text-xs font-semibold text-danger-600 hover:text-danger-800 disabled:opacity-40 disabled:cursor-not-allowed px-2 py-1 rounded hover:bg-danger-50"
                      >
                        Delete
                      </button>
                    )}
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Role Form Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 overflow-y-auto">
          <div className="relative w-full max-w-4xl bg-white rounded-xl shadow-2xl overflow-hidden my-8 max-h-[90vh] flex flex-col">
            <div className="bg-surface-50 px-6 py-4 border-b border-line-200 flex items-center justify-between shrink-0">
              <div>
                <h2 className="text-lg font-bold text-ink-900">
                  {modalMode === 'create'
                    ? 'Create Custom Role'
                    : modalMode === 'edit'
                    ? `Edit Role: ${formName}`
                    : `View Role: ${formName}`}
                </h2>
                <p className="text-xs text-ink-500">
                  {isSystemRoleModal
                    ? 'System roles are read-only and pre-configured'
                    : 'Define role metadata and assign functional permissions'}
                </p>
              </div>
              <button
                onClick={() => setIsModalOpen(false)}
                className="text-ink-400 hover:text-ink-700 text-lg font-bold"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-6">
              {formError && (
                <div className="p-3 bg-danger-50 border border-danger-200 rounded text-xs text-danger-700">
                  {formError}
                </div>
              )}

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-ink-700 mb-1">
                    Role Name <span className="text-danger-600">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    disabled={modalMode === 'view' || isSystemRoleModal}
                    value={formName}
                    onChange={(e) => handleNameChange(e.target.value)}
                    placeholder="e.g. Talent Acquisition Partner"
                    className="w-full text-sm px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500 disabled:bg-surface-100"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-ink-700 mb-1">Role Code</label>
                  <input
                    type="text"
                    disabled={modalMode !== 'create'}
                    value={formCode}
                    onChange={(e) => setFormCode(e.target.value)}
                    placeholder="AUTO_GENERATED_CODE"
                    className="w-full text-sm font-mono px-3 py-2 border border-line-300 rounded-md bg-surface-50 text-ink-700 disabled:opacity-75"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-ink-700 mb-1">Description</label>
                <textarea
                  rows={2}
                  disabled={modalMode === 'view' || isSystemRoleModal}
                  value={formDescription}
                  onChange={(e) => setFormDescription(e.target.value)}
                  placeholder="Describe the scope and responsibilities of this role..."
                  className="w-full text-sm px-3 py-2 border border-line-300 rounded-md focus:ring-1 focus:ring-primary-500 disabled:bg-surface-100"
                />
              </div>

              {/* Permission Matrix Component */}
              <div className="border-t border-line-200 pt-4">
                <PermissionMatrixGrid
                  modules={modules}
                  selectedPermissionCodes={formPermissionCodes}
                  onChange={setFormPermissionCodes}
                  disabled={modalMode === 'view'}
                  isSystemRole={isSystemRoleModal}
                />
              </div>

              <div className="border-t border-line-200 pt-4 flex items-center justify-end gap-3 shrink-0">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 text-sm font-semibold text-ink-600 hover:text-ink-800"
                >
                  {modalMode === 'view' ? 'Close' : 'Cancel'}
                </button>
                {modalMode !== 'view' && !isSystemRoleModal && (
                  <button
                    type="submit"
                    disabled={saving}
                    className="px-4 py-2 text-sm font-semibold text-white bg-primary-600 hover:bg-primary-700 rounded-md shadow-sm disabled:opacity-50"
                  >
                    {saving ? 'Saving...' : modalMode === 'create' ? 'Create Role' : 'Save Changes'}
                  </button>
                )}
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {roleToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-lg max-w-md w-full p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-bold text-ink-900">Confirm Role Deletion</h3>
            <p className="text-sm text-ink-600">
              Are you sure you want to delete custom role <strong>{roleToDelete.name}</strong> ({roleToDelete.code})? This action cannot be undone.
            </p>
            <div className="flex justify-end gap-3 pt-2">
              <button
                onClick={() => setRoleToDelete(null)}
                className="px-4 py-2 text-sm font-semibold text-ink-600 hover:text-ink-800"
              >
                Cancel
              </button>
              <button
                onClick={handleDeleteConfirm}
                disabled={deleting}
                className="px-4 py-2 text-sm font-semibold text-white bg-danger-600 hover:bg-danger-700 rounded-md shadow-sm disabled:opacity-50"
              >
                {deleting ? 'Deleting...' : 'Delete Role'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
