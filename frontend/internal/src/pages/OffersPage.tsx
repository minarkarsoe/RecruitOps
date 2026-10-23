import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button, Dialog, StatusPill } from '@recruitops/ui';
import type {
  CreateOfferRequest, DepartmentListItem, OfferDetail, OfferEligibleApplication, OfferListItem, OfferTemplate,
} from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission, isRecruitmentStaff } from '../lib/auth';
import { calendarDate, money, remindedRecently } from './offerFormat';

/**
 * Module 4.1 — the Offer Dashboard, built from `design/internal/offer-dashboard.html`.
 *
 * Reach is the server's (ADR-0030 §4): recruitment staff see every offer, a Hiring Manager their
 * departments' offers without salary unless the company shows it, and anyone named on an offer's
 * approval sees that offer. This page renders what it is given; a hidden salary arrives as null.
 *
 * `Rejected` here is the candidate's answer, never a pipeline rejection — so the pill is only ever
 * shown in this Offer column, and the line under it says who did the rejecting.
 */

type Group = '' | 'awaiting' | 'with-candidate' | 'closed';

const GROUPS: { value: Group; label: string }[] = [
  { value: '', label: 'All' },
  { value: 'awaiting', label: 'Awaiting approval' },
  { value: 'with-candidate', label: 'With candidate' },
  { value: 'closed', label: 'Closed' },
];

type Range = '90d' | 'quarter' | 'year' | 'all';

/** `YYYY-MM-DD` for the start of the chosen window, or undefined for all time. */
export function rangeStart(range: Range, today = new Date()): string | undefined {
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  if (range === '90d') return iso(new Date(today.getTime() - 90 * 86_400_000));
  if (range === 'quarter') return iso(new Date(Date.UTC(today.getUTCFullYear(), Math.floor(today.getUTCMonth() / 3) * 3, 1)));
  if (range === 'year') return iso(new Date(Date.UTC(today.getUTCFullYear(), 0, 1)));
  return undefined;
}

function shortDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
}

/** The line under the pill: the detail that makes a status actionable (`offer-dashboard.html`). */
function statusNote(o: OfferListItem): string | null {
  if (o.status === 'PendingApproval') return o.awaitingMe ? 'Your decision' : o.waitingOn ? `With ${o.waitingOn}` : null;
  if (o.status === 'Approved') return 'Cleared to send';
  if (o.status === 'Sent') return 'Not opened yet';
  if (o.status === 'Viewed') return o.viewedAt ? `Opened ${shortDate(o.viewedAt)} · not signed` : 'Opened · not signed';
  if (o.status === 'Accepted') return `Signed ${o.respondedAt ? shortDate(o.respondedAt) : ''} · joining ${calendarDate(o.joiningDate, false)}`;
  if (o.status === 'Rejected') return 'Declined by candidate';
  return null;
}

