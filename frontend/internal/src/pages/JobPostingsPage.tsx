import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { JobPostingListItem, RequisitionListItem } from '@recruitops/types';
import { api } from '../lib/api';

export function JobPostingsPage() {
  const [postings, setPostings] = useState<JobPostingListItem[]>([]);
  const [publishable, setPublishable] = useState<RequisitionListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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
      <h1 className="mb-6 font-display text-2xl font-bold">Job postings</h1>
      {error && <p role="alert" className="mb-4 text-[15px] text-danger-600">{error}</p>}

      {publishable.length > 0 && (
        <div className="mb-6">
        <Card>
          <h2 className="mb-1 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
            Approved and waiting to be advertised
          </h2>
          <p className="mb-4 text-[13px] text-ink-600">
            The business has signed off on these. Creating a posting copies the approved
            title and description, which you can then rewrite for candidates.
          </p>
          <ul className="divide-y divide-line-200">
            {publishable.map((r) => (
              <li key={r.id} className="flex items-center justify-between py-3">
                <div>
                  <p className="font-semibold">{r.title}</p>
                  <p className="text-[13px] text-ink-600">
                    {r.departmentName} · {r.headcount} {r.headcount === 1 ? 'head' : 'heads'}
                  </p>
                </div>
                <Button onClick={() => createFrom(r.id)} disabled={busy}>
                  Create posting
                </Button>
              </li>
            ))}
          </ul>
        </Card>
        </div>
      )}

      <Card>
        {postings.length === 0 ? (
          <p className="text-[15px] text-ink-600">
            No postings yet. They start from an approved requisition.
          </p>
        ) : (
          <table className="w-full text-left text-[15px]">
            <thead>
              <tr className="border-b border-line-200 text-[13px] uppercase tracking-wide text-ink-600">
                <th className="py-2">Title</th>
                <th className="py-2">Department</th>
                <th className="py-2">Status</th>
                <th className="py-2 text-right">Applicants</th>
              </tr>
            </thead>
            <tbody>
              {postings.map((p) => (
                <tr key={p.id} className="border-b border-line-200 last:border-0">
                  <td className="py-3">
                    <Link to={`/jobpostings/${p.id}`} className="font-semibold text-primary-600 hover:underline">
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
