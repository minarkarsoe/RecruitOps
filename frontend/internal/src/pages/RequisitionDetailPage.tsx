import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { ApprovalDecisionRequest, ApprovalStep, RequisitionDetail } from '@recruitops/types';
import { api } from '../lib/api';
import { auth } from '../lib/auth';

function decisionBadge(decision: ApprovalStep['decision']) {
  const styles = {
    Waiting: 'bg-warning-100 text-warning-600',
    Approved: 'bg-success-100 text-success-600',
    Rejected: 'bg-danger-100 text-danger-600',
  };
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[12px] font-semibold ${styles[decision]}`}>
      {decision}
    </span>
  );
}

export function RequisitionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [item, setItem] = useState<RequisitionDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [comment, setComment] = useState('');
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
      setComment('');
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

  const decide = (approve: boolean) =>
    act(() => {
      const body: ApprovalDecisionRequest = { approve, comment: comment || null };
      return api<RequisitionDetail>(`/requisitions/${id}/decision`, {
        method: 'POST',
        body: JSON.stringify(body),
      });
    });

  // The current user is the active approver if the first Waiting step names them.
  // The backend still enforces this — showing the form to others just results in 404.
  const activeStep = item?.approvals.find(a => a.decision === 'Waiting');
  const canDecide = item?.status === 'PendingApproval' &&
    activeStep?.approverUserId === session?.userId;

  // Cancelling is the requester's own withdrawal, or a company-wide role overriding it.
  // Mirrors RequisitionService.CancelAsync — the backend is still the authority.
  const isOwnerOrCompanyWide =
    item?.requestedByUserId === session?.userId ||
    session?.role === 'Admin' ||
    session?.role === 'HrDirector';

  const canCancel =
    (item?.status === 'Draft' || item?.status === 'PendingApproval') && isOwnerOrCompanyWide;

  // Only a Draft is editable — after submit, approvers are deciding on these contents.
  const canEdit = item?.status === 'Draft' && isOwnerOrCompanyWide;

  if (error && !item) return <p role="alert" className="text-danger-600">{error}</p>;
  if (!item) return <p className="text-ink-600">Loading…</p>;

  return (
    <>
      <header className="mb-6">
        <Link to="/requisitions" className="mb-2 inline-block text-[13px] text-primary-600 hover:underline">
          ← Back to requisitions
        </Link>
        <div className="flex items-center gap-3">
          <h1 className="font-display text-2xl font-bold">{item.title}</h1>
          <StatusPill status={item.status} />
          {canEdit && (
            <Link
              to={`/requisitions/${item.id}/edit`}
              className="ml-auto text-[13px] font-semibold text-primary-600 hover:underline"
            >
              Edit draft
            </Link>
          )}
        </div>
        <p className="mt-1 text-[13px] text-ink-600">{item.departmentName}</p>
      </header>

      {error && <p role="alert" className="mb-4 text-[15px] text-danger-600">{error}</p>}

      <div className="space-y-6">
        {/* ── Core details ── */}
        <Card>
          <dl className="grid grid-cols-2 gap-4 text-[15px]">
            <div>
              <dt className="text-[13px] text-ink-600">Headcount</dt>
              <dd className="font-mono">{item.headcount}</dd>
            </div>
            <div>
              <dt className="text-[13px] text-ink-600">Salary budget</dt>
              <dd className="font-mono">
                {item.salaryBudget != null ? item.salaryBudget.toLocaleString() : '—'}
              </dd>
            </div>
            <div>
              <dt className="text-[13px] text-ink-600">Submitted</dt>
              <dd>{item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : '—'}</dd>
            </div>
            <div>
              <dt className="text-[13px] text-ink-600">Decided</dt>
              <dd>{item.decidedAt ? new Date(item.decidedAt).toLocaleDateString() : '—'}</dd>
            </div>
          </dl>
        </Card>

        {/* ── Job description ── */}
        <Card>
          <h2 className="mb-3 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
            Job description
          </h2>
          <div className="whitespace-pre-wrap text-[15px] leading-relaxed">
            {item.jobDescription}
          </div>
        </Card>

        {/* ── Approval timeline ── */}
        {item.approvals.length > 0 && (
          <Card>
            <h2 className="mb-4 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
              Approval timeline
            </h2>
            <ol className="space-y-3">
              {item.approvals.map((step) => (
                <li key={step.sequence} className="flex items-start gap-4">
                  <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-surface-50 text-[12px] font-bold text-ink-600">
                    {step.sequence}
                  </span>
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold">{step.label}</span>
                      {decisionBadge(step.decision)}
                    </div>
                    {step.decidedAt && (
                      <p className="mt-0.5 text-[12px] text-ink-400">
                        {new Date(step.decidedAt).toLocaleString()}
                      </p>
                    )}
                    {step.comment && (
                      <p className="mt-1 rounded-sm bg-surface-50 p-2 text-[13px] italic text-ink-600">
                        "{step.comment}"
                      </p>
                    )}
                  </div>
                </li>
              ))}
            </ol>
          </Card>
        )}

        {/* ── Actions ── */}
        {item.status === 'Draft' && (
          <Card>
            <h2 className="mb-3 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
              Submit for approval
            </h2>
            <p className="mb-4 text-[15px] text-ink-600">
              This routes the requisition through your company's approval chain.
            </p>
            <Button onClick={submit} disabled={busy}>
              {busy ? 'Submitting…' : 'Submit for approval'}
            </Button>
          </Card>
        )}

        {canDecide && (
          <Card>
            <h2 className="mb-3 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
              Decision — {item.awaitingApprovalFrom ?? 'pending'}
            </h2>
            <p className="mb-4 text-[13px] text-ink-600">
              Only the named approver for the current step may decide. The backend enforces this.
            </p>
            <label htmlFor="comment" className="mb-1 block text-[13px] font-semibold">
              Comment <span className="font-normal text-ink-400">(optional)</span>
            </label>
            <textarea
              id="comment" rows={3} value={comment} onChange={(e) => setComment(e.target.value)}
              className="mb-4 w-full rounded-sm border border-line-200 p-3 focus:outline-none focus:ring-2 focus:ring-primary-600"
            />
            <div className="flex gap-3">
              <Button onClick={() => decide(true)} disabled={busy}>Approve</Button>
              <Button variant="danger" onClick={() => decide(false)} disabled={busy}>Reject</Button>
            </div>
          </Card>
        )}

        {canCancel && (
          <Card>
            <h2 className="mb-3 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
              Withdraw
            </h2>
            <p className="mb-4 text-[15px] text-ink-600">
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
