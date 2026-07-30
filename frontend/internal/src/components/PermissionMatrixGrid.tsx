import { useMemo } from 'react';
import type { PermissionModule, PermissionFeature, Permission } from '@recruitops/types';

export interface PermissionMatrixGridProps {
  modules: PermissionModule[];
  selectedPermissionCodes: string[];
  onChange?: (codes: string[]) => void;
  disabled?: boolean;
  isSystemRole?: boolean;
}

const ACTION_ORDER = ['read', 'create', 'update', 'delete'];

export function PermissionMatrixGrid({
  modules,
  selectedPermissionCodes,
  onChange,
  disabled = false,
  isSystemRole = false,
}: PermissionMatrixGridProps) {
  const isReadOnly = disabled || isSystemRole;
  const selectedSet = useMemo(() => new Set(selectedPermissionCodes), [selectedPermissionCodes]);

  // Flatten all available permission codes across all modules
  const allPermissionCodes = useMemo(() => {
    const codes: string[] = [];
    modules.forEach((mod) => {
      mod.features.forEach((feat) => {
        feat.permissions.forEach((p) => {
          codes.push(p.code);
        });
      });
    });
    return codes;
  }, [modules]);

  const isAllSelected = useMemo(() => {
    if (allPermissionCodes.length === 0) return false;
    return allPermissionCodes.every((c) => selectedSet.has(c));
  }, [allPermissionCodes, selectedSet]);

  function handleToggleCode(code: string) {
    if (isReadOnly || !onChange) return;
    const nextSet = new Set(selectedSet);
    if (nextSet.has(code)) {
      nextSet.delete(code);
    } else {
      nextSet.add(code);
    }
    onChange(Array.from(nextSet));
  }

  function handleToggleAllGlobal() {
    if (isReadOnly || !onChange) return;
    if (isAllSelected) {
      onChange([]);
    } else {
      onChange(allPermissionCodes);
    }
  }

  function handleToggleModule(mod: PermissionModule) {
    if (isReadOnly || !onChange) return;
    const modCodes: string[] = [];
    mod.features.forEach((f) => {
      f.permissions.forEach((p) => modCodes.push(p.code));
    });
    const allModSelected = modCodes.every((c) => selectedSet.has(c));

    const nextSet = new Set(selectedSet);
    if (allModSelected) {
      modCodes.forEach((c) => nextSet.delete(c));
    } else {
      modCodes.forEach((c) => nextSet.add(c));
    }
    onChange(Array.from(nextSet));
  }

  function handleToggleFeature(feat: PermissionFeature) {
    if (isReadOnly || !onChange) return;
    const featCodes = feat.permissions.map((p) => p.code);
    const allFeatSelected = featCodes.every((c) => selectedSet.has(c));

    const nextSet = new Set(selectedSet);
    if (allFeatSelected) {
      featCodes.forEach((c) => nextSet.delete(c));
    } else {
      featCodes.forEach((c) => nextSet.add(c));
    }
    onChange(Array.from(nextSet));
  }

  return (
    <div className="space-y-6">
      {isSystemRole && (
        <div className="bg-amber-50 border border-amber-300 rounded-lg p-3 text-amber-900 text-xs flex items-center gap-2">
          <span className="text-base">🛡️</span>
          <span>
            <strong>System Protected Role</strong> — Pre-configured system roles cannot be modified. All permissions are shown in read-only mode.
          </span>
        </div>
      )}

      <div className="flex items-center justify-between border-b border-line-200 pb-3">
        <div>
          <h3 className="text-sm font-semibold text-ink-900">Permission Matrix</h3>
          <p className="text-xs text-ink-500">Configure access levels grouped by system module and feature</p>
        </div>
        {!isReadOnly && (
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleToggleAllGlobal}
              className="text-xs font-semibold text-primary-600 hover:text-primary-800"
            >
              {isAllSelected ? 'Deselect All Permissions' : 'Select All Permissions'}
            </button>
            <span className="text-xs text-ink-400">
              ({selectedSet.size} of {allPermissionCodes.length} selected)
            </span>
          </div>
        )}
      </div>

      <div className="space-y-4">
        {modules.map((mod) => {
          const modCodes: string[] = [];
          mod.features.forEach((f) => f.permissions.forEach((p) => modCodes.push(p.code)));
          const modSelectedCount = modCodes.filter((c) => selectedSet.has(c)).length;
          const isModAllSelected = modCodes.length > 0 && modSelectedCount === modCodes.length;

          return (
            <div key={mod.module} className="border border-line-200 rounded-lg overflow-hidden bg-white shadow-sm">
              <div className="bg-surface-50 px-4 py-3 border-b border-line-200 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  {!isReadOnly && (
                    <input
                      type="checkbox"
                      checked={isModAllSelected}
                      onChange={() => handleToggleModule(mod)}
                      disabled={isReadOnly}
                      className="h-4 w-4 rounded border-line-300 text-primary-600 focus:ring-primary-500"
                    />
                  )}
                  <span className="font-semibold text-ink-900 text-sm capitalize">
                    {mod.module.replace('_', ' ')}
                  </span>
                </div>
                <span className="text-xs text-ink-500 font-medium">
                  {modSelectedCount} / {modCodes.length} active
                </span>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="border-b border-line-200 bg-surface-0 text-ink-500 font-medium text-[11px] uppercase tracking-wider">
                      <th className="py-2.5 px-4 w-1/4">Feature</th>
                      <th className="py-2.5 px-2 text-center w-16">Read</th>
                      <th className="py-2.5 px-2 text-center w-16">Create</th>
                      <th className="py-2.5 px-2 text-center w-16">Update</th>
                      <th className="py-2.5 px-2 text-center w-16">Delete</th>
                      <th className="py-2.5 px-4">Special Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line-100">
                    {mod.features.map((feat) => {
                      const featCodes = feat.permissions.map((p) => p.code);
                      const isFeatAllSelected = featCodes.length > 0 && featCodes.every((c) => selectedSet.has(c));

                      const stdPerms: Record<string, Permission | undefined> = {};
                      const specialPerms: Permission[] = [];

                      feat.permissions.forEach((p) => {
                        const act = p.action.toLowerCase();
                        if (ACTION_ORDER.includes(act)) {
                          stdPerms[act] = p;
                        } else {
                          specialPerms.push(p);
                        }
                      });

                      return (
                        <tr key={feat.feature} className="hover:bg-surface-50 transition-colors">
                          <td className="py-3 px-4 font-medium text-ink-800">
                            <div className="flex items-center gap-2">
                              {!isReadOnly && (
                                <input
                                  type="checkbox"
                                  checked={isFeatAllSelected}
                                  onChange={() => handleToggleFeature(feat)}
                                  disabled={isReadOnly}
                                  className="h-3.5 w-3.5 rounded border-line-300 text-primary-600 focus:ring-primary-500"
                                  title="Toggle feature permissions"
                                />
                              )}
                              <span className="capitalize">{feat.feature.replace('_', ' ')}</span>
                            </div>
                          </td>

                          {ACTION_ORDER.map((act) => {
                            const p = stdPerms[act];
                            if (!p) {
                              return <td key={act} className="py-3 px-2 text-center text-ink-300">—</td>;
                            }
                            const isChecked = selectedSet.has(p.code);

                            return (
                              <td key={act} className="py-3 px-2 text-center">
                                <input
                                  type="checkbox"
                                  checked={isChecked}
                                  onChange={() => handleToggleCode(p.code)}
                                  disabled={isReadOnly}
                                  className="h-4 w-4 rounded border-line-300 text-primary-600 focus:ring-primary-500 disabled:opacity-50 cursor-pointer disabled:cursor-not-allowed"
                                  title={`${p.name} (${p.code})`}
                                />
                              </td>
                            );
                          })}

                          <td className="py-3 px-4">
                            {specialPerms.length === 0 ? (
                              <span className="text-ink-300">—</span>
                            ) : (
                              <div className="flex flex-wrap gap-3">
                                {specialPerms.map((p) => {
                                  const isChecked = selectedSet.has(p.code);
                                  return (
                                    <label
                                      key={p.code}
                                      className={`inline-flex items-center gap-1.5 px-2 py-1 rounded text-xs border ${
                                        isChecked
                                          ? 'bg-primary-50 border-primary-300 text-primary-800'
                                          : 'bg-white border-line-200 text-ink-700'
                                      } ${isReadOnly ? 'opacity-70 cursor-not-allowed' : 'cursor-pointer hover:border-primary-400'}`}
                                    >
                                      <input
                                        type="checkbox"
                                        checked={isChecked}
                                        onChange={() => handleToggleCode(p.code)}
                                        disabled={isReadOnly}
                                        className="h-3.5 w-3.5 rounded border-line-300 text-primary-600 focus:ring-primary-500"
                                      />
                                      <span className="font-medium">{p.name || p.action}</span>
                                    </label>
                                  );
                                })}
                              </div>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
