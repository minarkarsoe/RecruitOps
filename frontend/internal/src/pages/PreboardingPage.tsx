import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Card, Dialog } from '@recruitops/ui';
import type {
  Preboarding, PreboardingDocument, PreboardingFile, PreboardingHandoffTeam, RevealedBankNumber,
} from '@recruitops/types';
import { api, apiBlob } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';
import { calendarDate, day } from './offerFormat';

/**
 * Module 4.3 — pre-boarding review, the recruiter's side (ADR-0030 §5b), built from
 * `design/internal/preboarding-review.html` (updated 2026-10-23).
 *
 * The most sensitive data in the product, and the layout follows from that: no thumbnails — each
 * document opens one at a time in a viewer, and the API records the opening before it sends the file;
 * the account number is masked until revealed, and revealing is recorded. Hiring Managers never reach
 * this page — the API answers them 403 — so there is no restricted variant to build.
 *
 * Every decision names the revision of the document the recruiter was shown: if the candidate changed
 * it meanwhile the API refuses, and the page reloads rather than approve a file nobody opened.
 */

const SENSITIVE = new Set(['NrcFront', 'NrcBack', 'BankAccount']);

function size(bytes: number): string {
  return bytes < 100 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function type(contentType: string): string {
  return contentType === 'application/pdf' ? 'PDF' : contentType === 'image/png' ? 'PNG' : 'JPG';
}

function dayTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
}

function weeksToStart(joiningDate: string, now = Date.now()): string {
  const days = Math.ceil((Date.parse(`${joiningDate}T00:00:00Z`) - now) / 86_400_000);
  if (days < 0) return 'started';
  if (days < 14) return `${days} day${days === 1 ? '' : 's'} to start`;
  return `${Math.round(days / 7)} weeks to start`;
}

const pill = 'inline-flex h-5 items-center rounded-full px-2 text-2xs font-medium';

function StatusIcon({ status }: { status: PreboardingDocument['status'] }) {
  if (status === 'Approved') {
    return (
      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-positive-100 text-positive-700">
        <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="m3.5 8.4 3 3L12.5 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" /></svg>
      </span>
    );
  }
  if (status === 'Returned') {
    return (
      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-critical-100 text-critical-700">
        <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M5 5l6 6M11 5l-6 6" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" /></svg>
      </span>
    );
  }
  if (status === 'Submitted') {
    return (
      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-warn-100 text-warn-700">
        <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" aria-hidden="true"><circle cx="8" cy="8" r="6" stroke="currentColor" strokeWidth="1.5" /><path d="M8 4.8V8l2.2 1.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>
      </span>
    );
  }
  return (
    <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full border border-line bg-canvas text-ink-500">
      <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M5 8h6" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" /></svg>
    </span>
  );
}

const HANDOFF_PILL: Record<PreboardingHandoffTeam['status'], { label: string; tone: string }> = {
  Held: { label: 'Held', tone: 'border border-line bg-canvas text-ink-600' },
  NotSetUp: { label: 'Not set up', tone: 'bg-warn-50 text-warn-700' },
  Queued: { label: 'Queued', tone: 'bg-info-50 text-info-700' },
  Sent: { label: 'Sent', tone: 'bg-positive-50 text-positive-700' },
  Failed: { label: 'Not delivered', tone: 'bg-critical-50 text-critical-700' },
  NotSent: { label: 'Not sent', tone: 'bg-warn-50 text-warn-700' },
};

