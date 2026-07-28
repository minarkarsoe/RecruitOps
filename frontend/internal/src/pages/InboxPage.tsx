import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Card, StatusPill } from '@recruitops/ui';
import type { RequisitionListItem } from '@recruitops/types';
import { api } from '../lib/api';

export function InboxPage() {
  const [items, setItems] = useState<RequisitionListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<RequisitionListItem[]>('/requisitions/inbox')
      .then(setItems)
      .catch((e) => setError(e.message));
  }, []);

  return (
    <>
      <header className="mb-6">
        <h1 className="font-display text-2xl font-bold">Approval inbox</h1>
        <p className="mt-1 text-[13px] text-ink-400">
          Requisitions waiting for your decision, in submission order.
        </p>
      </header>

      {error && <p role="alert" className="mb-4 text-[15px] text-danger-600">{error}</p>}
      {items === null && !error && <p className="text-ink-600">Loading…</p>}

      {items?.length === 0 && (
        <Card>
          <div className="py-6 text-center">
            <h3 className="text-base font-semibold">Nothing to review</h3>
            <p className="mt-1 text-[13px] text-ink-600">
              You're all caught up — no requisitions are waiting for your approval.
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
                <th className="px-4 py-3 font-semibold">Your step</th>
                <th className="px-4 py-3 font-semibold">Submitted</th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id} className="border-t border-line-200 hover:bg-surface-50">
                  <td className="px-4 py-3">
                    <Link to={`/requisitions/${r.id}`}
                      className="font-semibold text-primary-700 hover:underline">
                      {r.title}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-ink-600">{r.departmentName}</td>
                  <td className="px-4 py-3 font-mono text-[13px]">{r.headcount}</td>
                  <td className="px-4 py-3"><StatusPill status={r.status} /></td>
                  <td className="px-4 py-3 text-[13px] font-semibold text-warning-600">
                    {r.awaitingApprovalFrom ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-[13px] text-ink-400">
                    {r.submittedAt ? new Date(r.submittedAt).toLocaleDateString() : '—'}
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
