'use client';

import { useState } from 'react';
import type {
  ApplicationFormField, SubmitApplicationRequest, SubmitApplicationResponse,
} from '@recruitops/types';
import { parseFormFields } from '@recruitops/types';
import { api } from '../../../lib/api';

/**
 * The application form (Module 2.2).
 *
 * This is the only Client Component on the public app — the page around it stays a Server
 * Component so the Open Graph metadata is still rendered on the server, which is the entire
 * reason this app is SSR (ADR-0012). Making the whole page interactive to get one form
 * would throw that away.
 */
export function ApplicationForm({
  token,
  formFieldsJson,
}: {
  token: string;
  formFieldsJson: string | null;
}) {
  const fields = parseFormFields(formFieldsJson);

  const [form, setForm] = useState<SubmitApplicationRequest>({
    fullName: '', email: '', phone: '', coverNote: '',
  });
  // Answers are held as strings regardless of field type; the server coerces and is the
  // authority on what each type accepts. Duplicating that coercion here would give two
  // places to disagree about what "7" or "true" means.
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const setAnswer = (key: string, value: string) =>
    setAnswers((prev) => ({ ...prev, [key]: value }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();

    // Mirrors the server rule: without a way to contact them, nobody can ever tell the
    // applicant the outcome. Checked here too so they find out before losing their typing.
    if (!form.email?.trim() && !form.phone?.trim()) {
      setError('Please give us either an email address or a phone number.');
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const payload: SubmitApplicationRequest = {
        ...form,
        customFieldsJson: fields.length > 0 ? JSON.stringify(answers) : null,
      };
      const res = await api<SubmitApplicationResponse>(
        `/public/jobs/${encodeURIComponent(token)}/apply`,
        { method: 'POST', body: JSON.stringify(payload) },
      );
      setDone(res.message);
    } catch {
      // No server detail is echoed: this page is public, and the API's messages are not
      // written for strangers.
      setError('Something went wrong sending your application. Please try again.');
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    // The kit's success panel (`design/public/apply.html`): a bordered card, centred, with the
    // message set at a readable measure rather than run to the container's full width.
    return (
      <div className="rounded-lg border border-line bg-white p-5 text-center">
        <span className="mx-auto grid h-12 w-12 place-items-center rounded-full border border-line bg-canvas">
          <svg className="h-6 w-6 text-brand-700" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M5 12.5l4.5 4.5L19 7.5"
              stroke="currentColor"
              strokeWidth="1.8"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </span>
        <h2 className="mt-4 text-xl font-semibold tracking-tight">Application received</h2>
        <p className="mx-auto mt-2 max-w-[44ch] text-base text-ink-600">{done}</p>
      </div>
    );
  }

  return (
    <>
      <h2 className="text-xl font-semibold tracking-tight">Apply for this role</h2>
      <p className="mt-2 text-base text-ink-600">No account needed.</p>

      {error && (
        <div role="alert" className="mt-4 rounded-md border border-critical-100 bg-critical-50 px-3.5 py-3">
          <p className="text-base font-medium text-critical-700">{error}</p>
        </div>
      )}

      <form onSubmit={submit} className="mt-6 space-y-5">
        <div>
          <label htmlFor="fullName" className={labelClass}>
            Full name <span className="text-critical-500">*</span>
          </label>
          <input
            id="fullName" required maxLength={200} className={inputClass}
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="email" className={labelClass}>Email</label>
            <input
              id="email" type="email" maxLength={256} className={inputClass}
              value={form.email ?? ''}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
            />
          </div>
          <div>
            <label htmlFor="phone" className={labelClass}>Phone</label>
            <input
              id="phone" type="tel" maxLength={30} className={`${inputClass} tnum`}
              value={form.phone ?? ''}
              onChange={(e) => setForm({ ...form, phone: e.target.value })}
            />
          </div>
        </div>
        <p className="text-sm text-ink-600">
          Give at least one of email or phone so we can reach you.
        </p>

        {/* Customer-defined questions (Module 2.2). The schema comes from the posting;
            the server validates the answers against it and rebuilds them, so nothing here
            is trusted. */}
        {fields.map((f) => (
          <CustomField
            key={f.key}
            field={f}
            value={answers[f.key] ?? ''}
            onChange={(v) => setAnswer(f.key, v)}
          />
        ))}

        <div>
          <label htmlFor="coverNote" className={labelClass}>
            Anything you&apos;d like to add <span className="font-normal text-ink-600">(optional)</span>
          </label>
          <textarea
            id="coverNote" rows={5} maxLength={4000}
            className={textareaClass}
            value={form.coverNote ?? ''}
            onChange={(e) => setForm({ ...form, coverNote: e.target.value })}
          />
        </div>

        <button
          type="submit" disabled={busy}
          className="h-12 w-full rounded-md bg-brand-700 text-md font-medium text-white
            transition-colors hover:bg-brand-800 disabled:bg-ink-400/40"
        >
          {busy ? 'Sending…' : 'Submit application'}
        </button>
      </form>
    </>
  );
}

// Built against `design/public/apply.html`.
//
// ⚠️ These are deliberately BIGGER than the internal app's controls — h-12 and 15px against h-9
// and 14px. The internal app is an operations tool a recruiter lives in all day, where density is
// the point; this form is filled once, by a stranger, very often on a phone. Copying the internal
// `Input` here would be consistency in the wrong direction.
//
// `focus:border-brand-700` rather than a ring: `index.css`/`globals.css` already give every
// focusable element the same 2px `:focus-visible` outline, and a ring on top of that is two focus
// treatments fighting.
const labelClass = 'block text-base font-medium';
const inputClass =
  'mt-2 h-12 w-full rounded-md border border-line bg-white px-3.5 text-md outline-none ' +
  'transition-colors focus:border-brand-700';
const textareaClass =
  'mt-2 w-full rounded-md border border-line bg-white p-3.5 text-md outline-none ' +
  'transition-colors focus:border-brand-700';

/** Renders one customer-defined question. `required` is mirrored onto the input so the
 *  browser catches it before a round-trip, but the server enforces it either way. */
function CustomField({
  field, value, onChange,
}: {
  field: ApplicationFormField;
  value: string;
  onChange: (value: string) => void;
}) {
  const id = `cf_${field.key}`;

  if (field.type === 'checkbox') {
    return (
      <label className="flex items-start gap-2.5 text-base">
        <input
          id={id} type="checkbox" className="mt-1 h-4 w-4 accent-brand-700"
          required={field.required}
          checked={value === 'true'}
          onChange={(e) => onChange(String(e.target.checked))}
        />
        <span>
          {field.label}
          {field.required && <span className="text-critical-500"> *</span>}
        </span>
      </label>
    );
  }

  return (
    <div>
      <label htmlFor={id} className={labelClass}>
        {field.label}
        {field.required && <span className="text-critical-500"> *</span>}
      </label>

      {field.type === 'textarea' ? (
        <textarea
          id={id} rows={4} maxLength={2000} required={field.required}
          className={textareaClass}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
      ) : field.type === 'select' ? (
        <select
          id={id} required={field.required} className={inputClass}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">Choose…</option>
          {(field.options ?? []).map((o) => (
            <option key={o} value={o}>{o}</option>
          ))}
        </select>
      ) : (
        <input
          id={id}
          type={field.type === 'number' ? 'number' : field.type === 'date' ? 'date' : 'text'}
          maxLength={field.type === 'text' ? 500 : undefined}
          required={field.required}
          className={inputClass}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
    </div>
  );
}
