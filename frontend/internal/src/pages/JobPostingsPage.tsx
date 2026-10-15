import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { JobPostingListItem, RequisitionListItem } from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';

export function JobPostingsPage() {
  const [postings, setPostings] = useState<JobPostingListItem[]>([]);
  const [publishable, setPublishable] = useState<RequisitionListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const session = auth.get();

  async function load() {
    const list = await api<JobPostingListItem[]>('/jobpostings');
    setPostings(list);

    // Approved requisitions that don't have a posting yet — the actual to-do list for a
    // recruiter. Showing all approved requisitions would mean re-reading the postings list
    // by eye to work out which ones are already done.
    const requisitions = await api<RequisitionListItem[]>('/requisitions');
    const used = new Set(list.map((p) => p.requisitionId));
    setPublishable(requisitions.filter((r) => r.status === 'Approved' && !used.has(r.id)));
  }

  useEffect(() => {
    load().catch((e) => setError(e instanceof Error ? e.message : 'Could not load postings.'));
  }, []);

  async function createFrom(requisitionId: string) {
    setBusy(true);
    setError(null);
    try {
      await api('/jobpostings', { method: 'POST', body: JSON.stringify({ requisitionId }) });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not create the posting.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h1 className="mb-6 text-xl font-semibold tracking-tight">Job postings</h1>
      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      {publishable.length > 0 && (
        <div className="mb-6">
        <Card>
          <h2 className="mb-1 text-base font-semibold">
            Approved and waiting to be advertised
          </h2>
          <p className="mb-4 text-sm text-ink-600">
            The business has signed off on these. Creating a posting copies the approved
            title and description, which you can then rewrite for candidates.
          </p>
          <ul className="divide-y divide-line">
            {publishable.map((r) => (
              <li key={r.id} className="flex items-center justify-between py-3">
                <div>
                  <p className="font-semibold">{r.title}</p>
                  <p className="text-sm text-ink-600">
                    {r.departmentName} · {r.headcount} {r.headcount === 1 ? 'head' : 'heads'}
                  </p>
                </div>
                {hasPermission(session, 'permission:postings:postings:create') && (
                  <Button onClick={() => createFrom(r.id)} disabled={busy}>
                    Create posting
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </Card>
        </div>
      )}

      <Card>
        {postings.length === 0 ? (
          <p className="text-md text-ink-600">
            No postings yet. They start from an approved requisition.
          </p>
        ) : (
          <table className="w-full text-left text-base">
            <thead>
              <tr className="border-b border-line bg-canvas text-left text-sm">
                <th className="px-4 py-2.5 font-medium text-ink-600">Title</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Department</th>
                <th className="px-4 py-2.5 font-medium text-ink-600">Status</th>
                <th className="px-4 py-2.5 font-medium text-ink-600 text-right">Applicants</th>
              </tr>
            </thead>
            <tbody>
              {postings.map((p) => (
                <tr key={p.id} className="border-b border-line last:border-0">
                  <td className="py-3">
                    <Link to={`/jobpostings/${p.id}`} className="font-semibold text-brand-700 hover:underline">
                      {p.title}
                    </Link>
                  </td>
                  <td className="py-3 text-ink-600">{p.departmentName}</td>
                  <td className="py-3"><StatusPill status={p.status} /></td>
                  <td className="py-3 text-right font-mono">{p.applicationCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </>
  );
}
