import { useEffect, useState } from 'react';
import { Button, Card } from '@recruitops/ui';
import type {
  ApprovalChain, ApprovalChainPurpose, CreateApprovalChainRequest,
  DepartmentListItem, OfferSettings, SelectableUser, UpdateOfferSettingsRequest,
} from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';
import { PreboardingSettingsCard } from './PreboardingSettingsCard';

type StepDraft = { label: string; approverUserId: string };

const EMPTY_FORM: CreateApprovalChainRequest = {
  name: '',
  departmentId: null,
  purpose: 'Requisition',
  steps: [{ label: '', approverUserId: '' }],
};

/** `e.message` is trusted only when it is a non-blank string — an `ApiError` can carry `''`. */
function errorMessage(e: unknown, fallback: string): string {
  return e instanceof Error && e.message.trim() ? e.message : fallback;
}

/**
 * Label for a chain step's approver. `/users/selectable` filters `IsActive`, so a step can
 * reference a user who has since left the company — that must read as a readable gap, not a
 * raw GUID. A blank/whitespace-only `displayName` must not win over the id fallback either:
 * `??` alone is nullish-only, so `'   '` would beat the fallback and the row would render
 * nothing at all (worse than the GUID it was meant to avoid).
 */
function approverLabel(users: SelectableUser[], approverUserId: string): string {
  const approver = users.find((u) => u.id === approverUserId);
  if (!approver) return 'Unknown approver (no longer active)';
  const name = approver.displayName.trim();
  return name ? approver.displayName : approverUserId;
}

