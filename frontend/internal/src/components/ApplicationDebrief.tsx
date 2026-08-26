import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, StatusPill } from '@recruitops/ui';
import type {
  Interview, InterviewMode, ScheduleInterviewRequest, SelectableUser,
} from '@recruitops/types';
import { api } from '../lib/api';
import { auth, isRecruitmentStaff } from '../lib/auth';
import { ApplicationNotes } from './ApplicationNotes';

/**
 * Everything the hiring side does against one job application after screening: its
 * interview rounds (3.1/3.3) and its debrief thread (3.4).
 *
 * The two are one component because the thread's round picker needs the rounds, and this is
 * the only place either is mounted — splitting them would mean either a second request for
 * the same list or a prop drilled through the pipeline board for no other reason.
 *
 * Lives on the pipeline board rather than on a candidate page of its own because there is
 * no `GET /api/applications/{id}` — the candidate's name reaches us only through the
 * pipeline row, so this component takes what it needs as props instead of refetching it.
 */

const MODES: { value: InterviewMode; label: string }[] = [
  { value: 'OnSite', label: 'On site' },
  { value: 'Video', label: 'Video' },
  { value: 'Phone', label: 'Phone' },
];

/** What the `location` field means depends on the mode; one field, three questions. */
function locationLabel(mode: InterviewMode): string {
  if (mode === 'Video') return 'Meeting link';
  if (mode === 'Phone') return 'Phone number';
  return 'Location';
}

/**
 * `datetime-local` hands back "2026-08-03T14:30" with no zone, which `new Date()` reads as
 * local time — which is what the scheduler meant. `toISOString()` then attaches the offset,
 * so the API stores an unambiguous instant rather than a wall-clock string that means
 * something different to a panel member in another timezone.
 */
function toIsoInstant(localValue: string): string {
  return new Date(localValue).toISOString();
}

