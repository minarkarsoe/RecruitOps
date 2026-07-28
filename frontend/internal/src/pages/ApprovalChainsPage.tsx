import { useEffect, useState } from 'react';
import { Button, Card } from '@recruitops/ui';
import type {
  ApprovalChain, CreateApprovalChainRequest,
  DepartmentListItem, UserListItem,
} from '@recruitops/types';
import { api } from '../lib/api';

type StepDraft = { label: string; approverUserId: string };

const EMPTY_FORM: CreateApprovalChainRequest = {
  name: '',
  departmentId: null,
  steps: [{ label: '', approverUserId: '' }],
};

export function ApprovalChainsPage() {
  const [chains, setChains] = useState<ApprovalChain[] | null>(null);
  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<CreateApprovalChainRequest>(EMPTY_FORM);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api<ApprovalChain[]>('/approvalchains').then(setChains).catch(() => setChains([]));
    api<DepartmentListItem[]>('/departments').then(setDepartments).catch(() => {});
    api<UserListItem[]>('/users').then(setUsers).catch(() => {});
  }, []);

  function setStep(i: number, patch: Partial<StepDraft>) {
    setForm((f) => {
      const steps = [...f.steps];
      steps[i] = { ...steps[i], ...patch };
      return { ...f, steps };
    });
  }

  function addStep() {
    setForm((f) => ({ ...f, steps: [...f.steps, { label: '', approverUserId: '' }] }));
  }

  function removeStep(i: number) {
    setForm((f) => ({ ...f, steps: f.steps.filter((_, idx) => idx !== i) }));
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const created = await api<ApprovalChain>('/approvalchains', {
        method: 'POST',
        body: JSON.stringify(form),
      });
      setChains((prev) => [...(prev ?? []), created]);
      setForm(EMPTY_FORM);
      setShowForm(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create chain.');
    } finally {
      setBusy(false);
    }
  }

  const field = 'h-10 w-full rounded-sm border border-line-200 px-3 focus:outline-none focus:ring-2 focus:ring-primary-600 text-[15px]';

  return (
    <>
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Approval chains</h1>
          <p className="mt-1 text-[13px] text-ink-400">
            Define who must approve a requisition and in what order.
          </p>
        </div>
        {!showForm && (
          <Button onClick={() => setShowForm(true)}>New chain</Button>
        )}
      </header>

      {/* ── Create form ── */}
      {showForm && (
        <Card>
          <h2 className="mb-4 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
            New approval chain
          </h2>
          {error && <p role="alert" className="mb-3 text-[13px] text-danger-600">{error}</p>}
          <form onSubmit={submit} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="mb-1 block text-[13px] font-semibold">Chain name</label>
                <input required className={field} placeholder="e.g. Default headcount approval"
                  value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div>
                <label className="mb-1 block text-[13px] font-semibold">
                  Department <span className="font-normal text-ink-400">(empty = company-wide)</span>
                </label>
                <select className={field}
                  value={form.departmentId ?? ''}
                  onChange={(e) => setForm({ ...form, departmentId: e.target.value || null })}>
                  <option value="">Company-wide (all departments)</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>{d.name}</option>
                  ))}
                </select>
              </div>
            </div>

            <div>
              <div className="mb-2 flex items-center justify-between">
                <label className="text-[13px] font-semibold">Approval steps (in order)</label>
                <button type="button" onClick={addStep}
                  className="text-[13px] font-semibold text-primary-600 hover:text-primary-700">
                  + Add step
                </button>
              </div>
              <div className="space-y-2">
                {form.steps.map((step, i) => (
                  <div key={i} className="flex items-center gap-2">
                    <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-surface-50 text-[12px] font-bold text-ink-600">
                      {i + 1}
                    </span>
                    <input
                      required placeholder="Step label (e.g. HR Review)"
                      className={`${field} flex-1`}
                      value={step.label}
                      onChange={(e) => setStep(i, { label: e.target.value })}
                    />
                    <select
                      required className={`${field} flex-1`}
                      value={step.approverUserId}
                      onChange={(e) => setStep(i, { approverUserId: e.target.value })}
                    >
                      <option value="">Select approver…</option>
                      {users.map((u) => (
                        <option key={u.id} value={u.id}>
                          {u.displayName} ({u.role})
                        </option>
                      ))}
                    </select>
                    {form.steps.length > 1 && (
                      <button type="button" onClick={() => removeStep(i)}
                        className="shrink-0 text-[20px] leading-none text-ink-400 hover:text-danger-600">
                        ×
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </div>

            <div className="flex gap-3">
              <Button type="submit" disabled={busy}>
                {busy ? 'Creating…' : 'Create chain'}
              </Button>
              <Button variant="ghost" type="button" onClick={() => { setShowForm(false); setError(null); }}>
                Cancel
              </Button>
            </div>
          </form>
        </Card>
      )}

      {/* ── Chain list ── */}
      {chains === null && <p className="text-ink-600">Loading…</p>}
      {chains?.length === 0 && !showForm && (
        <Card>
          <div className="py-6 text-center">
            <h3 className="text-base font-semibold">No approval chains yet</h3>
            <p className="mt-1 text-[13px] text-ink-600">
              Create one to enable requisition submission.
            </p>
          </div>
        </Card>
      )}
      {chains && chains.length > 0 && (
        <div className="mt-6 space-y-4">
          {chains.map((chain) => (
            <Card key={chain.id}>
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-semibold">{chain.name}</p>
                  <p className="text-[13px] text-ink-400">
                    {chain.departmentId
                      ? departments.find((d) => d.id === chain.departmentId)?.name ?? 'Specific department'
                      : 'Company-wide'
                    }
                    {' · '}
                    {chain.isActive ? (
                      <span className="text-success-600">Active</span>
                    ) : (
                      <span className="text-ink-400">Inactive</span>
                    )}
                  </p>
                </div>
                <span className="text-[13px] text-ink-400">{chain.steps.length} step{chain.steps.length !== 1 ? 's' : ''}</span>
              </div>
              {chain.steps.length > 0 && (
                <ol className="mt-3 space-y-1 border-t border-line-200 pt-3">
                  {chain.steps.map((s) => (
                    <li key={s.sequence} className="flex items-center gap-2 text-[13px]">
                      <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-surface-50 text-[11px] font-bold text-ink-600">
                        {s.sequence}
                      </span>
                      <span className="font-semibold">{s.label}</span>
                      <span className="text-ink-400">·</span>
                      <span className="text-ink-600">
                        {users.find((u) => u.id === s.approverUserId)?.displayName ?? s.approverUserId}
                      </span>
                    </li>
                  ))}
                </ol>
              )}
            </Card>
          ))}
        </div>
      )}
    </>
  );
}