export function ApprovalChainsPage() {
  const [chains, setChains] = useState<ApprovalChain[] | null>(null);
  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [users, setUsers] = useState<SelectableUser[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<CreateApprovalChainRequest>(EMPTY_FORM);
  // The chain list is the page's primary resource, so its load error is kept apart from the
  // (less critical) department/approver load errors and from form-submit errors — each has a
  // different owner and must not be clobbered or wiped by an unrelated action (see `submit`
  // and the Cancel handler below, and D1/D2 in the M1 remediation).
  const [chainsError, setChainsError] = useState<string | null>(null);
  const [auxError, setAuxError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  // Separate from `auxError` (the page-level banner) so the create form can explain, right
  // next to the now-unfillable approver dropdown, *why* it only offers the placeholder —
  // the same silent-failure gap D1/D2 were rejected over, applied here to `users`.
  const [usersError, setUsersError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const error = chainsError ?? formError ?? auxError;

  useEffect(() => {
    // Mirrors InboxPage.tsx: leave `chains` null on failure instead of `[]`, so "the server
    // said none" (a real empty list) can never be confused with "we could not find out".
    api<ApprovalChain[]>('/approvalchains').then((data) => setChains(data ?? []))
      .catch((e) => setChainsError(errorMessage(e, 'Could not load approval chains.')));
    api<DepartmentListItem[]>('/departments').then(setDepartments)
      .catch((e) => setAuxError(errorMessage(e, 'Could not load departments.')));
    api<SelectableUser[]>('/users/selectable').then(setUsers)
      .catch((e) => {
        const message = errorMessage(e, 'Could not load approvers.');
        setAuxError(message);
        setUsersError(message);
      });
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
    setFormError(null);
    try {
      const created = await api<ApprovalChain>('/approvalchains', {
        method: 'POST',
        body: JSON.stringify(form),
      });
      setChains((prev) => [...(prev ?? []), created]);
      setForm(EMPTY_FORM);
      setShowForm(false);
    } catch (err) {
      setFormError(errorMessage(err, 'Could not create chain.'));
    } finally {
      setBusy(false);
    }
  }

  const field = 'h-10 w-full rounded-md border border-line px-3 focus:outline-none focus:ring-2 focus:ring-brand-700 text-md';

  return (
    <>
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Approval chains</h1>
          <p className="mt-1 text-sm text-ink-400">
            Define who must approve a requisition, or an over-budget offer, and in what order.
          </p>
        </div>
        {/* Creating a chain needs settings:update; the HR Director reads chains without it
            (ADR-0022 amendment, 2026-10-17), and a button that can only answer 403 is a dead end. */}
        {!showForm && hasPermission(auth.get(), 'permission:settings:settings:update') && (
          <Button onClick={() => setShowForm(true)}>New chain</Button>
        )}
      </header>

      {error && <p role="alert" className="mb-4 text-sm text-critical-700">{error}</p>}

      <OfferPolicyCard />
      <PreboardingSettingsCard />

      {/* ── Create form ── */}
      {showForm && (
        <Card>
          <h2 className="mb-4 text-base font-semibold">
            New approval chain
          </h2>
          <form onSubmit={submit} className="space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div>
                <label className="mb-1 block text-sm font-semibold">Chain name</label>
                <input required className={field} placeholder="e.g. Default headcount approval"
                  value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div>
                <label className="mb-1 block text-sm font-semibold">
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
              {/* ADR-0030 §1: an offer is approved by the same rules on a chain of its own, so a
                  department's offer approvers can differ from its requisition approvers. */}
              <div>
                <label htmlFor="chain-purpose" className="mb-1 block text-sm font-semibold">Approves</label>
                <select id="chain-purpose" className={field}
                  value={form.purpose ?? 'Requisition'}
                  onChange={(e) => setForm({ ...form, purpose: e.target.value as ApprovalChainPurpose })}>
                  <option value="Requisition">Requisitions</option>
                  <option value="Offer">Offers</option>
                </select>
              </div>
            </div>

            <div>
              <div className="mb-2 flex items-center justify-between">
                <label className="text-sm font-semibold">Approval steps (in order)</label>
                <button type="button" onClick={addStep}
                  className="text-sm font-semibold text-brand-700 hover:text-brand-700">
                  + Add step
                </button>
              </div>
              <div className="space-y-2">
                {form.steps.map((step, i) => (
                  <div key={i} className="flex items-center gap-2">
                    <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-canvas text-xs font-bold text-ink-600">
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
                      {usersError && (
                        <option value="" disabled>Could not load approvers</option>
                      )}
                      {users.map((u) => (
                        <option key={u.id} value={u.id}>
                          {u.displayName} ({u.role})
                        </option>
                      ))}
                    </select>
                    {form.steps.length > 1 && (
                      <button type="button" onClick={() => removeStep(i)}
                        className="shrink-0 text-2xl leading-none text-ink-400 hover:text-critical-700">
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
              <Button variant="ghost" type="button" onClick={() => { setShowForm(false); setFormError(null); }}>
                Cancel
              </Button>
            </div>
          </form>
        </Card>
      )}

      {/* ── Chain list ── */}
      {chains === null && !chainsError && <p className="text-ink-600">Loading…</p>}
      {chains?.length === 0 && !showForm && (
        <Card>
          <div className="py-6 text-center">
            <h3 className="text-base font-semibold">No approval chains yet</h3>
            <p className="mt-1 text-sm text-ink-600">
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
                  <p className="text-sm text-ink-400">
                    {chain.purpose === 'Offer' ? 'Offers' : 'Requisitions'}
                    {' · '}
                    {chain.departmentId
                      ? departments.find((d) => d.id === chain.departmentId)?.name ?? 'Specific department'
                      : 'Company-wide'
                    }
                    {' · '}
                    {chain.isActive ? (
                      <span className="text-positive-700">Active</span>
                    ) : (
                      <span className="text-ink-400">Inactive</span>
                    )}
                  </p>
                </div>
                <span className="text-sm text-ink-400">{chain.steps.length} step{chain.steps.length !== 1 ? 's' : ''}</span>
              </div>
              {chain.steps.length > 0 && (
                <ol className="mt-3 space-y-1 border-t border-line pt-3">
                  {chain.steps.map((s) => (
                    <li key={s.sequence} className="flex items-center gap-2 text-sm">
                      <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-canvas text-2xs font-bold text-ink-600">
                        {s.sequence}
                      </span>
                      <span className="font-semibold">{s.label}</span>
                      <span className="text-ink-400">·</span>
                      <span className="text-ink-600">
                        {approverLabel(users, s.approverUserId)}
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

/**
 * The company's offer policy (ADR-0030 §2, §4), beside the chains it routes offers through.
 *
 * ⚠️ Not drawn in `design/` — `settings-org.html` has the chains and no offer policy. Kept to two
 * plain switches until it is. Readers need `settings:read` (the API answers 403 otherwise, and the
 * card then stays away); changing it needs `settings:update`, and every change is audited.
 */
function OfferPolicyCard() {
  const canEdit = hasPermission(auth.get(), 'permission:settings:settings:update');
  const [settings, setSettings] = useState<OfferSettings | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    api<OfferSettings>('/settings/offers').then(setSettings).catch(() => setSettings(null));
  }, []);

  if (!settings) return null;

  async function save(next: OfferSettings) {
    setBusy(true);
    setMessage(null);
    try {
      const body: UpdateOfferSettingsRequest = next;
      setSettings(await api<OfferSettings>('/settings/offers', { method: 'PUT', body: JSON.stringify(body) }));
      setMessage('Saved.');
    } catch (e) {
      setMessage(errorMessage(e, 'Could not save the offer policy.'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mb-6">
      <Card>
        <h2 className="text-base font-semibold">Offer policy</h2>
        <div className="mt-3 space-y-3">
          <label className="flex items-start gap-3">
            <input
              type="checkbox" className="mt-1 h-4 w-4" disabled={!canEdit || busy}
              checked={settings.requireOfferApproval}
              onChange={(e) => void save({ ...settings, requireOfferApproval: e.target.checked })}
            />
            <span>
              <span className="block text-md font-medium">Every offer needs approval</span>
              <span className="block text-sm text-ink-600">
                Off: only offers above the requisition's budget go to the offer chain.
              </span>
            </span>
          </label>
          <label className="flex items-start gap-3">
            <input
              type="checkbox" className="mt-1 h-4 w-4" disabled={!canEdit || busy}
              checked={settings.showOfferSalaryToHiringManagers}
              onChange={(e) => void save({ ...settings, showOfferSalaryToHiringManagers: e.target.checked })}
            />
            <span>
              <span className="block text-md font-medium">Hiring Managers see offered salaries</span>
              <span className="block text-sm text-ink-600">
                Off: they see their departments' offers and where each stands, but no amounts.
              </span>
            </span>
          </label>
        </div>
        {message && <p role="status" className="mt-3 text-sm text-ink-600">{message}</p>}
      </Card>
    </div>
  );
}
