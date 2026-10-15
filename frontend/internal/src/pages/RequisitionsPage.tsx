import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { RequisitionListItem } from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission, isDepartmentScoped } from '../lib/auth';

export function RequisitionsPage() {
  const [items, setItems] = useState<RequisitionListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const session = auth.get();

  useEffect(() => {
    api<RequisitionListItem[]>('/requisitions')
      .then(setItems)
      .catch((e) => setError(e.message));
  }, []);

  return (
    <>
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Requisitions</h1>
          {session && isDepartmentScoped(session.role) && (
            <p className="mt-1 text-sm text-ink-400">
              Showing your department&rsquo;s requisitions only.
            </p>
          )}
        </div>
        {hasPermission(session, 'permission:requisitions:requisitions:create') && (
          <Link to="/requisitions/new"><Button>New requisition</Button></Link>
        )}
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      {items === null && !error && <p className="text-ink-600">Loading…</p>}

      {items?.length === 0 && (
        <Card>
          <div className="py-6 text-center">
            <h3 className="text-base font-semibold">No requisitions yet</h3>
            <p className="mt-1 text-sm text-ink-600">
              Raise one to start the approval process.
            </p>
          </div>
        </Card>
      )}

      {items && items.length > 0 && (
        <div className="overflow-hidden rounded-md border border-line bg-white shadow-card">
          <table className="w-full text-left text-base">
            <thead>
              <tr className="border-b border-line bg-canvas text-left text-sm">
                <th className="px-4 py-2.5 font-medium text-ink-600">Position</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Department</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Heads</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Status</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Awaiting</th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id} className="border-t border-line hover:bg-canvas">
                  <td className="px-4 py-3">
                    <Link to={`/requisitions/${r.id}`} className="font-semibold text-brand-700 hover:underline">
                      {r.title}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-ink-600">{r.departmentName}</td>
                  <td className="px-4 py-3 font-mono text-sm">{r.headcount}</td>
                  <td className="px-4 py-3"><StatusPill status={r.status} /></td>
                  <td className="px-4 py-3 text-sm text-ink-600">
                    {r.awaitingApprovalFrom ?? '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
