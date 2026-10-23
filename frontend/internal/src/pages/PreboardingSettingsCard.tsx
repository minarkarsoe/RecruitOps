import { useEffect, useState } from 'react';
import { Button, Card } from '@recruitops/ui';
import type { PreboardingSettings, UpdatePreboardingSettingsRequest } from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';

/**
 * Module 4.3 — what a candidate is asked for after accepting, and where the IT and Admin handoffs go
 * (ADR-0030 §5b). Drawn in `design/internal/settings-integrations.html` ("Pre-boarding"); it sits here,
 * beside Offer policy, until System Settings exists in the app. Changes are audited by the API.
 */

type Flag = 'nrcRequired' | 'bankAccountRequired' | 'educationRequired' | 'photoRequired';

const ITEMS: { flag: Flag; label: string; note?: string }[] = [
  { flag: 'nrcRequired', label: 'NRC — front and back' },
  { flag: 'bankAccountRequired', label: 'Bank account details', note: 'Typed, with a photo of the bank book to check them against.' },
  { flag: 'educationRequired', label: 'Education certificates', note: 'Up to 5 files.' },
  { flag: 'photoRequired', label: 'Photo for the staff card', note: 'Goes to Admin with the handoff, once approved.' },
];

function errorMessage(e: unknown, fallback: string): string {
  return e instanceof Error && e.message.trim() ? e.message : fallback;
}

export function PreboardingSettingsCard() {
  const canEdit = hasPermission(auth.get(), 'permission:settings:settings:update');
  const [settings, setSettings] = useState<PreboardingSettings | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    api<PreboardingSettings>('/settings/preboarding').then(setSettings).catch(() => setSettings(null));
  }, []);

  if (!settings) return null;

  async function save() {
    if (!settings) return;
    setBusy(true);
    setMessage(null);
    setFailed(false);
    try {
      const body: UpdatePreboardingSettingsRequest = {
        ...settings,
        itHandoffEmail: settings.itHandoffEmail?.trim() || null,
        adminHandoffEmail: settings.adminHandoffEmail?.trim() || null,
      };
      setSettings(await api<PreboardingSettings>('/settings/preboarding', { method: 'PUT', body: JSON.stringify(body) }));
      setMessage('Saved.');
    } catch (e) {
      setFailed(true);
      setMessage(errorMessage(e, 'Could not save the pre-boarding settings.'));
    } finally {
      setBusy(false);
    }
  }

  const input = 'mt-1.5 h-10 w-full rounded-md border border-line bg-white px-3 text-md focus:outline-none focus:ring-2 focus:ring-brand-700 disabled:bg-canvas';

  return (
    <div className="mb-6" id="preboarding">
      <Card>
        <h2 className="text-base font-semibold">Pre-boarding</h2>
        <p className="mt-1 text-sm text-ink-600">
          What a candidate is asked for after accepting, and who is told when they are ready. Applies to offers accepted
          from now on — a candidate already collecting documents keeps the list they were given.
        </p>

        <h3 className="mt-4 text-md font-medium">Joining documents</h3>
        <p className="text-sm text-ink-600">Every item is offered. A required one has to be approved before the departments are told.</p>
        <ul className="mt-2 divide-y divide-line rounded-md border border-line">
          {ITEMS.map(({ flag, label, note }) => (
            <li key={flag} className="flex flex-wrap items-center gap-4 px-4 py-3">
              <div className="min-w-0 flex-1">
                <p className="text-md font-medium">{label}</p>
                {note && <p className="text-sm text-ink-600">{note}</p>}
              </div>
              <div role="radiogroup" aria-label={label} className="inline-flex shrink-0 rounded-md border border-line bg-canvas p-0.5">
                {[true, false].map((required) => (
                  <button
                    key={String(required)} type="button" role="radio" aria-checked={settings[flag] === required}
                    disabled={!canEdit || busy}
                    onClick={() => setSettings({ ...settings, [flag]: required })}
                    className={`h-8 rounded px-3 text-sm ${settings[flag] === required ? 'bg-white font-medium shadow-sm' : 'text-ink-600'}`}
                  >
                    {required ? 'Required' : 'Optional'}
                  </button>
                ))}
              </div>
            </li>
          ))}
        </ul>

        <h3 className="mt-4 text-md font-medium">Department handoff</h3>
        <p className="text-sm text-ink-600">
          Emailed once every required document is approved. A team with no address is not sent anything, and the
          pre-boarding screen says so.
        </p>
        <div className="mt-3 grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="it-handoff" className="block text-sm font-medium text-ink-700">IT — email account and laptop</label>
            <input
              id="it-handoff" type="email" className={input} disabled={!canEdit || busy}
              value={settings.itHandoffEmail ?? ''} onChange={(e) => setSettings({ ...settings, itHandoffEmail: e.target.value })}
            />
            <p className="mt-1.5 text-sm text-ink-600">Name, position and start date. Nothing else.</p>
          </div>
          <div>
            <label htmlFor="admin-handoff" className="block text-sm font-medium text-ink-700">Admin — desk and staff card</label>
            <input
              id="admin-handoff" type="email" className={input} disabled={!canEdit || busy}
              value={settings.adminHandoffEmail ?? ''} onChange={(e) => setSettings({ ...settings, adminHandoffEmail: e.target.value })}
            />
            <p className="mt-1.5 text-sm text-ink-600">Name, department, start date and the approved photo.</p>
          </div>
        </div>

        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-4">
          <p className="text-sm text-ink-600">Changes are recorded in the audit log: they decide where a new employee&apos;s photo is sent.</p>
          {canEdit && <Button onClick={() => void save()} disabled={busy}>{busy ? 'Saving…' : 'Save'}</Button>}
        </div>
        {message && (
          <p role={failed ? 'alert' : 'status'} className={`mt-3 text-sm ${failed ? 'text-critical-700' : 'text-ink-600'}`}>{message}</p>
        )}
      </Card>
    </div>
  );
}
