import { useState } from 'react';
import { Button, Card } from '@recruitops/ui';
import type { ApprovalStep } from '@recruitops/types';

/**
 * The approval rail — one device for requisitions (Module 1) and offers (Module 4), because the
 * rules behind it are one copy too (`ApprovalRail` in the domain, ADR-0030 §1). Two approvals that
 * look different is how they drift apart (`design/internal/offer-create.html`).
 */

function decisionBadge(decision: ApprovalStep['decision']) {
  const styles = {
    Waiting: 'bg-warn-50 text-warn-700',
    Approved: 'bg-positive-50 text-positive-700',
    Rejected: 'bg-critical-50 text-critical-700',
  };
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${styles[decision]}`}>
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

/** The live round, its rounds for display, and where the viewer stands in it. */
export function approvalState(approvals: ApprovalStep[], userId: string | undefined) {
  // Only the latest round is live; earlier rounds are history (ADR-0023).
  const currentRound = approvals.reduce((max, a) => Math.max(max, a.round), 0);
  const currentRoundSteps = approvals.filter(a => a.round === currentRound);

  // Oldest round first, so the rejection reads above the revision it produced.
  const rounds = [...new Set(approvals.map(a => a.round))]
    .sort((a, b) => a - b)
    .map(round => ({
      round,
      steps: approvals.filter(a => a.round === round).sort((a, b) => a.sequence - b.sequence),
    }));

  // The caller's OWN waiting step, which since ADR-0024 need not be the active one — a later
  // step outranks an earlier one, so being further down the chain is enough to approve.
  const myStep = currentRoundSteps.find(a => a.decision === 'Waiting' && a.approverUserId === userId);
  const activeStep = currentRoundSteps.find(a => a.decision === 'Waiting');

  return { currentRound, currentRoundSteps, rounds, myStep, activeStep };
}

export function ApprovalTimeline({ approvals }: { approvals: ApprovalStep[] }) {
  const { currentRound, rounds } = approvalState(approvals, undefined);
  return (
    <>
      {/* Grouped by round: a resubmission after a rejection opens a new round beside the old one,
          and the rejection that caused it is the most useful thing on the page (ADR-0023).
          Flattening them would interleave the two chains by sequence. */}
      {rounds.map(({ round, steps }) => (
        <section key={round} className={round === currentRound ? '' : 'opacity-70'}>
          {rounds.length > 1 && (
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">
              Attempt {round}
              {round === currentRound ? ' — current' : ' — superseded'}
            </h3>
          )}
          <ol className="mb-5 space-y-3 last:mb-0">
            {steps.map((step) => (
              // Keyed on round AND sequence: sequences repeat across rounds, so sequence alone
              // collides once something has been resubmitted.
              <li key={`${step.round}-${step.sequence}`} className="flex items-start gap-4">
                <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-canvas text-xs font-bold text-ink-600">
                  {step.sequence}
                </span>
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold">{step.label}</span>
                    {decisionBadge(step.decision)}
                  </div>
                  {step.decidedByUserId && (
                    <p className="mt-0.5 text-xs font-medium text-ink-600">
                      Approved by {deciderLabel(step, steps)} on behalf of {step.label}
                    </p>
                  )}
                  {step.decidedAt && (
                    <p className="mt-0.5 text-xs text-ink-400">
                      {new Date(step.decidedAt).toLocaleString()}
                    </p>
                  )}
                  {step.comment && (
                    <p className="mt-1 rounded-md bg-canvas p-2 text-sm italic text-ink-600">
                      "{step.comment}"
                    </p>
                  )}
                </div>
              </li>
            ))}
          </ol>
        </section>
      ))}
    </>
  );
}

interface ApprovalDecisionCardProps {
  approvals: ApprovalStep[];
  userId: string | undefined;
  /** What is being approved, as it reads mid-sentence: "requisition", "offer". */
  noun: string;
  busy: boolean;
  onDecide: (approve: boolean, comment: string | null) => void;
}

/** Shown only to someone holding a waiting step of the live round; the API enforces the same. */
export function ApprovalDecisionCard({ approvals, userId, noun, busy, onDecide }: ApprovalDecisionCardProps) {
  const [comment, setComment] = useState('');
  const { currentRoundSteps, myStep, activeStep } = approvalState(approvals, userId);
  if (!myStep) return null;

  // Rejecting stays bound to the active step. A senior may approve on a junior's behalf, but
  // rejecting for them would end it before they ever saw it (ADR-0024).
  const canRejectHere = myStep.sequence === activeStep?.sequence;

  // Steps this action would close on someone else's behalf — named, so the approver knows what
  // they are signing for rather than discovering it afterwards.
  const stepsClosedOnBehalf = currentRoundSteps.filter(
    a => a.decision === 'Waiting' && a.sequence < myStep.sequence);

  const decide = (approve: boolean) => onDecide(approve, comment || null);

  return (
    <Card>
      <h2 className="mb-3 text-base font-semibold">
        Decision — {myStep.label}
      </h2>
      {stepsClosedOnBehalf.length > 0 ? (
        <p className="mb-4 rounded-md bg-warn-50 p-3 text-sm text-ink-600">
          This is not your turn yet — it is waiting on{' '}
          <strong>{stepsClosedOnBehalf.map(s => s.label).join(', ')}</strong>. Approving
          closes {stepsClosedOnBehalf.length === 1 ? 'that step' : 'those steps'} as well as
          your own, and the timeline will record that you decided{' '}
          {stepsClosedOnBehalf.length === 1 ? 'it' : 'them'}. To reject, wait for{' '}
          {activeStep?.label} — rejecting on their behalf would end the {noun} before
          they ever saw it.
        </p>
      ) : (
        <p className="mb-4 text-sm text-ink-600">
          Only the named approver for this step, or a more senior one, may decide. The
          backend enforces this.
        </p>
      )}
      <label htmlFor="comment" className="mb-1 block text-sm font-semibold">
        Comment <span className="font-normal text-ink-400">(optional)</span>
      </label>
      <textarea
        id="comment" rows={3} value={comment} onChange={(e) => setComment(e.target.value)}
        className="mb-4 w-full rounded-md border border-line p-3 focus:outline-none focus:ring-2 focus:ring-brand-700"
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
  );
}
