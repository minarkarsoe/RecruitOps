import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { ApprovalDecisionRequest, ApprovalStep, RequisitionDetail } from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';

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

/**
 * Names the approver who closed a step on someone else's behalf (ADR-0024).
 *
 * The API sends `decidedByUserId` as an id, not a name. Rather than a second round-trip, the
 * decider is resolved against the round's own steps: a senior can only skip ahead from a step
 * they were themselves named on, so their label is already on this page. Falls back to a
 * neutral phrase if it cannot be resolved — an unattributed approval is still a true statement,
 * whereas a guessed name would not be.
 */
function deciderLabel(step: ApprovalStep, roundSteps: ApprovalStep[]): string {
  const decider = roundSteps.find(s => s.approverUserId === step.decidedByUserId);
  return decider ? decider.label : 'a later approver';
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

  const revise = () =>
    act(() => api<RequisitionDetail>(`/requisitions/${id}/revise`, { method: 'POST' }));

  // Only the latest round is live; earlier rounds are history (ADR-0023).
  const currentRound = item?.approvals.reduce((max, a) => Math.max(max, a.round), 0) ?? 0;
  const currentRoundSteps = item?.approvals.filter(a => a.round === currentRound) ?? [];

  // Oldest round first, so the rejection reads above the revision it produced.
  const rounds = [...new Set(item?.approvals.map(a => a.round) ?? [])]
    .sort((a, b) => a - b)
    .map(round => ({
      round,
      steps: (item?.approvals ?? [])
        .filter(a => a.round === round)
        .sort((a, b) => a.sequence - b.sequence),
    }));

  // The caller's OWN waiting step, which since ADR-0024 need not be the active one — a later
  // step outranks an earlier one, so being further down the chain is enough to approve.
  // The backend still enforces all of this; showing the form to others just results in 404.
  const myStep = currentRoundSteps.find(
    a => a.decision === 'Waiting' && a.approverUserId === session?.userId);
  const activeStep = currentRoundSteps.find(a => a.decision === 'Waiting');

  const canDecide = item?.status === 'PendingApproval' && myStep !== undefined &&
    hasPermission(session, 'permission:requisitions:requisitions:approve');

  // Rejecting stays bound to the active step. A senior may approve on a junior's behalf, but
  // rejecting for them would end the requisition before they ever saw it (ADR-0024).
  const canRejectHere = canDecide && myStep?.sequence === activeStep?.sequence;

  // Steps this action would close on someone else's behalf — named, so the approver knows
  // what they are signing for rather than discovering it afterwards.
  const stepsClosedOnBehalf = canDecide
    ? currentRoundSteps.filter(
        a => a.decision === 'Waiting' && a.sequence < (myStep?.sequence ?? 0))
    : [];

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
            {/* Grouped by round: a resubmission after a rejection opens a new round beside
                the old one, and the rejection that caused it is the most useful thing on the
                page (ADR-0023). Flattening them would interleave the two chains by sequence. */}
            {rounds.map(({ round, steps }) => (
              <section key={round} className={round === currentRound ? '' : 'opacity-70'}>
                {rounds.length > 1 && (
                  <h3 className="mb-2 text-[12px] font-semibold uppercase tracking-wide text-ink-400">
                    Attempt {round}
                    {round === currentRound ? ' — current' : ' — superseded'}
                  </h3>
                )}
                <ol className="mb-5 space-y-3 last:mb-0">
                  {steps.map((step) => (
                    // Keyed on round AND sequence: sequences repeat across rounds, so
                    // sequence alone collides once a requisition has been resubmitted.
                    <li key={`${step.round}-${step.sequence}`} className="flex items-start gap-4">
                      <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-surface-50 text-[12px] font-bold text-ink-600">
                        {step.sequence}
                      </span>
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <span className="font-semibold">{step.label}</span>
                          {decisionBadge(step.decision)}
                        </div>
                        {step.decidedByUserId && (
                          <p className="mt-0.5 text-[12px] font-medium text-ink-600">
                            Approved by {deciderLabel(step, steps)} on behalf of {step.label}
                          </p>
                        )}
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
              </section>
            ))}
          </Card>
        )}

        {/* ── Actions ── */}
        {item.status === 'Draft' && hasPermission(session, 'permission:requisitions:requisitions:update') && (
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
              Decision — {myStep?.label ?? 'pending'}
            </h2>
            {stepsClosedOnBehalf.length > 0 ? (
              <p className="mb-4 rounded-sm bg-warning-100 p-3 text-[13px] text-ink-600">
                This is not your turn yet — it is waiting on{' '}
                <strong>{stepsClosedOnBehalf.map(s => s.label).join(', ')}</strong>. Approving
                closes {stepsClosedOnBehalf.length === 1 ? 'that step' : 'those steps'} as well as
                your own, and the timeline will record that you decided{' '}
                {stepsClosedOnBehalf.length === 1 ? 'it' : 'them'}. To reject, wait for{' '}
                {activeStep?.label} — rejecting on their behalf would end the requisition before
                they ever saw it.
              </p>
            ) : (
              <p className="mb-4 text-[13px] text-ink-600">
                Only the named approver for this step, or a more senior one, may decide. The
                backend enforces this.
              </p>
            )}
            <label htmlFor="comment" className="mb-1 block text-[13px] font-semibold">
              Comment <span className="font-normal text-ink-400">(optional)</span>
            </label>
            <textarea
              id="comment" rows={3} value={comment} onChange={(e) => setComment(e.target.value)}
              className="mb-4 w-full rounded-sm border border-line-200 p-3 focus:outline-none focus:ring-2 focus:ring-primary-600"
            />
            <div className="flex gap-3">
              <Button onClick={() => decide(true)} disabled={busy}>
                {stepsClosedOnBehalf.length > 0
                  ? `Approve ${stepsClosedOnBehalf.length + 1} steps`
                  : 'Approve'}
              </Button>
              {canRejectHere && (
                <Button variant="danger" onClick={() => decide(false)} disabled={busy}>Reject</Button>
              )}
            </div>
          </Card>
        )}

        {canRevise && (
          <Card>
            <h2 className="mb-3 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
              Revise and resubmit
            </h2>
            <p className="mb-4 text-[15px] text-ink-600">
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
