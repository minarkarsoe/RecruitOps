import { useState } from 'react';
import { auth, isSuperAdmin } from '../lib/auth';
import type { TenantInfo } from '@recruitops/types';

interface TenantSwitcherBarProps {
  onTenantChange?: (tenantId: string, tenantName: string) => void;
}

const DEFAULT_TENANTS: TenantInfo[] = [
  { id: 'tenant-default', name: 'Default Enterprise Tenant', code: 'DEFAULT', isActive: true },
  { id: 'tenant-acme', name: 'Acme Corporation', code: 'ACME', isActive: true },
  { id: 'tenant-globex', name: 'Globex Recruitment Agency', code: 'GLOBEX', isActive: true },
  { id: 'tenant-stark', name: 'Stark Industries', code: 'STARK', isActive: true },
];

export function TenantSwitcherBar({ onTenantChange }: TenantSwitcherBarProps) {
  const session = auth.get();
  const [isOpen, setIsOpen] = useState(false);
  const [customTenantId, setCustomTenantId] = useState('');

  if (!session || !isSuperAdmin(session)) {
    return null;
  }

  const currentTenantName = session.activeTenantName || 'Default Tenant';
  const currentTenantId = session.activeTenantId || 'tenant-default';

  function handleSelectTenant(id: string, name: string) {
    auth.setActiveTenant(id, name);
    setIsOpen(false);
    if (onTenantChange) {
      onTenantChange(id, name);
    } else {
      window.location.reload();
    }
  }

  function handleCustomSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!customTenantId.trim()) return;
    const name = `Tenant (${customTenantId.trim()})`;
    handleSelectTenant(customTenantId.trim(), name);
  }

  return (
    <div className="bg-amber-50 border-b border-amber-300 px-4 py-2 text-xs flex items-center justify-between text-amber-950 font-medium">
      <div className="flex items-center gap-2">
        <span className="inline-flex items-center gap-1 font-bold px-2 py-0.5 rounded bg-amber-600 text-white text-[11px] uppercase tracking-wide">
          👑 Super-Admin Context
        </span>
        <span>
          Viewing Tenant: <strong className="font-semibold text-amber-900">{currentTenantName}</strong>{' '}
          <span className="text-amber-700 text-[11px]">({currentTenantId})</span>
        </span>
      </div>

      <div className="relative">
        <button
          onClick={() => setIsOpen(!isOpen)}
          className="inline-flex items-center gap-1 text-amber-800 hover:text-amber-950 hover:underline font-semibold bg-amber-100/80 px-2.5 py-1 rounded border border-amber-300"
        >
          Switch Tenant Context ▾
        </button>

        {isOpen && (
          <div className="absolute right-0 mt-1 w-64 rounded-md bg-white p-2 shadow-lg ring-1 ring-black ring-opacity-5 z-50 text-ink-900">
            <div className="px-2 py-1 text-[11px] font-semibold text-ink-500 uppercase tracking-wider border-b border-line-200">
              Select Tenant Context
            </div>
            <div className="py-1 max-h-48 overflow-y-auto">
              {DEFAULT_TENANTS.map((t) => (
                <button
                  key={t.id}
                  onClick={() => handleSelectTenant(t.id, t.name)}
                  className={`w-full text-left px-2 py-1.5 text-xs rounded flex justify-between items-center ${
                    currentTenantId === t.id
                      ? 'bg-primary-50 text-primary-700 font-semibold'
                      : 'hover:bg-surface-100 text-ink-800'
                  }`}
                >
                  <span>{t.name}</span>
                  <span className="text-[10px] text-ink-400 font-mono">{t.code}</span>
                </button>
              ))}
            </div>
            <form onSubmit={handleCustomSubmit} className="border-t border-line-200 pt-2 mt-1">
              <label className="block text-[11px] text-ink-600 mb-1">Custom Tenant ID:</label>
              <div className="flex gap-1">
                <input
                  type="text"
                  value={customTenantId}
                  onChange={(e) => setCustomTenantId(e.target.value)}
                  placeholder="e.g. tenant-999"
                  className="w-full text-xs px-2 py-1 border border-line-300 rounded"
                />
                <button
                  type="submit"
                  className="bg-amber-600 text-white text-xs px-2 py-1 rounded font-medium hover:bg-amber-700 shrink-0"
                >
                  Set
                </button>
              </div>
            </form>
          </div>
        )}
      </div>
    </div>
  );
}
