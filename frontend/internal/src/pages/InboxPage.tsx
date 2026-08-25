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
        <h1 className="text-xl font-semibold tracking-tight">Approval inbox</h1>
        <p className="mt-1 text-sm text-ink-400">
          Requisitions with a step assigned to you, in submission order. Some may still be
          waiting on someone below you in the chain — you can approve those ahead of them.
        </p>
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}
      {items === null && !error && <p className="text-ink-600">Loading…</p>}

      {items?.length === 0 && (
        <Card>
          <div className="py-6 text-center">
            <h3 className="text-base font-semibold">Nothing to review</h3>
            <p className="mt-1 text-sm text-ink-600">
              You're all caught up — no requisitions are waiting for your approval.
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
                <th className="px-4 py-2.5 font-medium text-ink-600">Your step</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Waiting on</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Submitted</th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id} className="border-t border-line hover:bg-canvas">
                  <td className="px-4 py-3">
                    <Link to={`/requisitions/${r.id}`}
                      className="font-semibold text-brand-700 hover:underline">
                      {r.title}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-ink-600">{r.departmentName}</td>
                  <td className="px-4 py-3 font-mono text-sm">{r.headcount}</td>
                  <td className="px-4 py-3"><StatusPill status={r.status} /></td>
                  {/* Two columns, because since ADR-0024 they can differ: a senior sees their
                      own step here while the chain still waits on a junior below them. The old
                      single column showed awaitingApprovalFrom under a "Your step" heading,
                      which would name someone else entirely. */}
                  <td className="px-4 py-3 text-sm font-semibold text-ink-900">
                    {r.yourStepLabel ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-sm">
                    {r.awaitingApprovalFrom === null ? (
                      <span className="text-ink-400">—</span>
                    ) : r.yourStepLabel === r.awaitingApprovalFrom ? (
                      <span className="font-semibold text-warn-700">Your turn</span>
                    ) : (
                      <span className="text-ink-600">
                        {r.awaitingApprovalFrom}
                        <span className="ml-2 rounded-full bg-canvas px-2 py-0.5 text-2xs font-semibold text-ink-400">
                          not your turn yet
                        </span>
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-sm text-ink-400">
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