/** The inverse, for pre-filling the reschedule form from a stored instant. */
function toLocalInputValue(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function formatSlot(iso: string, minutes: number): string {
  const start = new Date(iso);
  const end = new Date(start.getTime() + minutes * 60_000);
  const time = (d: Date) => d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  return `${start.toLocaleDateString([], { weekday: 'short', day: 'numeric', month: 'short' })} · ${time(start)}–${time(end)}`;
}

const field =
  'h-10 w-full rounded-md border border-line px-3 text-md focus:outline-none focus:ring-2 focus:ring-brand-700';

// ---------------------------------------------------------------------------

/**
 * Panel picker. A plain checkbox list rather than a multi-select: the panel is usually two
 * or three people out of a few dozen, and a native multi-select silently loses a selection
 * on a stray click — an interview scheduled with the wrong panel is only discovered when
 * someone cannot open their scorecard.
 */
function PanelPicker({
  users, selected, leadUserId, onChange, onLeadChange,
}: {
  users: SelectableUser[];
  selected: string[];
  leadUserId: string | null;
  onChange: (ids: string[]) => void;
  onLeadChange: (id: string | null) => void;
}) {
  function toggle(id: string) {
    const next = selected.includes(id)
      ? selected.filter((s) => s !== id)
      : [...selected, id];
    onChange(next);
    // Dropping the lead from the panel must clear the lead too — the API rejects a lead who
    // is not a participant, and failing that check at submit time is a worse place to learn.
    if (leadUserId && !next.includes(leadUserId)) onLeadChange(null);
  }

  return (
    <div>
      <p className="mb-1 text-sm font-semibold">Panel</p>
      <div className="max-h-56 overflow-y-auto rounded-md border border-line p-2">
        {users.length === 0 ? (
          <p className="p-2 text-sm text-ink-400">No users to choose from.</p>
        ) : (
          users.map((u) => (
            <label key={u.id} className="flex items-center gap-2 rounded-md px-2 py-1.5 text-md hover:bg-canvas">
              <input
                type="checkbox"
                checked={selected.includes(u.id)}
                onChange={() => toggle(u.id)}
              />
              <span className="flex-1">{u.displayName}</span>
              <span className="text-sm text-ink-400">{u.role}</span>
            </label>
          ))
        )}
      </div>

      {selected.length > 0 && (
        <div className="mt-2">
          <label htmlFor="lead" className="mb-1 block text-sm font-semibold">
            Lead <span className="font-normal text-ink-400">(optional)</span>
          </label>
          <select
            id="lead"
            className={field}
            value={leadUserId ?? ''}
            onChange={(e) => onLeadChange(e.target.value || null)}
          >
            <option value="">No lead</option>
            {users.filter((u) => selected.includes(u.id)).map((u) => (
              <option key={u.id} value={u.id}>{u.displayName}</option>
            ))}
          </select>
        </div>
      )}

      <p className="mt-1 text-sm text-ink-400">
        Panel members can read this one application and write their own scorecard — nothing
        else, and nothing in their department.
      </p>
    </div>
  );
}

// ---------------------------------------------------------------------------

interface ScheduleFormState {
  scheduledStart: string;
  durationMinutes: number;
  mode: InterviewMode;
  location: string;
  agenda: string;
  participantUserIds: string[];
  leadUserId: string | null;
}

function emptyForm(): ScheduleFormState {
  return {
    scheduledStart: '',
    durationMinutes: 60,
    mode: 'OnSite',
    location: '',
    agenda: '',
    participantUserIds: [],
    leadUserId: null,
  };
}

function ScheduleForm({
  users, busy, onCancel, onSubmit,
}: {
  users: SelectableUser[];
  busy: boolean;
  onCancel: () => void;
  onSubmit: (body: ScheduleInterviewRequest) => void;
}) {
  const [form, setForm] = useState<ScheduleFormState>(emptyForm);

  const incomplete = !form.scheduledStart || form.participantUserIds.length === 0;

  return (
    <form
      className="space-y-4 rounded-md border border-line bg-canvas p-4"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({
          scheduledStart: toIsoInstant(form.scheduledStart),
          durationMinutes: form.durationMinutes,
          mode: form.mode,
          location: form.location || null,
          agenda: form.agenda || null,
          participantUserIds: form.participantUserIds,
          leadUserId: form.leadUserId,
        });
      }}
    >
      <div className="grid grid-cols-3 gap-4">
        <div>
          <label htmlFor="start" className="mb-1 block text-sm font-semibold">Starts</label>
          <input
            id="start" type="datetime-local" required className={field}
            value={form.scheduledStart}
            onChange={(e) => setForm({ ...form, scheduledStart: e.target.value })}
          />
        </div>
        <div>
          <label htmlFor="duration" className="mb-1 block text-sm font-semibold">Minutes</label>
          <input
            id="duration" type="number" min={5} max={480} required className={field}
            value={form.durationMinutes}
            onChange={(e) => setForm({ ...form, durationMinutes: Number(e.target.value) })}
          />
        </div>
        <div>
          <label htmlFor="mode" className="mb-1 block text-sm font-semibold">Mode</label>
          <select
            id="mode" className={field} value={form.mode}
            onChange={(e) => setForm({ ...form, mode: e.target.value as InterviewMode })}
          >
            {MODES.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
          </select>
        </div>
      </div>

      <div>
        <label htmlFor="location" className="mb-1 block text-sm font-semibold">
          {locationLabel(form.mode)}
        </label>
        <input
          id="location" className={field} value={form.location}
          onChange={(e) => setForm({ ...form, location: e.target.value })}
        />
      </div>

      <div>
        <label htmlFor="agenda" className="mb-1 block text-sm font-semibold">
          Agenda <span className="font-normal text-ink-400">(shown to the panel)</span>
        </label>
        <textarea
          id="agenda" rows={3}
          className="w-full rounded-md border border-line p-3 text-md focus:outline-none focus:ring-2 focus:ring-brand-700"
          value={form.agenda}
          onChange={(e) => setForm({ ...form, agenda: e.target.value })}
        />
      </div>

      <PanelPicker
        users={users}
        selected={form.participantUserIds}
        leadUserId={form.leadUserId}
        onChange={(ids) => setForm({ ...form, participantUserIds: ids })}
        onLeadChange={(id) => setForm({ ...form, leadUserId: id })}
      />

      <p className="text-sm text-ink-600">
        Scheduling also moves this candidate to <strong>Interview</strong> and records the move
        in the stage history.
      </p>

      <div className="flex gap-3">
        <Button type="submit" disabled={busy || incomplete}>
          {busy ? 'Scheduling…' : 'Schedule interview'}
        </Button>
        <Button variant="secondary" type="button" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}

// ---------------------------------------------------------------------------

function InterviewRow({
  interview, users, busy, canManage, onReschedule, onSetPanel, onCancelRound, onComplete,
}: {
  interview: Interview;
  users: SelectableUser[];
  busy: boolean;
  canManage: boolean;
  onReschedule: (id: string, body: Omit<ScheduleInterviewRequest, 'participantUserIds' | 'leadUserId'>) => void;
  onSetPanel: (id: string, participantUserIds: string[], leadUserId: string | null) => void;
  onCancelRound: (id: string, reason: string) => void;
  onComplete: (id: string, noShow: boolean) => void;
}) {
  const [editing, setEditing] = useState<'none' | 'slot' | 'panel'>('none');
  const [slot, setSlot] = useState(() => ({
    scheduledStart: toLocalInputValue(interview.scheduledStart),
    durationMinutes: interview.durationMinutes,
    mode: interview.mode,
    location: interview.location ?? '',
    agenda: interview.agenda ?? '',
  }));
  const [panel, setPanel] = useState<string[]>(
    () => interview.participants.map((p) => p.userId),
  );
  const [lead, setLead] = useState<string | null>(
    () => interview.participants.find((p) => p.isLead)?.userId ?? null,
  );

  const open = interview.status === 'Scheduled';
  const submitted = interview.participants.filter((p) => p.hasSubmittedScorecard).length;

  return (
    <li className="py-4">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-semibold">Round {interview.round}</span>
            <StatusPill status={interview.status} />
          </div>
          <p className="mt-0.5 text-sm text-ink-600">
            {formatSlot(interview.scheduledStart, interview.durationMinutes)} ·{' '}
            {MODES.find((m) => m.value === interview.mode)?.label}
            {interview.location ? ` · ${interview.location}` : ''}
          </p>

          <p className="mt-1 text-sm text-ink-600">
            {interview.participants.map((p) => p.displayName + (p.isLead ? ' (lead)' : '')).join(', ')}
            {' — '}
            <span className="text-ink-400">
              {submitted} of {interview.participants.length} scorecards in
            </span>
          </p>

          {interview.agenda && (
            <p className="mt-2 max-w-[60ch] whitespace-pre-wrap rounded-md bg-canvas p-2 text-sm text-ink-600">
              {interview.agenda}
            </p>
          )}

          {interview.status === 'Cancelled' && interview.cancellationReason && (
            <p className="mt-2 text-sm text-ink-600">
              Cancelled — {interview.cancellationReason}
            </p>
          )}
        </div>

        <div className="flex shrink-0 flex-col items-end gap-2">
          {/* Available whatever the round's status: a completed interview is exactly when
              the panel wants to read the debrief, and a cancelled one may still carry the
              scorecard someone had already started. */}
          <Link
            to={`/interviews/${interview.id}`}
            className="text-sm font-semibold text-brand-700 hover:underline"
          >
            Scorecards →
          </Link>

          {canManage && open && (
            <div className="flex flex-wrap justify-end gap-2">
              <Button variant="secondary" onClick={() => setEditing(editing === 'slot' ? 'none' : 'slot')}>
                Reschedule
              </Button>
              <Button variant="secondary" onClick={() => setEditing(editing === 'panel' ? 'none' : 'panel')}>
                Panel
              </Button>
              <Button variant="secondary" disabled={busy} onClick={() => onComplete(interview.id, false)}>
                Complete
              </Button>
              <Button variant="secondary" disabled={busy} onClick={() => onComplete(interview.id, true)}>
                No-show
              </Button>
              <Button
                variant="danger"
                disabled={busy}
                onClick={() => {
                  const reason = window.prompt('Why is this round being cancelled?');
                  if (reason !== null) onCancelRound(interview.id, reason);
                }}
              >
                Cancel round
              </Button>
            </div>
          )}
        </div>
      </div>

      {canManage && editing === 'slot' && (
        <form
          className="mt-3 space-y-3 rounded-md border border-line bg-canvas p-4"
          onSubmit={(e) => {
            e.preventDefault();
            onReschedule(interview.id, {
              scheduledStart: toIsoInstant(slot.scheduledStart),
              durationMinutes: slot.durationMinutes,
              mode: slot.mode,
              location: slot.location || null,
              agenda: slot.agenda || null,
            });
            setEditing('none');
          }}
        >
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="mb-1 block text-sm font-semibold">Starts</label>
              <input
                type="datetime-local" required className={field} value={slot.scheduledStart}
                onChange={(e) => setSlot({ ...slot, scheduledStart: e.target.value })}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-semibold">Minutes</label>
              <input
                type="number" min={5} max={480} required className={field} value={slot.durationMinutes}
                onChange={(e) => setSlot({ ...slot, durationMinutes: Number(e.target.value) })}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-semibold">Mode</label>
              <select
                className={field} value={slot.mode}
                onChange={(e) => setSlot({ ...slot, mode: e.target.value as InterviewMode })}
              >
                {MODES.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-semibold">{locationLabel(slot.mode)}</label>
            <input
              className={field} value={slot.location}
              onChange={(e) => setSlot({ ...slot, location: e.target.value })}
            />
          </div>
          {/* The panel is deliberately absent here — rescheduling and swapping an interviewer
              are different intentions, and one form doing both is how a panel gets wiped by
              an omitted field. */}
          <div className="flex gap-3">
            <Button type="submit" disabled={busy}>Save new time</Button>
            <Button variant="secondary" type="button" onClick={() => setEditing('none')}>Cancel</Button>
          </div>
        </form>
      )}

      {canManage && editing === 'panel' && (
        <form
          className="mt-3 space-y-3 rounded-md border border-line bg-canvas p-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSetPanel(interview.id, panel, lead);
            setEditing('none');
          }}
        >
          <PanelPicker
            users={users}
            selected={panel}
            leadUserId={lead}
            onChange={setPanel}
            onLeadChange={setLead}
          />
          <p className="text-sm text-ink-600">
            Someone who has already started a scorecard cannot be removed — their evaluation
            exists and dropping them would orphan it.
          </p>
          <div className="flex gap-3">
            <Button type="submit" disabled={busy || panel.length === 0}>Save panel</Button>
            <Button variant="secondary" type="button" onClick={() => setEditing('none')}>Cancel</Button>
          </div>
        </form>
      )}
    </li>
  );
}

