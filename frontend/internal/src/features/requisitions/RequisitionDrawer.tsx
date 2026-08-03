import { useState } from 'react';
import {
  Badge,
  Button,
  Sheet,
  SheetBody,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  StatusPill,
} from '@recruitops/ui';
import type { ApprovalStep, RequisitionDetail } from '@recruitops/types';
import { auth, hasPermission } from '../../lib/auth';

export interface RequisitionDrawerProps {
  requisition: RequisitionDetail | null;
  isOpen: boolean;
  onClose: () => void;
  onSubmitForApproval?: (id: string) => Promise<void>;
  onDecide?: (id: string, approve: boolean, comment?: string) => Promise<void>;
  onCancel?: (id: string) => Promise<void>;
  onEdit?: (id: string) => void;
  isLoading?: boolean;
  actionBusy?: boolean;
  error?: string | null;
}

function decisionBadge(decision: ApprovalStep['decision']) {
  const variantMap: Record<ApprovalStep['decision'], 'warning' | 'success' | 'danger'> = {
    Waiting: 'warning',
    Approved: 'success',
    Rejected: 'danger',
  };
  return (
    <Badge variant={variantMap[decision]} size="sm">
      {decision}
    </Badge>
  );
}

export function RequisitionDrawer({
  requisition,
  isOpen,
  onClose,
  onSubmitForApproval,
  onDecide,
  onCancel,
  onEdit,
  isLoading = false,
  actionBusy = false,
  error = null,
}: RequisitionDrawerProps) {
  const [comment, setComment] = useState('');
  const session = auth.get();

  if (!isOpen) return null;

  const activeStep = requisition?.approvals.find((a) => a.decision === 'Waiting');
  const canDecide =
    requisition?.status === 'PendingApproval' &&
    activeStep?.approverUserId === session?.userId &&
    hasPermission(session, 'permission:requisitions:requisitions:approve');

  const isOwnerOrCompanyWide =
    requisition?.requestedByUserId === session?.userId ||
    session?.role === 'Admin' ||
    session?.role === 'HrDirector';

  const canCancel =
    (requisition?.status === 'Draft' || requisition?.status === 'PendingApproval') &&
    isOwnerOrCompanyWide &&
    hasPermission(session, 'permission:requisitions:requisitions:delete');

  const canEdit =
    requisition?.status === 'Draft' &&
    isOwnerOrCompanyWide &&
    hasPermission(session, 'permission:requisitions:requisitions:update');

  const canSubmit =
    requisition?.status === 'Draft' &&
    hasPermission(session, 'permission:requisitions:requisitions:update');

  const handleDecide = async (approve: boolean) => {
    if (!requisition || !onDecide) return;
    await onDecide(requisition.id, approve, comment);
    setComment('');
  };

  return (
    <Sheet isOpen={isOpen} onClose={onClose} size="lg">
      {isLoading ? (
        <SheetBody>
          <div className="py-12 text-center text-ink-600">Loading requisition details...</div>
        </SheetBody>
      ) : !requisition ? (
        <SheetBody>
          <div className="py-12 text-center text-ink-600">No requisition details available.</div>
        </SheetBody>
      ) : (
        <div className="flex h-full flex-col">
          <SheetHeader className="flex items-center justify-between">
            <div>
              <div className="flex items-center gap-3">
                <SheetTitle>{requisition.title}</SheetTitle>
                <StatusPill status={requisition.status} />
              </div>
              <p className="mt-1 text-sm text-ink-600">
                Department: <span className="font-medium text-ink-900">{requisition.departmentName}</span>
              </p>
            </div>
            {canEdit && onEdit && (
              <Button variant="secondary" className="h-8 px-3 text-xs" onClick={() => onEdit(requisition.id)}>
                Edit Draft
              </Button>
            )}
          </SheetHeader>

          <SheetBody className="flex-1 space-y-6">
            {error && (
              <div role="alert" className="rounded-sm bg-danger-100 p-3 text-sm text-danger-600">
                {error}
              </div>
            )}

            {/* Core Metrics Grid */}
            <div className="grid grid-cols-2 gap-4 rounded-md border border-line-200 bg-surface-50 p-4 sm:grid-cols-4">
              <div>
                <dt className="text-xs text-ink-500 uppercase tracking-wide">Target Hires</dt>
                <dd className="mt-1 font-mono text-base font-semibold text-ink-900">
                  {requisition.headcount}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-ink-500 uppercase tracking-wide">Salary Budget</dt>
                <dd className="mt-1 font-mono text-base font-semibold text-ink-900">
                  {requisition.salaryBudget != null
                    ? `$${requisition.salaryBudget.toLocaleString()}`
                    : '—'}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-ink-500 uppercase tracking-wide">Submitted</dt>
                <dd className="mt-1 text-sm font-medium text-ink-900">
                  {requisition.submittedAt
                    ? new Date(requisition.submittedAt).toLocaleDateString()
                    : '—'}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-ink-500 uppercase tracking-wide">Decided</dt>
                <dd className="mt-1 text-sm font-medium text-ink-900">
                  {requisition.decidedAt
                    ? new Date(requisition.decidedAt).toLocaleDateString()
                    : '—'}
                </dd>
              </div>
            </div>

            {/* Job Description */}
            <div>
              <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-500">
                Job Description
              </h3>
              <div className="rounded-md border border-line-200 bg-surface-0 p-4 text-sm leading-relaxed whitespace-pre-wrap text-ink-900">
                {requisition.jobDescription || 'No description provided.'}
              </div>
            </div>

            {/* Approval Timeline */}
            {requisition.approvals.length > 0 && (
              <div>
                <h3 className="mb-3 text-xs font-semibold uppercase tracking-wider text-ink-500">
                  Approval Timeline
                </h3>
                <div className="rounded-md border border-line-200 bg-surface-0 p-4">
                  <ol className="space-y-4">
                    {requisition.approvals.map((step) => (
                      <li key={step.sequence} className="flex items-start gap-4">
                        <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-surface-50 text-xs font-bold text-ink-700 border border-line-200">
                          {step.sequence}
                        </span>
                        <div className="flex-1">
                          <div className="flex items-center gap-2">
                            <span className="font-semibold text-sm text-ink-900">{step.label}</span>
                            {decisionBadge(step.decision)}
                          </div>
                          {step.decidedAt && (
                            <p className="mt-0.5 text-xs text-ink-400">
                              {new Date(step.decidedAt).toLocaleString()}
                            </p>
                          )}
                          {step.comment && (
                            <p className="mt-1.5 rounded-sm bg-surface-50 p-2.5 text-xs italic text-ink-700 border border-line-200">
                              &ldquo;{step.comment}&rdquo;
                            </p>
                          )}
                        </div>
                      </li>
                    ))}
                  </ol>
                </div>
              </div>
            )}

            {/* Approver Decision Section */}
            {canDecide && (
              <div className="rounded-md border border-warning-100 bg-warning-100/20 p-4">
                <h3 className="mb-1 text-sm font-semibold text-ink-900">
                  Approval Action Required — {requisition.awaitingApprovalFrom ?? 'Pending'}
                </h3>
                <p className="mb-3 text-xs text-ink-600">
                  You are the active approver for this step. Please review and record your decision.
                </p>
                <div className="space-y-3">
                  <div>
                    <label htmlFor="decision-comment" className="mb-1 block text-xs font-medium text-ink-700">
                      Comments (Optional)
                    </label>
                    <textarea
                      id="decision-comment"
                      rows={2}
                      value={comment}
                      onChange={(e) => setComment(e.target.value)}
                      placeholder="Add feedback or notes for the team..."
                      className="w-full rounded-sm border border-line-200 p-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-600"
                    />
                  </div>
                  <div className="flex gap-3">
                    <Button onClick={() => handleDecide(true)} disabled={actionBusy}>
                      {actionBusy ? 'Working...' : 'Approve Requisition'}
                    </Button>
                    <Button
                      variant="danger"
                      onClick={() => handleDecide(false)}
                      disabled={actionBusy}
                    >
                      Reject
                    </Button>
                  </div>
                </div>
              </div>
            )}
          </SheetBody>

          {/* Footer Actions */}
          <SheetFooter>
            {canSubmit && onSubmitForApproval && (
              <Button
                onClick={() => onSubmitForApproval(requisition.id)}
                disabled={actionBusy}
              >
                {actionBusy ? 'Submitting...' : 'Submit for Approval'}
              </Button>
            )}
            {canCancel && onCancel && (
              <Button
                variant="danger"
                onClick={() => {
                  if (confirm('Withdraw this requisition? This cannot be undone.')) {
                    onCancel(requisition.id);
                  }
                }}
                disabled={actionBusy}
              >
                Withdraw Requisition
              </Button>
            )}
            <Button variant="secondary" onClick={onClose}>
              Close
            </Button>
          </SheetFooter>
        </div>
      )}
    </Sheet>
  );
}
