import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { RequisitionListItem } from '@recruitops/types';
import { api } from '../lib/api';
import { auth, isDepartmentScoped } from '../lib/auth';

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
          <h1 className="font-display text-2xl font-bold">Requisitions</h1>
          {session && isDepartmentScoped(session.role) && (
            <p className="mt-1 text-[13px] text-ink-400">
              Showing your department&rsquo;s requisitions only.
            </p>
          )}
        </div>
        <Link to="/requisitions/new"><Button>New requisition</Button></Link>
      </header>

      {error && <p role="alert" className="mb-4 text-[15px] text-danger-600">{error}</p>}

      {items === null && !error && <p className="text-ink-600">Loading…</p>}

      {items?.length === 0 && (
        <Card>
          <div className="py-6 text-center">
            <h3 className="text-base font-semibold">No requisitions yet</h3>
            <p className="mt-1 text-[13px] text-ink-600">
              Raise one to start the approval process.
            </p>
          </div>
        </Card>
      )}

      {items && items.length > 0 && (
        <div className="overflow-hidden rounded-md border border-line-200 bg-surface-0 shadow-card">
          <table className="w-full text-left">
            <thead>
              <tr className="bg-surface-50 text-[11px] uppercase tracking-wide text-ink-600">
                <th className="px-4 py-3 font-semibold">Position</th>
                <th className="px-4 py-3 font-semibold">Department</th>
                <th className="px-4 py-3 font-semibold">Heads</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="px-4 py-3 font-semibold">Awaiting</th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id} className="border-t border-line-200 hover:bg-surface-50">
                  <td className="px-4 py-3">
                    <Link to={`/requisitions/${r.id}`} className="font-semibold text-primary-700 hover:underline">
                      {r.title}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-ink-600">{r.departmentName}</td>
                  <td className="px-4 py-3 font-mono text-[13px]">{r.headcount}</td>
                  <td className="px-4 py-3"><StatusPill status={r.status} /></td>
                  <td className="px-4 py-3 text-[13px] text-ink-600">
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