// ---------------------------------------------------------------------------

export function ApplicationDebrief({
  applicationId, onChanged,
}: {
  applicationId: string;
  /** Called after anything that can move the application's stage, so the board can reload. */
  onChanged?: () => void;
}) {
  const role = auth.get()?.role;
  const canManage = role ? isRecruitmentStaff(role) : false;

  const [interviews, setInterviews] = useState<Interview[] | null>(null);
  const [users, setUsers] = useState<SelectableUser[]>([]);
  const [scheduling, setScheduling] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setInterviews(await api<Interview[]>(`/applications/${applicationId}/interviews`));
  }, [applicationId]);

  useEffect(() => {
    load().catch((e) =>
      setError(e instanceof Error ? e.message : 'Could not load the interviews.'));
  }, [load]);

  // The directory is fetched once, only for those who can act on it — a Hiring Manager
  // reading the round would get a 403 they have no use for.
  useEffect(() => {
    if (!canManage) return;
    api<SelectableUser[]>('/users/selectable')
      .then(setUsers)
      .catch(() => setUsers([]));
  }, [canManage]);

  async function act(run: () => Promise<unknown>) {
    setBusy(true);
    setError(null);
    try {
      await run();
      await load();
      onChanged?.();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  if (error && interviews === null) {
    return <p role="alert" className="text-sm text-critical-700">{error}</p>;
  }
  if (interviews === null) return <p className="text-sm text-ink-600">Loading interviews…</p>;

  return (
    <>
    <div className="mt-3 rounded-md border border-line p-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-ink-600">
          Interviews · {interviews.length}
        </h3>
        {canManage && !scheduling && (
          <Button variant="secondary" onClick={() => setScheduling(true)}>
            Schedule {interviews.length > 0 ? 'another round' : 'interview'}
          </Button>
        )}
      </div>

      {error && <p role="alert" className="mt-2 text-sm text-critical-700">{error}</p>}

      {scheduling && (
        <div className="mt-4">
          <ScheduleForm
            users={users}
            busy={busy}
            onCancel={() => setScheduling(false)}
            onSubmit={(body) => act(async () => {
              await api(`/applications/${applicationId}/interviews`, {
                method: 'POST', body: JSON.stringify(body),
              });
              setScheduling(false);
            })}
          />
        </div>
      )}

      {interviews.length === 0 && !scheduling ? (
        <p className="mt-2 text-sm text-ink-600">No rounds scheduled.</p>
      ) : (
        <ul className="divide-y divide-line">
          {interviews.map((iv) => (
            <InterviewRow
              key={iv.id}
              interview={iv}
              users={users}
              busy={busy}
              canManage={canManage}
              onReschedule={(id, body) => act(() =>
                api(`/interviews/${id}`, { method: 'PUT', body: JSON.stringify(body) }))}
              onSetPanel={(id, participantUserIds, leadUserId) => act(() =>
                api(`/interviews/${id}/panel`, {
                  method: 'PUT', body: JSON.stringify({ participantUserIds, leadUserId }),
                }))}
              onCancelRound={(id, reason) => act(() =>
                api(`/interviews/${id}/cancel`, {
                  method: 'POST', body: JSON.stringify({ reason: reason || null }),
                }))}
              onComplete={(id, noShow) => act(() =>
                api(`/interviews/${id}/complete`, {
                  method: 'POST', body: JSON.stringify({ noShow }),
                }))}
            />
          ))}
        </ul>
      )}
    </div>

    {/* The thread is not gated on `canManage`: a panel member from another department is
        exactly who this is for, and posting to it is the job they were added to do. */}
    <ApplicationNotes applicationId={applicationId} interviews={interviews} />
    </>
  );
}
