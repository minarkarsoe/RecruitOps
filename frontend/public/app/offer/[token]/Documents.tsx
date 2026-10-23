'use client';

import { useState, type ReactElement, type RefObject } from 'react';
import type {
  PortalDocument, PortalDocuments, PreboardingDocumentKind, SaveBankDetailsRequest,
} from '@recruitops/types';
import { api, ApiError, upload } from '../../../lib/api';

/**
 * Joining documents on the offer link (Module 4.3, ADR-0030 §5b), built from
 * `design/public/offer-portal.html` `#documents` and `#documents-sent` (redrawn 2026-10-23).
 *
 * Every file is saved the moment it uploads, so nothing is lost if the candidate stops halfway. What a
 * file *is* is decided by the server from its first bytes — the `accept` lists below only help the
 * phone's picker. Nothing uploaded is ever shown back except its name and size: the page has no way to
 * download a document, and the account number comes back masked.
 */

const MAX_BYTES = 10 * 1024 * 1024;

export const longDate = (date: string) =>
  new Date(`${date}T00:00:00Z`).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });

const longDay = (iso: string) => new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' });

function size(bytes: number): string {
  return bytes < 100 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

const primary = 'flex w-full h-12 items-center justify-center rounded-md bg-brand-700 hover:bg-brand-800 text-white text-base font-semibold transition-colors disabled:opacity-50';
const secondary = 'flex w-full h-12 items-center justify-center rounded-md bg-white border border-line hover:border-line-strong text-ink-900 text-base font-medium transition-colors disabled:opacity-60';
const slot = 'flex h-11 w-full cursor-pointer items-center justify-center rounded-md border border-line bg-white text-sm font-medium transition-colors hover:border-line-strong focus-within:ring-2 focus-within:ring-brand-700';
const field = 'mt-1.5 h-11 w-full rounded-md border border-line bg-white px-3 text-base focus:outline-none focus:ring-2 focus:ring-brand-700';

/** One item as the candidate sees it: NRC is one card holding both sides. */
type Item = 'Nrc' | 'BankAccount' | 'Education' | 'Photo';

const ITEMS: { item: Item; title: string; kinds: PreboardingDocumentKind[]; note?: string }[] = [
  { item: 'Nrc', title: 'NRC — front and back', kinds: ['NrcFront', 'NrcBack'] },
  { item: 'BankAccount', title: 'Bank account details', kinds: ['BankAccount'], note: 'for salary payment' },
  { item: 'Education', title: 'Education certificates', kinds: ['Education'] },
  { item: 'Photo', title: 'Photo for your staff card', kinds: ['Photo'] },
];

type ItemState = 'approved' | 'returned' | 'checking' | 'todo';

function stateOf(docs: PortalDocument[]): ItemState {
  if (docs.every((d) => d.status === 'Approved')) return 'approved';
  if (docs.some((d) => d.status === 'Returned')) return 'returned';
  if (docs.every((d) => d.complete || d.status === 'Approved')) return 'checking';
  return 'todo';
}

/** What the API wrote for the candidate (409), or ours. A 401 or 404 goes to the caller. */
function messageOf(e: unknown, fallback: string): string {
  return e instanceof ApiError && (e.status === 409 || e.status === 400) && e.detail ? e.detail : fallback;
}

const icons: Record<ItemState, ReactElement> = {
  approved: (
    <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-positive-50">
      <svg className="h-4 w-4 text-positive-700" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M3.5 8.5l3 3 6-6" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" /></svg>
    </span>
  ),
  returned: (
    <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-critical-50">
      <svg className="h-4 w-4 text-critical-700" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M8 4.5v4.2M8 11.2v.3" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" /></svg>
    </span>
  ),
  checking: (
    <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-positive-50">
      <svg className="h-4 w-4 text-positive-700" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M3.5 8.5l3 3 6-6" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" /></svg>
    </span>
  ),
  todo: (
    <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-line bg-canvas">
      <svg className="h-4 w-4 text-ink-600" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M8 11V4M5 6.5L8 3.5l3 3M3.5 12.5h9" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" /></svg>
    </span>
  ),
};

export function Documents({
  base, docs, onChange, onFail, onSent, onReadLetter, headingRef, companyName,
}: {
  base: string;
  docs: PortalDocuments;
  onChange: (next: PortalDocuments) => void;
  /** A 401 (the session ended) or 404 (the link closed): the page decides where to go. */
  onFail: (e: unknown) => boolean;
  onSent: (next: PortalDocuments) => void;
  onReadLetter: () => void;
  headingRef: RefObject<HTMLHeadingElement>;
  companyName: string;
}) {
  const [busy, setBusy] = useState<string | null>(null);
  const [errors, setErrors] = useState<Partial<Record<Item | 'send', string>>>({});
  const [bank, setBank] = useState<SaveBankDetailsRequest>(() => {
    const b = docs.documents.find((d) => d.kind === 'BankAccount')?.bank;
    return { bankName: b?.bankName ?? '', accountName: b?.accountName ?? '', accountNumber: '' };
  });

  const byKind = (kind: PreboardingDocumentKind) => docs.documents.find((d) => d.kind === kind)!;

  async function run(key: string, item: Item | 'send', action: () => Promise<PortalDocuments>, fallback: string) {
    setBusy(key);
    setErrors((e) => ({ ...e, [item]: undefined }));
    try {
      onChange(await action());
    } catch (e) {
      if (!onFail(e)) setErrors((all) => ({ ...all, [item]: messageOf(e, fallback) }));
    } finally {
      setBusy(null);
    }
  }

  function choose(item: Item, kind: PreboardingDocumentKind, file: File | undefined) {
    if (!file) return;
    if (file.size > MAX_BYTES) {
      setErrors((e) => ({ ...e, [item]: 'That file is over 10 MB. Choose a smaller one, or take a new photo.' }));
      return;
    }
    const form = new FormData();
    form.append('file', file, file.name);
    void run(`upload-${kind}`, item, () => upload<PortalDocuments>(`${base}/documents/${kind}/files`, form),
      'That file did not upload. Please try again.');
  }

  function remove(item: Item, id: string) {
    void run(`remove-${id}`, item, () => api<PortalDocuments>(`${base}/documents/files/${id}`, { method: 'DELETE' }),
      'That file could not be removed. Please try again.');
  }

  function saveBank(e: React.FormEvent) {
    e.preventDefault();
    void run('bank', 'BankAccount', async () => {
      const saved = await api<PortalDocuments>(`${base}/documents/bank`, { method: 'PUT', body: JSON.stringify(bank) });
      setBank((b) => ({ ...b, accountNumber: '' }));
      return saved;
    }, 'Your bank details could not be saved. Please try again.');
  }

  async function send() {
    setBusy('send');
    setErrors({});
    try {
      onSent(await api<PortalDocuments>(`${base}/documents/send`, { method: 'POST' }));
    } catch (e) {
      if (!onFail(e)) setErrors({ send: messageOf(e, 'They could not be sent just now. Please try again.') });
    } finally {
      setBusy(null);
    }
  }

  /** A file slot: the file's name with Replace, or the button to choose one. Called, not mounted as a
   *  component: a component declared in here would be a new type on every render, and remount. */
  function slotRow(item: Item, kind: PreboardingDocumentKind, label?: string, photoOnly?: boolean) {
    const doc = byKind(kind);
    const file = doc.files[0];
    const locked = doc.status === 'Approved';
    const accept = photoOnly ? 'image/jpeg,image/png' : 'image/jpeg,image/png,application/pdf';
    const id = `file-${kind}`;
    const chooser = (text: string, className: string) => (
      <label htmlFor={id} className={className}>
        <input
          id={id} type="file" accept={accept} className="sr-only" disabled={busy !== null}
          aria-label={label ? `${label} — ${text}` : text}
          onChange={(e) => { choose(item, kind, e.target.files?.[0]); e.target.value = ''; }}
        />
        {busy === `upload-${kind}` ? 'Uploading…' : text}
      </label>
    );
    return (
      <li key={kind} className="flex items-center gap-3 px-3 py-2.5">
        {label && <span className="w-12 shrink-0 text-sm text-ink-600">{label}</span>}
        {file ? (
          <>
            <span className="min-w-0 flex-1 truncate text-sm">
              {file.fileName} <span className="tnum text-ink-600">· {size(file.sizeBytes)}</span>
            </span>
            {!locked && chooser('Replace', 'flex h-11 shrink-0 cursor-pointer items-center px-2 text-sm font-medium text-brand-700 focus-within:ring-2 focus-within:ring-brand-700 rounded')}
          </>
        ) : (
          <div className="min-w-0 flex-1">{chooser('Choose file or take a photo', slot)}</div>
        )}
      </li>
    );
  }

  function status(item: Item, state: ItemState, required: boolean, note?: string) {
    if (state === 'approved') return <p className="text-sm text-positive-700">Checked — thank you</p>;
    if (state === 'returned') return <p className="text-sm text-critical-700">Please upload this again</p>;
    if (state === 'checking' && docs.sent) return <p className="text-sm text-ink-600">Being checked</p>;
    const need = required ? 'Required' : item === 'Photo' ? 'Optional — you can do this on day one instead' : 'Optional';
    return <p className="text-sm text-ink-600">{note ? `${need} · ${note}` : need}</p>;
  }

  return (
    <section>
      <div className="rounded-lg border border-positive-500/30 bg-positive-50 px-5 py-4">
        <p className="text-base font-semibold text-positive-700">Offer accepted — welcome</p>
        <p className="mt-1 text-sm text-positive-700/90">
          {docs.signedAt ? `Signed on ${longDay(docs.signedAt)}. ` : ''}A copy has been emailed to you.
        </p>
      </div>

      <h1 ref={headingRef} tabIndex={-1} className="mt-7 text-2xl font-semibold tracking-tight focus:outline-none">A few documents before day one</h1>
      <p className="mt-2 text-base text-ink-600">
        You can come back to this link any time before {longDate(docs.openUntil)}. Each file is saved as soon as it uploads,
        so nothing is lost if you stop halfway.
      </p>
      <p className="mt-2 text-sm text-ink-600">Photos (JPG or PNG) or PDF, up to 10 MB each.</p>

      <div className="mt-6 space-y-3">
        {ITEMS.map(({ item, title, kinds, note }) => {
          const ds = kinds.map(byKind);
          const state = stateOf(ds);
          const returned = ds.find((d) => d.status === 'Returned' && d.message);
          const required = ds.some((d) => d.required);
          const education = byKind('Education');
          const bankDoc = byKind('BankAccount');
          return (
            <div key={item} className={`rounded-lg border bg-white p-4 ${state === 'returned' ? 'border-critical-100' : 'border-line'}`} data-testid={`item-${item}`}>
              <div className="flex items-start gap-3">
                {icons[state]}
                <div className="min-w-0 flex-1">
                  <h2 className="text-base font-medium">{title}</h2>
                  {status(item, state, required, note)}
                </div>
              </div>

              {returned && (
                <div className="mt-3 rounded-md bg-critical-50 px-3.5 py-3">
                  <p className="text-sm font-medium text-critical-700">From the recruitment team</p>
                  {/* Their words, as written: plain text, line breaks kept. */}
                  <p className="mm mt-1.5 whitespace-pre-wrap text-base text-critical-700">{returned.message}</p>
                </div>
              )}

              {state !== 'approved' && item === 'Nrc' && (
                <ul className="mt-3 divide-y divide-line rounded-md border border-line">
                  {slotRow('Nrc', 'NrcFront', 'Front')}
                  {slotRow('Nrc', 'NrcBack', 'Back')}
                </ul>
              )}

              {state !== 'approved' && item === 'BankAccount' && (
                <form className="mt-3 space-y-3" onSubmit={saveBank}>
                  <div>
                    <label htmlFor="bank-name" className="block text-sm font-medium">Bank</label>
                    <input id="bank-name" autoComplete="off" maxLength={100} value={bank.bankName}
                      onChange={(e) => setBank({ ...bank, bankName: e.target.value })} className={field} />
                  </div>
                  <div>
                    <label htmlFor="account-name" className="block text-sm font-medium">Name on the account</label>
                    <input id="account-name" autoComplete="off" maxLength={200} value={bank.accountName}
                      onChange={(e) => setBank({ ...bank, accountName: e.target.value })} className={field} />
                  </div>
                  <div>
                    <label htmlFor="account-number" className="block text-sm font-medium">Account number</label>
                    {bankDoc.bank?.accountNumberMasked && (
                      <p id="account-number-saved" className="text-sm text-ink-600">
                        Saved: <span className="tnum font-mono">{bankDoc.bank.accountNumberMasked}</span>. Type it again only to change it.
                      </p>
                    )}
                    <input id="account-number" inputMode="numeric" autoComplete="off" maxLength={40} value={bank.accountNumber}
                      aria-describedby={bankDoc.bank?.accountNumberMasked ? 'account-number-saved' : undefined}
                      onChange={(e) => setBank({ ...bank, accountNumber: e.target.value })} className={`${field} tnum font-mono`} />
                  </div>
                  <button type="submit" disabled={busy !== null || !bank.bankName.trim() || !bank.accountName.trim() || !bank.accountNumber.trim()}
                    className="h-11 w-full rounded-md border border-line-strong bg-white text-base font-medium transition-colors hover:bg-canvas disabled:opacity-50">
                    {busy === 'bank' ? 'Saving…' : 'Save bank details'}
                  </button>
                  <div>
                    <p className="text-sm font-medium">Photo of your bank book&apos;s first page</p>
                    <p className="text-sm text-ink-600">So the number can be checked before your first salary.</p>
                    <ul className="mt-2 rounded-md border border-line">{slotRow('BankAccount', 'BankAccount')}</ul>
                  </div>
                </form>
              )}

              {state !== 'approved' && item === 'Education' && (
                <>
                  {education.files.length > 0 && (
                    <ul className="mt-3 divide-y divide-line rounded-md border border-line">
                      {education.files.map((f) => (
                        <li key={f.id} className="flex items-center gap-3 px-3 py-2.5">
                          <span className="min-w-0 flex-1 truncate text-sm">{f.fileName} <span className="tnum text-ink-600">· {size(f.sizeBytes)}</span></span>
                          <button type="button" onClick={() => remove('Education', f.id)} disabled={busy !== null}
                            aria-label={`Remove ${f.fileName}`} className="h-11 shrink-0 px-2 text-sm font-medium text-brand-700 disabled:opacity-60">
                            {busy === `remove-${f.id}` ? 'Removing…' : 'Remove'}
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                  {education.files.length < 5 && (
                    <label htmlFor="file-Education" className={`mt-3 ${slot}`}>
                      <input id="file-Education" type="file" accept="image/jpeg,image/png,application/pdf" className="sr-only"
                        disabled={busy !== null} aria-label="Add an education certificate"
                        onChange={(e) => { choose('Education', 'Education', e.target.files?.[0]); e.target.value = ''; }} />
                      {busy === 'upload-Education' ? 'Uploading…' : <>Add a file <span className="ml-1 font-normal text-ink-600">— up to 5</span></>}
                    </label>
                  )}
                </>
              )}

              {state !== 'approved' && item === 'Photo' && (
                <ul className="mt-3 rounded-md border border-line">{slotRow('Photo', 'Photo', undefined, true)}</ul>
              )}

              {errors[item] && <p role="alert" className="mt-3 text-sm text-critical-700">{errors[item]}</p>}
            </div>
          );
        })}
      </div>

      <div className="mt-6 rounded-lg border border-line bg-white p-4">
        <p className="text-sm text-ink-700">
          These documents are stored by {companyName} and used only for your employment file. You can ask for them to be
          deleted if you do not join.
        </p>
      </div>

      {errors.send && <p role="alert" className="mt-4 text-base text-critical-700">{errors.send}</p>}
      <button type="button" onClick={() => void send()} disabled={busy !== null || !docs.canSend} className={`mt-6 ${primary}`}>
        {busy === 'send' ? 'Sending…' : 'Send for checking'}
      </button>
      <p className="tnum mt-2.5 text-center text-sm text-ink-600">
        {docs.requiredItemsReady} of {docs.requiredItems} required items ready
      </p>

      <button type="button" onClick={onReadLetter} className={`mt-6 ${secondary}`}>Read the signed letter</button>
    </section>
  );
}

/** After "Send for checking", until something changes or is sent back. */
export function DocumentsSent({
  docs, onChangeDocument, headingRef,
}: {
  docs: PortalDocuments;
  onChangeDocument: () => void;
  headingRef: RefObject<HTMLHeadingElement>;
}) {
  const byKind = (kind: PreboardingDocumentKind) => docs.documents.find((d) => d.kind === kind)!;
  return (
    <section>
      <div className="rounded-lg border border-positive-500/30 bg-positive-50 px-5 py-4">
        <h1 ref={headingRef} tabIndex={-1} className="text-base font-semibold text-positive-700 focus:outline-none">Sent for checking — thank you</h1>
        <p className="mt-1 text-sm text-positive-700/90">
          The recruitment team will check them and email{' '}
          {docs.maskedEmail ? <span className="font-mono">{docs.maskedEmail}</span> : 'you'} if anything needs redoing.
        </p>
      </div>

      <h2 className="mt-7 text-2xl font-semibold tracking-tight">Your documents</h2>
      <ul className="mt-4 divide-y divide-line rounded-lg border border-line bg-white">
        {ITEMS.map(({ item, title, kinds }) => {
          const ds = kinds.map(byKind);
          const state = stateOf(ds);
          const optionalEmpty = !ds.some((d) => d.required) && ds.every((d) => d.status === 'Missing');
          return (
            <li key={item} className="flex items-center justify-between gap-3 px-4 py-3.5">
              <span className={`text-base ${optionalEmpty ? 'text-ink-600' : ''}`}>{title}</span>
              <span className={`shrink-0 text-sm ${state === 'approved' ? 'text-positive-700' : 'text-ink-600'}`}>
                {state === 'approved' ? 'Checked' : optionalEmpty ? 'Optional · not added' : 'Being checked'}
              </span>
            </li>
          );
        })}
      </ul>
      <p className="mt-4 text-base text-ink-600">
        Anything not yet checked can still be changed: open this link again before {longDate(docs.openUntil)}.
      </p>
      <button type="button" onClick={onChangeDocument} className={`mt-6 ${secondary}`}>Change a document</button>
    </section>
  );
}
