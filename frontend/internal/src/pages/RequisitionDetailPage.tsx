import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { ApprovalDecisionRequest, RequisitionDetail } from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';
import { ApprovalDecisionCard, ApprovalTimeline } from '../components/ApprovalRail';

export function RequisitionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [item, setItem] = useState<RequisitionDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const session = auth.get();

  useEffect(() => {
    if (!id) return;
    api<RequisitionDetail>(`/requisitions/${id}`)
      .then(setItem)
      .catch((e) => setError(e.message));
  }, [id]);

  async function act(run: () => Promise<RequisitionDetail>) {
    setBusy(true);
    setError(null);
    try {
      setItem(await run());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Action failed.');
    } finally {
      setBusy(false);
    }
  }

  const submit = () =>
    act(() => api<RequisitionDetail>(`/requisitions/${id}/submit`, { method: 'POST' }));

  const cancel = () => {
    if (!confirm('Withdraw this requisition? This cannot be undone.')) return;
    return act(() => api<RequisitionDetail>(`/requisitions/${id}/cancel`, { method: 'POST' }));
  };

  const decide = (approve: boolean, comment: string | null) =>
    act(() => {
      const body: ApprovalDecisionRequest = { approve, comment };
      return api<RequisitionDetail>(`/requisitions/${id}/decision`, {
        method: 'POST',
        body: JSON.stringify(body),
      });
    });

  const revise = () =>
    act(() => api<RequisitionDetail>(`/requisitions/${id}/revise`, { method: 'POST' }));

  // The decision card shows itself only to someone holding a waiting step of the live round
  // (ADR-0024); the backend enforces all of this — showing it to others would just 404.
  const canDecide = item?.status === 'PendingApproval' &&
    hasPermission(session, 'permission:requisitions:requisitions:approve');

  // Cancelling is the requester's own withdrawal, or a company-wide role overriding it.
  // Mirrors RequisitionService.CancelAsync — the backend is still the authority.
  const isOwnerOrCompanyWide =
    item?.requestedByUserId === session?.userId ||
    session?.role === 'Admin' ||
    session?.role === 'HrDirector';

  const canCancel =
    (item?.status === 'Draft' || item?.status === 'PendingApproval') &&
    isOwnerOrCompanyWide &&
    hasPermission(session, 'permission:requisitions:requisitions:delete');

  // Only a Draft is editable — after submit, approvers are deciding on these contents.
  // Since ADR-0023 a Draft is no longer necessarily un-submitted: it may be a rejected
  // requisition returned for revision. Both are editable, so this gate is unchanged.
  const canEdit =
    item?.status === 'Draft' &&
    isOwnerOrCompanyWide &&
    hasPermission(session, 'permission:requisitions:requisitions:update');

  // Rejected reopens for the requester (ADR-0023). Approved and Cancelled stay terminal.
  const canRevise =
    item?.status === 'Rejected' &&
    isOwnerOrCompanyWide &&
    hasPermission(session, 'permission:requisitions:requisitions:update');

  if (error && !item) return <p role="alert" className="text-critical-700">{error}</p>;
  if (!item) return <p className="text-ink-600">Loading…</p>;

  return (
    <>
      <header className="mb-6">
        <Link to="/requisitions" className="mb-2 inline-block text-sm text-brand-700 hover:underline">
          ← Back to requisitions
        </Link>
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold tracking-tight">{item.title}</h1>
          <StatusPill status={item.status} />
          {canEdit && (
            <Link
              to={`/requisitions/${item.id}/edit`}
              className="ml-auto text-sm font-semibold text-brand-700 hover:underline"
            >
              Edit draft
            </Link>
          )}
        </div>
        <p className="mt-1 text-sm text-ink-600">{item.departmentName}</p>
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      <div className="space-y-6">
        {/* ── Core details ── */}
        <Card>
          <dl className="grid grid-cols-2 gap-4 text-md">
            <div>
              <dt className="text-sm text-ink-600">Headcount</dt>
              <dd className="font-mono">{item.headcount}</dd>
            </div>
            <div>
              <dt className="text-sm text-ink-600">Salary budget</dt>
              <dd className="font-mono">
                {item.salaryBudget != null ? item.salaryBudget.toLocaleString() : '—'}
              </dd>
            </div>
            <div>
              <dt className="text-sm text-ink-600">Submitted</dt>
              <dd>{item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : '—'}</dd>
            </div>
            <div>
              <dt className="text-sm text-ink-600">Decided</dt>
              <dd>{item.decidedAt ? new Date(item.decidedAt).toLocaleDateString() : '—'}</dd>
            </div>
          </dl>
        </Card>

        {/* ── Job description ── */}
        <Card>
          <h2 className="mb-3 text-base font-semibold">
            Job description
          </h2>
          <div className="whitespace-pre-wrap text-md leading-relaxed">
            {item.jobDescription}
          </div>
        </Card>

        {/* ── Approval timeline ── */}
        {item.approvals.length > 0 && (
          <Card>
            <h2 className="mb-4 text-base font-semibold">
              Approval timeline
            </h2>
            <ApprovalTimeline approvals={item.approvals} />
          </Card>
        )}

        {/* ── Actions ── */}
        {item.status === 'Draft' && hasPermission(session, 'permission:requisitions:requisitions:update') && (
          <Card>
            <h2 className="mb-3 text-base font-semibold">
              Submit for approval
            </h2>
            <p className="mb-4 text-md text-ink-600">
              This routes the requisition through your company's approval chain.
            </p>
            <Button onClick={submit} disabled={busy}>
              {busy ? 'Submitting…' : 'Submit for approval'}
            </Button>
          </Card>
        )}

        {canDecide && (
          <ApprovalDecisionCard
            approvals={item.approvals} userId={session?.userId} noun="requisition" busy={busy}
            onDecide={(approve, comment) => void decide(approve, comment)}
          />
        )}

        {canRevise && (
          <Card>
            <h2 className="mb-3 text-base font-semibold">
              Revise and resubmit
            </h2>
            <p className="mb-4 text-md text-ink-600">
              Returns this to Draft so you can address the feedback above and submit it again.
              The rejection stays on the record — resubmitting starts a fresh round of approvals
              beside it, not over it.
            </p>
            <Button onClick={revise} disabled={busy}>
              {busy ? 'Working…' : 'Revise this requisition'}
            </Button>
          </Card>
        )}

        {canCancel && (
          <Card>
            <h2 className="mb-3 text-base font-semibold">
              Withdraw
            </h2>
            <p className="mb-4 text-md text-ink-600">
              Cancels this requisition. Approval steps already recorded are kept for the audit
              trail, and it disappears from approvers' inboxes.
            </p>
            <Button variant="danger" onClick={cancel} disabled={busy}>
              {busy ? 'Working…' : 'Cancel requisition'}
            </Button>
          </Card>
        )}
      </div>
    </>
  );
}