export function PreboardingPage() {
  const { id } = useParams<{ id: string }>();
  const [data, setData] = useState<Preboarding | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [returning, setReturning] = useState<string | null>(null);
  const [message, setMessage] = useState('');
  const [revealed, setRevealed] = useState<string | null>(null);
  const [viewing, setViewing] = useState<{ file: PreboardingFile; label: string; url: string | null; error: string | null } | null>(null);

  const base = `/offers/${id}/preboarding`;
  const session = auth.get();
  const canReview = hasPermission(session, 'permission:offers:preboarding:review');
  const canReveal = hasPermission(session, 'permission:offers:preboarding:reveal');
  // Which opening is current: a response for a viewer already closed, or replaced, is thrown away and its
  // object URL revoked at once — never left holding an NRC image in memory (security review, L8).
  const opening = useRef(0);

  useEffect(() => {
    if (!id) return;
    api<Preboarding>(base).then(setData).catch((e) => setError(e instanceof Error ? e.message : 'Could not load pre-boarding.'));
  }, [id, base]);

  // A viewer's object URL lives only while the viewer is open — and not past the page.
  useEffect(() => () => { if (viewing?.url) URL.revokeObjectURL(viewing.url); }, [viewing?.url]);
  useEffect(() => () => { opening.current += 1; }, []);

  function closeViewer() {
    opening.current += 1;
    setViewing(null);
  }

  async function act(key: string, run: () => Promise<Preboarding>) {
    setBusy(key);
    setError(null);
    try {
      setData(await run());
      setReturning(null);
      setMessage('');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'That did not work.');
      // The candidate may have changed something: show what is there now.
      api<Preboarding>(base).then(setData).catch(() => undefined);
    } finally {
      setBusy(null);
    }
  }

  const approve = (d: PreboardingDocument) =>
    act(`approve-${d.kind}`, () => api<Preboarding>(`${base}/${d.kind}/approve`, { method: 'POST', body: JSON.stringify({ revision: d.revision }) }));

  const sendBack = (d: PreboardingDocument) =>
    act(`return-${d.kind}`, () => api<Preboarding>(`${base}/${d.kind}/return`, {
      method: 'POST', body: JSON.stringify({ revision: d.revision, message }),
    }));

  const remind = () => act('remind', () => api<Preboarding>(`${base}/remind`, { method: 'POST' }));

  async function reveal() {
    setBusy('reveal');
    setError(null);
    try {
      setRevealed((await api<RevealedBankNumber>(`${base}/bank/reveal`, { method: 'POST' })).accountNumber);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'The number could not be revealed.');
    } finally {
      setBusy(null);
    }
  }

  async function open(file: PreboardingFile, label: string) {
    const mine = ++opening.current;
    setViewing({ file, label, url: null, error: null });
    try {
      const blob = await apiBlob(`${base}/files/${file.id}`);
      if (mine !== opening.current) return; // closed or replaced meanwhile: never shown, nothing to revoke
      setViewing({ file, label, url: URL.createObjectURL(blob), error: null });
    } catch (e) {
      if (mine !== opening.current) return;
      setViewing({ file, label, url: null, error: e instanceof Error ? e.message : 'The document could not be opened.' });
    }
  }

  if (!data) {
    return error ? <p role="alert" className="text-md text-critical-700">{error}</p> : <p className="text-ink-600">Loading…</p>;
  }

  const outstanding = data.requiredDocuments - data.requiredApproved;
  // The handoff reads the photo when it is sent: one approved afterwards never reaches Admin.
  const photo = data.documents.find((d) => d.kind === 'Photo');
  const photoWaiting = !data.handoffAt && photo !== undefined && photo.files.length > 0 && photo.status !== 'Approved';
  const initials = data.candidateName.split(/\s+/).filter(Boolean).slice(-2).map((w) => w[0]).join('').toUpperCase();

  const fileRow = (d: PreboardingDocument, f: PreboardingFile, prefix?: string) => (
    <div key={f.id} className="mt-2 flex max-w-[540px] flex-wrap items-center gap-3">
      <p className="min-w-0 flex-1 text-sm text-ink-600">
        {prefix ?? f.fileName} · {type(f.contentType)}, {size(f.sizeBytes)} · uploaded {day(f.uploadedAt)}
      </p>
      <Button variant="secondary" onClick={() => void open(f, d.label)} aria-label={`Open document: ${d.label}${d.files.length > 1 ? `, ${f.fileName}` : ''}`}>
        Open document
      </Button>
    </div>
  );

  return (
    <>
      <header className="mb-6">
        <div className="mb-2 flex items-center gap-1.5 text-sm">
          <Link to="/offers" className="text-brand-700 hover:underline">Offers</Link>
          <span className="text-ink-400">/</span>
          <Link to={`/offers/${data.offerId}`} className="tnum font-mono text-brand-700 hover:underline">{data.reference}</Link>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-xl font-semibold tracking-tight">Pre-boarding</h1>
          <span className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-xs font-medium ${outstanding === 0 ? 'bg-positive-50 text-positive-700' : 'bg-warn-50 text-warn-700'}`}>
            <span className="h-1.5 w-1.5 rounded-full bg-current" />
            {data.requiredApproved} of {data.requiredDocuments} approved
          </span>
        </div>
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      <div className="max-w-[1000px] space-y-4">
        <Card>
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex items-center gap-3">
              <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-positive-100 text-base font-semibold text-positive-700">{initials}</span>
              <div>
                <p className="text-xl font-semibold tracking-tight">{data.candidateName}</p>
                <p className="mt-0.5 text-base text-ink-600">
                  {data.position} · {data.departmentName} · joining <span className="tnum">{calendarDate(data.joiningDate)}</span>
                </p>
              </div>
            </div>
            <div className="shrink-0 text-right">
              <span className="inline-flex h-6 items-center gap-1.5 rounded-full bg-positive-50 px-2.5 text-xs font-medium text-positive-700">
                <span className="h-1.5 w-1.5 rounded-full bg-current" />Offer accepted
              </span>
              <p className="tnum mt-1 text-sm text-ink-600">
                {data.signedAt ? `Signed ${day(data.signedAt)} · ` : ''}{weeksToStart(data.joiningDate)}
              </p>
            </div>
          </div>
        </Card>

        <section className="overflow-hidden rounded-lg border border-line bg-white" aria-labelledby="documents-heading">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-5 py-4">
            <div>
              <h2 id="documents-heading" className="text-base font-semibold">Joining documents</h2>
              <p className="mt-1 text-sm text-ink-600">
                Which of these are required is set in{' '}
                <Link to="/approvalchains#preboarding" className="font-medium text-brand-700 hover:text-brand-800">Settings</Link>,
                per company — not per candidate, and fixed when the offer was accepted.
              </p>
            </div>
            <p className="tnum shrink-0 text-sm text-ink-600">
              {data.submittedAt ? `Sent for checking ${day(data.submittedAt)}` : 'Not sent for checking yet'}
              {data.remindedAt && ` · reminded ${day(data.remindedAt)}`}
            </p>
          </div>

          <ul className="divide-y divide-line">
            {data.documents.map((d) => (
              <li key={d.kind} className={`px-5 py-4 ${d.status === 'Submitted' && d.complete ? 'bg-warn-50/40' : ''}`} data-testid={`doc-${d.kind}`}>
                <div className="flex flex-wrap items-start gap-3.5">
                  <StatusIcon status={d.status} />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className={`text-base font-medium ${d.status === 'Missing' ? 'text-ink-600' : ''}`}>{d.label}</p>
                      {SENSITIVE.has(d.kind) && <span className={`${pill} bg-critical-50 text-critical-700`}>Sensitive</span>}
                      {d.status === 'Submitted' && d.complete && <span className={`${pill} bg-warn-100 text-warn-700`}>Awaiting your review</span>}
                      {d.status === 'Returned' && <span className={`${pill} bg-critical-50 text-critical-700`}>Re-submission needed</span>}
                      {d.status === 'Missing' && <span className={`${pill} border border-line bg-canvas text-ink-600`}>Not uploaded</span>}
                      {d.status === 'Submitted' && !d.complete && <span className={`${pill} border border-line bg-canvas text-ink-600`}>Incomplete</span>}
                      {!d.required && <span className={`${pill} border border-line bg-canvas text-ink-600`}>Optional</span>}
                    </div>

                    {d.status === 'Approved' && (
                      <p className="mt-0.5 text-sm text-ink-600">
                        Approved{d.reviewedByName ? ` by ${d.reviewedByName}` : ''}{d.reviewedAt ? `, ${dayTime(d.reviewedAt)}` : ''}
                      </p>
                    )}
                    {d.status === 'Missing' && (
                      <p className="mt-0.5 text-sm text-ink-600">
                        {d.kind === 'Photo' ? 'For the staff card. ' : ''}{d.required ? 'Required by this company’s settings.' : 'Optional.'}
                      </p>
                    )}

                    {d.bank && (d.bank.bankName || d.bank.accountNumberMasked) && (
                      <>
                        <dl className="mt-2.5 max-w-[420px] divide-y divide-line rounded-md border border-line bg-white">
                          <div className="flex items-baseline justify-between gap-3 px-3.5 py-2.5">
                            <dt className="text-sm text-ink-600">Bank</dt>
                            <dd className="text-base">{d.bank.bankName}</dd>
                          </div>
                          <div className="flex items-baseline justify-between gap-3 px-3.5 py-2.5">
                            <dt className="text-sm text-ink-600">Account name</dt>
                            <dd className="text-base">{d.bank.accountName}</dd>
                          </div>
                          <div className="flex items-center justify-between gap-3 px-3.5 py-2.5">
                            <dt className="text-sm text-ink-600">Account number</dt>
                            <dd className="flex items-center gap-2.5">
                              <span className="tnum font-mono text-base" data-testid="account-number">{revealed ?? d.bank.accountNumberMasked}</span>
                              {!canReveal ? null : revealed === null ? (
                                <button type="button" onClick={() => void reveal()} disabled={busy !== null}
                                  className="text-sm font-medium text-brand-700 hover:text-brand-800 disabled:opacity-60">
                                  {busy === 'reveal' ? 'Revealing…' : 'Reveal'}
                                </button>
                              ) : (
                                <button type="button" onClick={() => setRevealed(null)} className="text-sm font-medium text-brand-700 hover:text-brand-800">Hide</button>
                              )}
                            </dd>
                          </div>
                        </dl>
                        {canReveal && <p className="mt-2 text-sm text-ink-600">Revealing is recorded in the audit log with your name and the time.</p>}
                      </>
                    )}

                    {d.files.map((f) => fileRow(d, f, d.kind === 'BankAccount' ? 'Bank book, first page' : undefined))}

                    {d.status === 'Returned' && d.returnMessage && returning !== d.kind && (
                      <div className="mt-2 max-w-[540px] rounded-md border border-critical-100 bg-critical-50 px-3.5 py-3">
                        <p className="text-sm font-medium text-critical-700">
                          Sent back{d.reviewedAt ? ` ${day(d.reviewedAt)}` : ''} — what the candidate was told
                        </p>
                        <p className="mm mt-1.5 whitespace-pre-wrap text-base text-critical-700">{d.returnMessage}</p>
                      </div>
                    )}

                    {returning === d.kind && (
                      <div className="mt-3 max-w-[540px]">
                        <label htmlFor={`return-${d.kind}`} className="block text-sm font-medium">What the candidate will be told, in your words</label>
                        <textarea
                          id={`return-${d.kind}`} rows={4} maxLength={1000} value={message} onChange={(e) => setMessage(e.target.value)}
                          className="mm mt-1.5 w-full rounded-md border border-line bg-white px-3 py-2 text-base focus:border-brand-700 focus:outline-none"
                        />
                        <p className="mt-1.5 text-sm text-ink-600">Emailed to them with their link, and shown beside the document there. Up to 1,000 characters.</p>
                      </div>
                    )}
                  </div>

                  <div className="flex shrink-0 flex-col items-end gap-1.5">
                    {returning === d.kind ? (
                      <>
                        <Button onClick={() => void sendBack(d)} disabled={busy !== null || message.trim().length === 0}>
                          {busy === `return-${d.kind}` ? 'Sending…' : 'Send request'}
                        </Button>
                        <Button variant="ghost" onClick={() => { setReturning(null); setMessage(''); }}>Cancel</Button>
                      </>
                    ) : canReview && (
                      <div className="flex items-center gap-2">
                        {d.complete && (d.status === 'Submitted' || d.status === 'Returned') && (
                          <Button onClick={() => void approve(d)} disabled={busy !== null} aria-label={`Approve ${d.label}`}>
                            {busy === `approve-${d.kind}` ? 'Approving…' : 'Approve'}
                          </Button>
                        )}
                        {d.status === 'Submitted' && data.windowOpen && (
                          <Button variant="secondary" onClick={() => { setReturning(d.kind); setMessage(''); }} disabled={busy !== null} aria-label={`Reject ${d.label}`}>
                            Reject
                          </Button>
                        )}
                        {d.required && !d.complete && d.status !== 'Approved' && data.canRemind && (
                          <Button variant="secondary" onClick={() => void remind()} disabled={busy !== null}>
                            {busy === 'remind' ? 'Reminding…' : 'Remind'}
                          </Button>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              </li>
            ))}
          </ul>

          <div className="border-t border-line bg-canvas px-5 py-4">
            <p className="max-w-[80ch] text-sm text-ink-700">
              <span className="font-medium text-ink-900">Rejections are written to the candidate, not to the file.</span>{' '}
              “Illegible” is a note to yourself; “retake it so the text is readable” is an instruction someone can act on.
              Write it in the language they read — Burmese, English or both. Nothing translates it, so what you check is
              exactly what they get.
            </p>
          </div>
        </section>

        <h2 className="pt-4 text-2xl font-semibold tracking-tight">Department handoff</h2>
        <p className="max-w-[74ch] text-base text-ink-600">
          Sent automatically, once, when the last required document is approved — and shown here so it is not invisible:
          what will be sent, to whom, and when.
        </p>
        <section className="overflow-hidden rounded-lg border border-line bg-white" aria-label="Department handoff">
          <ul className="divide-y divide-line">
            {data.handoff.map((t) => {
              const tone = HANDOFF_PILL[t.status];
              return (
                <li key={t.team} className="flex flex-wrap items-start gap-3.5 px-5 py-4" data-testid={`handoff-${t.team}`}>
                  <div className="min-w-0 flex-1">
                    <p className="text-base font-medium">{t.team}</p>
                    {t.team === 'IT' ? (
                      <>
                        <p className="mt-0.5 text-base text-ink-700">
                          Create the email account and prepare a laptop for {data.candidateName}, {data.position}, starting {calendarDate(data.joiningDate)}.
                        </p>
                        <p className="mt-1 text-sm text-ink-600">No salary, no NRC, no bank details. IT needs a name, a role and a date.</p>
                      </>
                    ) : (
                      <>
                        <p className="mt-0.5 text-base text-ink-700">
                          Prepare a desk and an employee card for {data.candidateName}, {data.departmentName}, starting {calendarDate(data.joiningDate)}.
                        </p>
                        <p className="mt-1 text-sm text-ink-600">The approved staff-card photo travels with this one, because the card needs it. Nothing else does.</p>
                        {photoWaiting && (
                          <p className="mt-1 text-sm text-warn-700">
                            The photo has not been approved yet. Approve it before the last required document, or this email goes without it.
                          </p>
                        )}
                      </>
                    )}
                    {t.address && <p className="mt-1 text-sm text-ink-600">To <span className="font-medium text-ink-900">{t.address}</span>{t.sentAt ? ` · sent ${dayTime(t.sentAt)}` : ''}</p>}
                    {t.status === 'NotSetUp' && (
                      <p className="mt-1 text-sm text-warn-700">
                        No {t.team} address is set, so this one will not be sent.{' '}
                        <Link to="/approvalchains#preboarding" className="font-medium text-brand-700 hover:text-brand-800">Add it in Settings</Link>.
                      </p>
                    )}
                    {t.detail && <p className="mt-1 text-sm text-warn-700">{t.detail}</p>}
                  </div>
                  <span className={`inline-flex h-6 shrink-0 items-center gap-1.5 rounded-full px-2.5 text-xs font-medium ${tone.tone}`}>
                    <span className="h-1.5 w-1.5 rounded-full bg-current" />{tone.label}
                  </span>
                </li>
              );
            })}
          </ul>
          {data.handoffAt ? (
            <div className="border-t border-line bg-canvas px-5 py-4">
              <p className="text-sm text-ink-700">Released {dayTime(data.handoffAt)}, when the last required document was approved.</p>
            </div>
          ) : (
            <div className="border-t border-line bg-warn-50 px-5 py-4">
              <p className="text-sm font-medium text-warn-700">Both are held until every required document is approved</p>
              <p className="mt-1 max-w-[80ch] text-sm text-warn-700">
                {outstanding === 1 ? 'One is' : `${outstanding} are`} still outstanding, so nothing has been sent. Firing early would have IT
                setting up an account for someone whose certificate turns out to be for a different person — rare, and exactly
                the case a joining check exists to catch.
              </p>
            </div>
          )}
        </section>

        <h2 className="pt-4 text-2xl font-semibold tracking-tight">HRMS handoff, on day one</h2>
        <Card>
          <div className="flex flex-wrap items-start justify-between gap-4">
            <p className="max-w-[62ch] text-base text-ink-600">
              Syncing the new employee into the company&apos;s HRMS is an add-on, not part of this installation.
            </p>
            <span className="inline-flex h-6 shrink-0 items-center gap-1.5 rounded-full border border-line bg-canvas px-2.5 text-xs font-medium text-ink-600">
              <span className="h-1.5 w-1.5 rounded-full bg-ink-400" />Not connected
            </span>
          </div>
        </Card>

        <Card>
          <p className="text-base font-semibold">These files are the reason a retention policy is needed</p>
          <p className="mt-1.5 max-w-[80ch] text-sm text-ink-600">
            An NRC scan and a bank account number have no reason to stay in a recruitment database once the person has
            joined. Retention is not built yet: until it is, these documents are kept until someone deletes them by hand.
          </p>
        </Card>
      </div>

      <Dialog isOpen={viewing !== null} onClose={closeViewer} size="xl" title={viewing?.label} description="Opening this was recorded in the audit log.">
        {viewing && (
          <div className="min-h-[200px]">
            {viewing.error && <p role="alert" className="text-md text-critical-700">{viewing.error}</p>}
            {!viewing.error && !viewing.url && <p className="text-ink-600">Opening…</p>}
            {viewing.url && (viewing.file.contentType === 'application/pdf' ? (
              <iframe src={viewing.url} title={viewing.label} className="h-[70vh] w-full rounded-md border border-line" />
            ) : (
              <img src={viewing.url} alt={viewing.label} className="mx-auto max-h-[70vh] w-auto rounded-md border border-line" />
            ))}
          </div>
        )}
      </Dialog>
    </>
  );
}
