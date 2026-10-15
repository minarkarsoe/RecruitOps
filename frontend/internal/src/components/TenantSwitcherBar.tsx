import { useCallback, useEffect, useState } from 'react';
import { auth, isSuperAdmin } from '../lib/auth';
import { api } from '../lib/api';
import type { TenantInfo } from '@recruitops/types';

/**
 * Super-admin tenant switcher — sets the `X-Tenant-Id` the API reads (see `CurrentTenant`).
 *
 * ⚠️ **This used to offer four hard-coded companies**: `tenant-default`, `tenant-acme`,
 * `tenant-globex`, `tenant-stark`. None of them existed, and none of them could — a tenant id is
 * a `Company.Id`, which is a GUID, so every one of those strings failed to parse the moment the
 * server started reading the header. The switcher looked complete and could not switch to
 * anything. It now asks the server which companies there are.
 *
 * A single-tenant install (the normal deployment — one company per database, ADR-0004) gets one
 * row here, which is the honest answer rather than a menu of places that are not there.
 */
export function TenantSwitcherBar({
  onTenantChange,
}: {
  onTenantChange?: (tenantId: string, tenantName: string) => void;
}) {
  const session = auth.get();
  const superAdmin = !!session && isSuperAdmin(session);

  const [isOpen, setIsOpen] = useState(false);
  const [tenants, setTenants] = useState<TenantInfo[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      setTenants(await api<TenantInfo[]>('/tenants'));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load the company list.');
      setTenants([]);
    }
  }, []);

  // Only when the menu is opened. A super-admin looking at a screen has not asked which other
  // companies exist, and this is on every authenticated page.
  useEffect(() => {
    if (isOpen && tenants === null) load();
  }, [isOpen, tenants, load]);

  if (!superAdmin) return null;

  const currentTenantId = session?.activeTenantId;
  const currentTenantName =
    session?.activeTenantName
    ?? tenants?.find((t) => t.id === currentTenantId)?.name
    ?? 'this company';

  function handleSelectTenant(id: string, name: string) {
    auth.setActiveTenant(id, name);
    setIsOpen(false);
    if (onTenantChange) {
      onTenantChange(id, name);
    } else {
      // Every open screen is showing another company's rows now, so a reload is the honest way
      // to get there — refetching piecemeal would leave two companies on screen at once.
      window.location.reload();
    }
  }

  return (
    <div className="flex items-center justify-between gap-3 border-b border-warn-100 bg-warn-50 px-5 py-2 text-sm text-warn-700">
      <div className="flex min-w-0 items-center gap-2">
        <span className="inline-flex h-5 shrink-0 items-center rounded-full bg-warn-500 px-2 text-2xs font-medium uppercase tracking-wider text-ink-900">
          Super admin
        </span>
        <span className="truncate">
          Viewing <strong className="font-medium">{currentTenantName}</strong>
          {currentTenantId && (
            <span className="ml-1.5 font-mono text-xs text-warn-700/80">{currentTenantId}</span>
          )}
        </span>
      </div>

      <div className="relative shrink-0">
        <button
          type="button"
          aria-expanded={isOpen}
          onClick={() => setIsOpen(!isOpen)}
          className="h-7 rounded-md border border-warn-500/40 bg-white px-2.5 text-sm font-medium
            text-warn-700 transition-colors hover:border-warn-500"
        >
          Switch company
        </button>

        {isOpen && (
          <div
            role="group"
            aria-label="Companies in this database"
            className="absolute right-0 z-50 mt-1 w-72 rounded-lg border border-line bg-white p-1.5 text-ink-900 shadow-overlay"
          >
            <p className="px-2 py-1 text-2xs font-medium uppercase tracking-wider text-ink-500">
              Companies in this database
            </p>

            {tenants === null && (
              <div className="space-y-1 p-2">
                <span className="skeleton block h-7 w-full" />
                <span className="skeleton block h-7 w-full" />
              </div>
            )}

            {error && (
              <p role="alert" className="px-2 py-2 text-sm text-critical-700">
                {error}
              </p>
            )}

            {tenants?.length === 0 && !error && (
              <p className="px-2 py-2 text-sm text-ink-600">
                No other company is installed here. One database per company is the normal
                deployment.
              </p>
            )}

            <div className="max-h-56 overflow-y-auto">
              {tenants?.map((t) => (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => handleSelectTenant(t.id, t.name)}
                  className={`flex w-full items-center justify-between gap-2 rounded px-2 py-1.5 text-left text-base transition-colors ${
                    currentTenantId === t.id
                      ? 'bg-brand-50 font-medium text-brand-800'
                      : 'text-ink-800 hover:bg-canvas'
                  }`}
                >
                  <span className="truncate">{t.name}</span>
                  <span className="shrink-0 font-mono text-xs text-ink-500">{t.code}</span>
                </button>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
