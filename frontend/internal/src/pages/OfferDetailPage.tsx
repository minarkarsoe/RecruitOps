import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type { ApprovalDecisionRequest, OfferDetail, OfferLetter, SendOfferRequest, UpdateOfferRequest } from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';
import { ApprovalDecisionCard, ApprovalTimeline, approvalState } from '../components/ApprovalRail';
import { calendarDate, day, money, remindedRecently } from './offerFormat';

/**
 * Module 4.2 — one offer, built from `design/internal/offer-create.html`.
 *
 * Three readers, one page, and the server decides which (ADR-0030 §4): recruitment staff edit a
 * Draft and send it for approval; someone named on the approval decides it, on the same rail
 * device as a requisition; a Hiring Manager reads it — without salary, letter or budget unless
 * the company shows them, because the API sends those as null.
 *
 * The budget warning sits where the numbers are typed, before anything is submitted: the point is
 * to let the recruiter reconsider, not to ambush them after sending.
 */

const PROBATION = [0, 1, 2, 3, 6, 12];

/** What the base must come down to for the package to fit the budget, given the allowances. */
export function baseToFit(budget: number, allowances: number): number {
  return Math.max(0, budget - allowances);
}


export function OfferDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const session = auth.get();

  const [offer, setOffer] = useState<OfferDetail | null>(null);
  const [form, setForm] = useState<UpdateOfferRequest | null>(null);
  const [letter, setLetter] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function show(o: OfferDetail) {
    setOffer(o);
    setForm(o.salaryVisible ? {
      baseSalary: o.baseSalary ?? 0,
      allowances: o.allowances ?? 0,
      joiningDate: o.joiningDate,
      probationMonths: o.probationMonths,
      expiresOn: o.expiresOn,
      letterBody: o.letterBody ?? '',
    } : null);
    setLetter(null);
  }

  useEffect(() => {
    if (!id) return;
    api<OfferDetail>(`/offers/${id}`).then(show).catch((e) => setError(e instanceof Error ? e.message : 'Could not load the offer.'));
  }, [id]);

  async function act(run: () => Promise<OfferDetail>) {
    setBusy(true);
    setError(null);
    try {
      show(await run());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  const save = () => act(() => api<OfferDetail>(`/offers/${id}`, { method: 'PUT', body: JSON.stringify(form) }));

  // Saved first, so what the approver is asked about is exactly what is on screen.
  const submit = () => act(async () => {
    await api<OfferDetail>(`/offers/${id}`, { method: 'PUT', body: JSON.stringify(form) });
    return api<OfferDetail>(`/offers/${id}/submit`, { method: 'POST' });
  });

  const revise = () => act(() => api<OfferDetail>(`/offers/${id}/revise`, { method: 'POST' }));

  // A draft is saved first, as for approval: what the candidate receives is what is on screen.
  function send() {
    if (!offer) return;
    // The address is fixed at sending — every code and reminder goes there (ADR-0030 §5a) — so it is
    // shown before it is fixed, not after.
    const to = offer.candidateEmail ? ` at ${offer.candidateEmail}` : '';
    if (!confirm(`Send this offer to ${offer.candidateName}${to}? They will get an email with a secure link, and the terms can no longer be changed.`)) return;
    const draft = offer.status === 'Draft';
    void act(async () => {
      if (draft) await api<OfferDetail>(`/offers/${id}`, { method: 'PUT', body: JSON.stringify(form) });
      // The address just confirmed goes with the request: if it changed meanwhile, the API refuses.
      const body: SendOfferRequest = { expectedEmail: offer.candidateEmail };
      return api<OfferDetail>(`/offers/${id}/send`, { method: 'POST', body: JSON.stringify(body) });
    });
  }

  const remind = () => act(() => api<OfferDetail>(`/offers/${id}/remind`, { method: 'POST' }));

  // Bound to the round on screen: if the offer was revised meanwhile, the API refuses rather than
  // record an approval of terms this approver never saw.
  const decide = (approve: boolean, comment: string | null) => act(() => {
    const round = approvalState(offer?.approvals ?? [], session?.userId).currentRound;
    const body: ApprovalDecisionRequest = { approve, comment, round };
    return api<OfferDetail>(`/offers/${id}/decide`, { method: 'POST', body: JSON.stringify(body) });
  });

  async function remove() {
    if (!confirm('Delete this draft offer? This cannot be undone.')) return;
    setBusy(true);
    try {
      await api(`/offers/${id}`, { method: 'DELETE' });
      navigate('/offers');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not delete the offer.');
      setBusy(false);
    }
  }

  async function preview() {
    setError(null);
    try {
      setLetter((await api<OfferLetter>(`/offers/${id}/letter`)).text);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not render the letter.');
    }
  }

  if (error && !offer) return <p role="alert" className="text-critical-700">{error}</p>;
  if (!offer) return <p className="text-ink-600">Loading…</p>;

  const editable = offer.canManage && offer.status === 'Draft' && form !== null;

  // Computed from what is typed, not what was saved: the button never lies about what it does.
  const total = form ? form.baseSalary + form.allowances : null;
  const overBudget = form && offer.salaryBudget !== null ? (total ?? 0) > offer.salaryBudget : offer.overBudget;
  const needsApproval = overBudget === true || offer.policyRequiresApproval === true;
  const overBy = form && offer.salaryBudget ? Math.round(((form.baseSalary - offer.salaryBudget) / offer.salaryBudget) * 100) : null;

  const field = 'h-10 w-full rounded-md border border-line bg-white px-3 tnum focus:outline-none focus:ring-2 focus:ring-brand-700 disabled:bg-canvas disabled:text-ink-600';

  return (
    <>
      <header className="mb-6">
        <div className="mb-2 flex items-center gap-1.5 text-sm">
          <Link to="/offers" className="text-brand-700 hover:underline">Offers</Link>
          <span className="text-ink-400">/</span>
          <span className="tnum font-mono text-ink-600">{offer.reference}</span>
        </div>
        <div className="flex flex-wrap items-start gap-3">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-xl font-semibold tracking-tight">{offer.candidateName}</h1>
              <StatusPill status={offer.status} />
            </div>
            <p className="mt-1 text-base text-ink-600">
              {offer.position} · {offer.departmentName} · from{' '}
              <Link to={`/requisitions/${offer.requisitionId}`} className="text-brand-700 hover:underline">its requisition</Link>
            </p>
          </div>
          {editable && (
            <div className="ml-auto flex items-center gap-2">
              <Button variant="secondary" onClick={save} disabled={busy}>Save draft</Button>
              {/* Keyed apart: two different actions, not one button changing colour mid-transition. */}
              {needsApproval ? (
                <Button key="submit" onClick={submit} disabled={busy}>Send for internal approval</Button>
              ) : (
                <Button key="send" onClick={send} disabled={busy}>Send to candidate</Button>
              )}
            </div>
          )}
          {offer.canManage && offer.status === 'Approved' && (
            <div className="ml-auto">
              <Button onClick={send} disabled={busy}>Send to candidate</Button>
            </div>
          )}
        </div>
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-7">
          <section className="rounded-lg border border-line bg-white">
            <header className="flex items-center justify-between border-b border-line px-5 py-3">
              <h2 className="text-base font-semibold">Terms</h2>
              <span className="text-sm text-ink-600">Name, position and department auto-filled</span>
            </header>

            {!offer.salaryVisible && (
              <p className="border-b border-line bg-canvas px-5 py-3 text-sm text-ink-600">
                Salary is hidden. Your company shows offer salaries to recruitment staff and to the
                people who approve the offer.
              </p>
            )}

            <div className="space-y-4 px-5 py-4">
              {form && (
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div>
                    <label htmlFor="base" className="mb-1 block text-sm font-semibold">Base salary (MMK / month)</label>
                    <input
                      id="base" type="number" min={0} className={field} disabled={!editable}
                      value={form.baseSalary} aria-invalid={overBudget === true}
                      aria-describedby={overBudget ? 'baseHint' : undefined}
                      onChange={(e) => setForm({ ...form, baseSalary: Number(e.target.value) || 0 })}
                    />
                    {overBudget && offer.salaryBudget !== null && (
                      <p id="baseHint" className="mt-1 text-sm text-warn-700">
                        The package is above the approved budget of <span className="tnum font-mono">{money(offer.salaryBudget)}</span>
                        {overBy !== null && overBy > 0 && <> — the base alone by <span className="tnum font-mono">{overBy}%</span></>}.
                      </p>
                    )}
                  </div>
                  <div>
                    <label htmlFor="allow" className="mb-1 block text-sm font-semibold">Allowances (MMK / month)</label>
                    <input
                      id="allow" type="number" min={0} className={field} disabled={!editable}
                      value={form.allowances} onChange={(e) => setForm({ ...form, allowances: Number(e.target.value) || 0 })}
                    />
                  </div>
                </div>
              )}

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                <div>
                  <label htmlFor="join" className="mb-1 block text-sm font-semibold">Joining date</label>
                  <input
                    id="join" type="date" className={field} disabled={!editable}
                    value={form?.joiningDate ?? offer.joiningDate}
                    onChange={(e) => form && setForm({ ...form, joiningDate: e.target.value })}
                  />
                </div>
                <div>
                  <label htmlFor="prob" className="mb-1 block text-sm font-semibold">Probation</label>
                  <select
                    id="prob" className={field} disabled={!editable}
                    value={form?.probationMonths ?? offer.probationMonths}
                    onChange={(e) => form && setForm({ ...form, probationMonths: Number(e.target.value) })}
                  >
                    {[...new Set([...PROBATION, offer.probationMonths])].sort((a, b) => a - b).map((m) => (
                      <option key={m} value={m}>{m === 0 ? 'None' : `${m} month${m === 1 ? '' : 's'}`}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label htmlFor="exp" className="mb-1 block text-sm font-semibold">Offer expires</label>
                  <input
                    id="exp" type="date" className={field} disabled={!editable}
                    value={form?.expiresOn ?? offer.expiresOn}
                    onChange={(e) => form && setForm({ ...form, expiresOn: e.target.value })}
                  />
                  <p className="mt-1 text-sm text-ink-600">The secure link stops working on this date.</p>
                </div>
              </div>
            </div>

            {/* The consequence is stated where the decision is made, not after sending. */}
            {editable && overBudget && offer.salaryBudget !== null && form && (
              <footer className="border-t border-line bg-warn-50 px-5 py-4">
                <p className="text-base font-medium text-warn-700">This offer needs approval before it can be sent</p>
                <p className="mt-1 max-w-[62ch] text-sm text-warn-700">
                  Total package <span className="tnum font-mono font-medium">{money(total ?? 0)}</span> exceeds the
                  budget approved on the requisition. Bring the base to{' '}
                  <span className="tnum font-mono font-medium">{money(baseToFit(offer.salaryBudget, form.allowances))}</span>{' '}
                  or below and this offer needs no approval step.
                </p>
              </footer>
            )}
            {editable && !overBudget && offer.policyRequiresApproval && (
              <footer className="border-t border-line bg-canvas px-5 py-4 text-sm text-ink-600">
                Your company requires approval for every offer, so this one goes to the approval chain too.
              </footer>
            )}
          </section>

          {form && (
            <section className="rounded-lg border border-line bg-white">
              <header className="flex items-center justify-between border-b border-line px-5 py-3">
                <h2 className="text-base font-semibold">Offer letter</h2>
                <button type="button" onClick={preview} className="text-sm font-medium text-brand-700 hover:text-brand-800">
                  Preview letter
                </button>
              </header>
              <div className="px-5 py-4">
                <label htmlFor="letter" className="sr-only">Letter text</label>
                <textarea
                  id="letter" rows={12} disabled={!editable}
                  className="w-full rounded-md border border-line p-3 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-brand-700 disabled:bg-canvas"
                  value={form.letterBody} onChange={(e) => setForm({ ...form, letterBody: e.target.value })}
                />
                <p className="mt-1 text-sm text-ink-600">
                  {editable ? (
                    <>
                      Editing here changes this offer only. Fields such as <code className="font-mono">{'{{BaseSalary}}'}</code> are
                      filled from the saved terms — save the draft, then preview.
                    </>
                  ) : (
                    offer.status === 'Accepted'
                      ? <>Preview shows the letter exactly as the candidate signed it.</>
                      : <>Fields such as <code className="font-mono">{'{{BaseSalary}}'}</code> are filled in for the candidate — preview to read it as they will.</>
                  )}
                </p>
                {letter !== null && (
                  // Plain text from the server, rendered as text: a name is never markup.
                  <div className="mt-4 whitespace-pre-wrap rounded-md border border-line bg-canvas p-4 text-md leading-relaxed" data-testid="letter-preview">
                    {letter}
                  </div>
                )}
              </div>
            </section>
          )}
        </div>

        <aside className="space-y-4 lg:col-span-5">
          {/* Same rail device as Module 1. Deliberately not a second mechanism. */}
          {offer.approvals.length > 0 && (
            <Card>
              <h2 className="mb-4 text-base font-semibold">Approval</h2>
              <ApprovalTimeline approvals={offer.approvals} />
            </Card>
          )}

          {editable && needsApproval && (
            <Card>
              <h2 className="mb-3 text-base font-semibold">Approval this will need</h2>
              {offer.approvalPreview && offer.approvalPreview.length > 0 ? (
                <ol className="space-y-2">
                  {offer.approvalPreview.map((label, i) => (
                    <li key={`${i}-${label}`} className="flex items-center gap-3">
                      <span className="flex h-6 w-6 items-center justify-center rounded-full bg-canvas text-xs font-bold text-ink-600">{i + 1}</span>
                      <span className="font-medium">{label}</span>
                    </li>
                  ))}
                </ol>
              ) : (
                <p className="text-sm text-warn-700">
                  No offer approval chain is set up for this department. An Admin can add one under
                  Approval chains; until then this offer cannot be sent for approval.
                </p>
              )}
              <p className="mt-3 text-sm text-ink-600">
                Rejection here works like a requisition: it comes back to you with a reason, you revise,
                and the new round sits beside the old one rather than replacing it.
              </p>
            </Card>
          )}

          {offer.status === 'PendingApproval' && (
            <ApprovalDecisionCard
              approvals={offer.approvals} userId={session?.userId} noun="offer" busy={busy}
              onDecide={(approve, comment) => void decide(approve, comment)}
            />
          )}

          {offer.canManage && (offer.status === 'PendingApproval' || offer.status === 'Approved') && (
            <Card>
              <h2 className="mb-2 text-base font-semibold">Change the terms</h2>
              <p className="mb-3 text-sm text-ink-600">
                Returns this offer to Draft. {offer.status === 'Approved' ? 'The approval is void' : 'The pending approval is withdrawn'};
                sending it for approval again starts a fresh round, and this one stays on record.
              </p>
              <Button variant="secondary" onClick={revise} disabled={busy}>Revise offer</Button>
            </Card>
          )}

          {/* ⚠️ The cards below — with the candidate, answered, expired — are not drawn in design/:
              offer-create.html draws the editor before sending. Plain, and flagged in FEATURE-STATUS. */}
          {(offer.status === 'Sent' || offer.status === 'Viewed') && (
            <Card>
              <h2 className="mb-2 text-base font-semibold">With the candidate</h2>
              <p className="text-sm text-ink-600">
                Sent {offer.sentAt ? day(offer.sentAt) : ''} ·{' '}
                {offer.viewedAt ? `opened ${day(offer.viewedAt)}, not answered yet` : 'not opened yet'}.
                They can answer until {calendarDate(offer.expiresOn)}; after that the link stops working.
              </p>
              {offer.canManage && (
                <div className="mt-3 flex items-center gap-3">
                  <Button variant="secondary" onClick={remind} disabled={busy || remindedRecently(offer.lastRemindedAt)}>
                    Remind candidate
                  </Button>
                  {offer.lastRemindedAt && (
                    <span className="text-sm text-ink-600">Reminded {day(offer.lastRemindedAt)}</span>
                  )}
                </div>
              )}
            </Card>
          )}

          {offer.status === 'Accepted' && (
            <Card>
              <h2 className="mb-2 text-base font-semibold">Accepted</h2>
              {offer.signature ? (
                <dl className="space-y-1.5 text-sm">
                  <div><dt className="inline text-ink-600">Signed as </dt><dd className="inline font-medium">“{offer.signature.typedName}”</dd></div>
                  <div><dt className="inline text-ink-600">On </dt><dd className="inline">{new Date(offer.signature.signedAt).toLocaleString()}</dd></div>
                  <div><dt className="inline text-ink-600">From </dt><dd className="inline tnum font-mono">{offer.signature.ipAddress ?? 'an address that was not recorded'}</dd></div>
                  {offer.signature.sentToEmail && (
                    <div><dt className="inline text-ink-600">Unlocked by a code sent to </dt><dd className="inline">{offer.signature.sentToEmail}</dd></div>
                  )}
                  {offer.signature.userAgent && (
                    <div><dt className="inline text-ink-600">Device </dt><dd className="inline break-words text-ink-600">{offer.signature.userAgent}</dd></div>
                  )}
                  <div>
                    <dt className="text-ink-600">Fingerprint of the letter signed (SHA-256)</dt>
                    <dd className="break-all font-mono text-xs text-ink-600">{offer.signature.letterSha256}</dd>
                  </div>
                </dl>
              ) : (
                <p className="text-sm text-ink-600">The candidate accepted on {offer.respondedAt ? day(offer.respondedAt) : '—'}.</p>
              )}
              <p className="mt-3 text-sm text-ink-600">Joining {calendarDate(offer.joiningDate)}.</p>
              {/* Documents are recruitment staff's alone (ADR-0030 §5b): no link for anyone else. */}
              {offer.canManage && hasPermission(session, 'permission:offers:preboarding:read') && (
                <Link to={`/offers/${offer.id}/preboarding`} className="mt-3 inline-block text-sm font-medium text-brand-700 hover:text-brand-800">
                  Pre-boarding — joining documents
                </Link>
              )}
            </Card>
          )}

          {offer.status === 'Rejected' && (
            <Card>
              <h2 className="mb-2 text-base font-semibold">Declined by the candidate</h2>
              <p className="text-sm text-ink-600">On {offer.respondedAt ? day(offer.respondedAt) : '—'}.</p>
              {offer.declineReason && (
                // The candidate's own words, as text — never markup.
                <p className="mt-2 whitespace-pre-wrap rounded-md bg-canvas p-3 text-sm italic text-ink-700">“{offer.declineReason}”</p>
              )}
            </Card>
          )}

          {offer.status === 'Expired' && (
            <Card>
              <h2 className="mb-2 text-base font-semibold">Expired</h2>
              <p className="text-sm text-ink-600">
                The candidate did not answer by {calendarDate(offer.expiresOn)}, and the link has stopped working.
                {offer.canManage && ' To try again, create a new offer from the dashboard.'}
              </p>
            </Card>
          )}

          {offer.canManage && (offer.status === 'Draft' || offer.status === 'Approved') && (
            <Card>
              <h2 className="mb-2 text-base font-semibold">What the candidate will receive</h2>
              <ul className="space-y-1.5 text-sm text-ink-600">
                <li>· An email with a secure link, not the letter as an attachment — and, when they open it, a code sent to their email.</li>
                <li>· The full letter, then Accept &amp; Sign (their typed name) or Decline.</li>
                <li>· A copy of what they signed, by email.</li>
              </ul>
            </Card>
          )}

          {offer.canManage && offer.status === 'Draft' && offer.approvals.length === 0 && (
            <div className="text-right">
              <button type="button" onClick={remove} disabled={busy} className="text-sm font-medium text-critical-700 hover:underline">
                Delete draft
              </button>
            </div>
          )}
        </aside>
      </div>
    </>
  );
}