export function OffersPage() {
  const session = auth.get();
  const staff = session !== null && isRecruitmentStaff(session.role);
  // Joining documents are their own permission (ADR-0030 §5b): a trimmed custom role may not hold it.
  const canPreboard = staff && hasPermission(session, 'permission:offers:preboarding:read');

  const [group, setGroup] = useState<Group>('');
  const [departmentId, setDepartmentId] = useState('');
  const [range, setRange] = useState<Range>('90d');
  const [items, setItems] = useState<OfferListItem[] | null>(null);
  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [reminding, setReminding] = useState<string | null>(null);

  const load = useCallback(async () => {
    const params = new URLSearchParams();
    if (group) params.set('group', group);
    if (departmentId) params.set('departmentId', departmentId);
    const from = rangeStart(range);
    if (from) params.set('from', from);
    const qs = params.toString();
    setItems(await api<OfferListItem[]>(`/offers${qs ? `?${qs}` : ''}`));
  }, [group, departmentId, range]);

  useEffect(() => {
    load().catch((e) => setError(e instanceof Error ? e.message : 'Could not load offers.'));
  }, [load]);

  useEffect(() => {
    api<DepartmentListItem[]>('/departments').then(setDepartments).catch(() => setDepartments([]));
  }, []);

  async function remind(o: OfferListItem) {
    setReminding(o.id);
    setNotice(null);
    setError(null);
    try {
      await api(`/offers/${o.id}/remind`, { method: 'POST' });
      setNotice(`${o.candidateName} will be reminded by email.`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not send the reminder.');
    } finally {
      setReminding(null);
    }
  }

  return (
    <>
      <div className="mb-4 flex items-center gap-4">
        <h1 className="text-xl font-semibold tracking-tight">Offers</h1>
        {staff && (
          <div className="ml-auto">
            <Button onClick={() => setCreating(true)}>Create offer</Button>
          </div>
        )}
      </div>

      {/* Detented filters: discrete labelled positions, consistent with the board. */}
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="flex items-center rounded-md border border-line bg-white p-0.5" role="group" aria-label="Offer status">
          {GROUPS.map((g) => (
            <button
              key={g.value || 'all'}
              type="button"
              aria-pressed={group === g.value}
              onClick={() => setGroup(g.value)}
              className={`h-7 rounded px-2.5 text-sm transition-colors ${
                group === g.value ? 'bg-ink-900 font-medium text-white' : 'text-ink-600 hover:text-ink-900'}`}
            >
              {g.label}
            </button>
          ))}
        </div>
        <select
          aria-label="Department" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}
          className="h-8 rounded-md border border-line bg-white px-2.5 text-sm"
        >
          <option value="">All departments</option>
          {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <select
          aria-label="Period" value={range} onChange={(e) => setRange(e.target.value as Range)}
          className="h-8 rounded-md border border-line bg-white px-2.5 text-sm"
        >
          <option value="90d">Last 90 days</option>
          <option value="quarter">This quarter</option>
          <option value="year">This year</option>
          <option value="all">All time</option>
        </select>
        {items && <span className="tnum ml-auto text-sm text-ink-600">{items.length} offer{items.length === 1 ? '' : 's'}</span>}
      </div>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}
      {notice && <p role="status" className="mb-4 text-md text-positive-700">{notice}</p>}
      {items === null && !error && <p className="text-ink-600">Loading…</p>}

      {items && items.length === 0 && (
        <div className="rounded-lg border border-line bg-white p-16 text-center">
          <p className="text-base font-medium">No offers here</p>
          <p className="mx-auto mt-1.5 max-w-[46ch] text-base text-ink-600">
            An offer starts from a candidate at the Offer stage of a posting's pipeline.
          </p>
        </div>
      )}

      {items && items.length > 0 && (
        <div className="relative overflow-x-auto rounded-lg border border-line bg-white">
          <table className="w-full min-w-[900px] text-base">
            <thead>
              <tr className="border-b border-line bg-canvas text-left text-sm">
                <th className="px-5 py-2.5 font-medium text-ink-600">Candidate</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Position</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Department</th>
                <th className="px-5 py-2.5 text-right font-medium text-ink-600">Offered salary</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Sent</th>
                <th className="px-5 py-2.5 font-medium text-ink-600">Offer status</th>
                <th className="px-5 py-2.5 font-medium text-ink-600"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {items.map((o) => (
                <tr key={o.id} className="border-b border-line transition-colors last:border-b-0 hover:bg-canvas">
                  <td className="px-5 py-3">
                    <span className="font-medium">{o.candidateName}</span>
                    <span className="tnum block font-mono text-xs text-ink-600">{o.reference}</span>
                  </td>
                  <td className="px-5 py-3 text-ink-600">{o.position}</td>
                  <td className="px-5 py-3 text-ink-600">{o.departmentName}</td>
                  <td className="tnum px-5 py-3 text-right font-mono">
                    {o.baseSalary !== null ? money(o.baseSalary) : <span className="font-sans text-sm text-ink-400">Hidden</span>}
                  </td>
                  <td className="tnum px-5 py-3 font-mono text-sm text-ink-600">{o.sentAt ? shortDate(o.sentAt) : '—'}</td>
                  <td className="px-5 py-3">
                    <StatusPill status={o.status} />
                    {statusNote(o) && <span className="mt-0.5 block text-sm text-ink-600">{statusNote(o)}</span>}
                  </td>
                  <td className="px-5 py-3 text-right">
                    {/* Remind is offered only where it is meaningful: viewed but unsigned (the design's
                        rule). The API also allows it for an unopened offer, from the offer itself. */}
                    {staff && o.status === 'Viewed' ? (
                      <Button
                        variant="secondary" onClick={() => void remind(o)}
                        disabled={reminding === o.id || remindedRecently(o.lastRemindedAt)}
                        title={remindedRecently(o.lastRemindedAt) ? 'Reminded less than a day ago' : undefined}
                      >
                        Remind
                      </Button>
                    ) : canPreboard && o.status === 'Accepted' ? (
                      // An accepted offer's next work is its joining documents (the design's link); the
                      // offer itself is one click on from there.
                      <Link to={`/offers/${o.id}/preboarding`} className="text-sm font-medium text-brand-700 hover:text-brand-800">
                        Pre-boarding
                      </Link>
                    ) : (
                      <Link to={`/offers/${o.id}`} className="text-sm font-medium text-brand-700 hover:text-brand-800">
                        {o.awaitingMe ? 'Review' : 'Open'}
                      </Link>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {creating && <CreateOfferDialog onClose={() => setCreating(false)} />}
    </>
  );
}

/**
 * "Create offer" — pick a candidate at the Offer stage and the template to start the letter from.
 * Not drawn in `design/`: the dashboard draws the button, not what it opens. Kept to the two
 * choices the server needs; everything else is filled in on the offer itself.
 */
function CreateOfferDialog({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const [eligible, setEligible] = useState<OfferEligibleApplication[] | null>(null);
  const [templates, setTemplates] = useState<OfferTemplate[]>([]);
  const [applicationId, setApplicationId] = useState('');
  const [templateId, setTemplateId] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<OfferEligibleApplication[]>('/offers/eligible-applications')
      .then(setEligible)
      .catch((e) => setError(e instanceof Error ? e.message : 'Could not load candidates.'));
    api<OfferTemplate[]>('/offer-templates').then((t) => setTemplates(t.filter((x) => x.isActive))).catch(() => setTemplates([]));
  }, []);

  async function create() {
    setBusy(true);
    setError(null);
    try {
      const body: CreateOfferRequest = { jobApplicationId: applicationId, offerTemplateId: templateId || null };
      const offer = await api<OfferDetail>('/offers', { method: 'POST', body: JSON.stringify(body) });
      navigate(`/offers/${offer.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not create the offer.');
      setBusy(false);
    }
  }

  return (
    <Dialog isOpen onClose={onClose} size="lg" title="Create offer" description="For a candidate at the Offer stage.">
      <div className="space-y-4 px-6 py-5">
        {eligible === null && !error && <p className="text-ink-600">Loading…</p>}
        {eligible && eligible.length === 0 && (
          <p className="text-md text-ink-600">
            No candidate is waiting for an offer. Move one to the Offer stage on a posting's board first.
          </p>
        )}
        {eligible && eligible.length > 0 && (
          <fieldset>
            <legend className="mb-2 text-sm font-semibold">Candidate</legend>
            <ul className="max-h-72 divide-y divide-line overflow-y-auto rounded-md border border-line">
              {eligible.map((a) => (
                <li key={a.jobApplicationId}>
                  <label className="flex cursor-pointer items-start gap-3 px-3 py-2.5 hover:bg-canvas">
                    <input
                      type="radio" name="application" value={a.jobApplicationId} className="mt-1"
                      checked={applicationId === a.jobApplicationId} onChange={() => setApplicationId(a.jobApplicationId)}
                    />
                    <span>
                      <span className="block font-medium">{a.candidateName}</span>
                      <span className="block text-sm text-ink-600">{a.position} · {a.departmentName}</span>
                    </span>
                  </label>
                </li>
              ))}
            </ul>
          </fieldset>
        )}
        <div>
          <label htmlFor="offer-template" className="mb-1 block text-sm font-semibold">Offer letter template</label>
          <select
            id="offer-template" value={templateId} onChange={(e) => setTemplateId(e.target.value)}
            className="h-10 w-full rounded-md border border-line bg-white px-3"
          >
            {templates.map((t) => <option key={t.id ?? 'default'} value={t.id ?? ''}>{t.name}</option>)}
          </select>
        </div>
        {error && <p role="alert" className="text-md text-critical-700">{error}</p>}
      </div>
      <div className="flex justify-end gap-2.5 border-t border-line px-6 py-3.5">
        <Button variant="secondary" onClick={onClose} disabled={busy}>Cancel</Button>
        <Button onClick={create} disabled={busy || !applicationId}>{busy ? 'Creating…' : 'Create offer'}</Button>
      </div>
    </Dialog>
  );
}
