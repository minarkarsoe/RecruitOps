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
    return (
      <>
        <h2 className="font-display text-[19px] font-semibold">Application received</h2>
        <p className="mt-2 text-[15px] text-ink-600">{done}</p>
      </>
    );
  }

  const field =
    'h-10 w-full rounded-sm border border-line-200 px-3 focus:outline-none focus:ring-2 focus:ring-primary-600';

  return (
    <>
      <h2 className="font-display text-[19px] font-semibold">Apply for this role</h2>
      <p className="mt-2 text-[15px] text-ink-600">No account needed.</p>

      {error && <p role="alert" className="mt-4 text-[15px] text-danger-600">{error}</p>}

      <form onSubmit={submit} className="mt-6 space-y-4">
        <div>
          <label htmlFor="fullName" className="mb-1 block text-[13px] font-semibold">
            Full name
          </label>
          <input
            id="fullName" required maxLength={200} className={field}
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="email" className="mb-1 block text-[13px] font-semibold">Email</label>
            <input
              id="email" type="email" maxLength={256} className={field}
              value={form.email ?? ''}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
            />
          </div>
          <div>
            <label htmlFor="phone" className="mb-1 block text-[13px] font-semibold">Phone</label>
            <input
              id="phone" type="tel" maxLength={30} className={field}
              value={form.phone ?? ''}
              onChange={(e) => setForm({ ...form, phone: e.target.value })}
            />
          </div>
        </div>
        <p className="text-[13px] text-ink-400">
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
          <label htmlFor="coverNote" className="mb-1 block text-[13px] font-semibold">
            Anything you&apos;d like to add <span className="font-normal text-ink-400">(optional)</span>
          </label>
          <textarea
            id="coverNote" rows={5} maxLength={4000}
            className="w-full rounded-sm border border-line-200 p-3 focus:outline-none focus:ring-2 focus:ring-primary-600"
            value={form.coverNote ?? ''}
            onChange={(e) => setForm({ ...form, coverNote: e.target.value })}
          />
        </div>

        <button
          type="submit" disabled={busy}
          className="h-10 rounded-sm bg-primary-600 px-5 text-[15px] font-semibold text-white disabled:opacity-50"
        >
          {busy ? 'Sending…' : 'Submit application'}
        </button>
      </form>
    </>
  );
}

const inputClass =
  'h-10 w-full rounded-sm border border-line-200 px-3 focus:outline-none focus:ring-2 focus:ring-primary-600';

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
      <label className="flex items-start gap-2 text-[15px]">
        <input
          id={id} type="checkbox" className="mt-1"
          required={field.required}
          checked={value === 'true'}
          onChange={(e) => onChange(String(e.target.checked))}
        />
        <span>
          {field.label}
          {field.required && <span className="text-danger-600"> *</span>}
        </span>
      </label>
    );
  }

  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-[13px] font-semibold">
        {field.label}
        {field.required && <span className="text-danger-600"> *</span>}
      </label>

      {field.type === 'textarea' ? (
        <textarea
          id={id} rows={4} maxLength={2000} required={field.required}
          className="w-full rounded-sm border border-line-200 p-3 focus:outline-none focus:ring-2 focus:ring-primary-600"
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
