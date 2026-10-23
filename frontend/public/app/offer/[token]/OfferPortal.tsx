'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import type { AcceptOfferRequest, DeclineOfferRequest, PortalOffer, PortalOfferLink } from '@recruitops/types';
import { api, ApiError } from '../../../lib/api';

/**
 * The candidate's offer link (Module 4.3, ADR-0030 §5a), built from `design/public/offer-portal.html`
 * — its states #link, #code, #offer, #letter, #sign, #decline, #accepted, #declined, #expired and
 * #invalid. Phone-first: one column, 44px targets, no navigation and no internal vocabulary.
 *
 * The server decides everything. The link alone gets the company and a masked address; a code the
 * candidate receives by email opens a session (an HttpOnly cookie this script never sees); only that
 * session reads the terms and answers. Every refusal the API writes for the candidate (409) is shown
 * as written; a 401 means the session ended, and the page goes back to asking for a code.
 */

type Step = 'loading' | 'invalid' | 'expired' | 'link' | 'code' | 'offer' | 'letter' | 'sign' | 'decline' | 'answered';

function longDate(date: string): string {
  return new Date(`${date}T00:00:00Z`).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
}

function longDay(iso: string): string {
  return new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' });
}

function mmk(amount: number): string {
  return `MMK ${amount.toLocaleString('en-US')}`;
}

function probation(months: number): string {
  return months === 0 ? 'None' : `${months} month${months === 1 ? '' : 's'}`;
}

/** The API's own words for a refusal it wrote for the candidate; otherwise ours. */
function messageOf(e: unknown, fallback: string): string {
  return e instanceof ApiError && e.status === 409 && e.detail ? e.detail : fallback;
}

const primary = 'flex w-full h-12 items-center justify-center rounded-md bg-brand-700 hover:bg-brand-800 text-white text-base font-semibold transition-colors disabled:opacity-60';
const secondary = 'flex w-full h-12 items-center justify-center rounded-md bg-white border border-line hover:border-line-strong text-ink-900 text-base font-medium transition-colors disabled:opacity-60';

export function OfferPortal({ token }: { token: string }) {
  const base = `/public/offers/${encodeURIComponent(token)}`;

  const [step, setStep] = useState<Step>('loading');
  const [link, setLink] = useState<PortalOfferLink | null>(null);
  const [offer, setOffer] = useState<PortalOffer | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [consent, setConsent] = useState(false);
  const [reason, setReason] = useState('');
  // Where "Back" from the letter goes: the offer, or the answered page for a signed letter.
  const [letterFrom, setLetterFrom] = useState<Step>('offer');

  const heading = useRef<HTMLHeadingElement>(null);

  /** Moves to a step, clears its messages and takes focus to its heading, as a new page would. */
  const go = useCallback((next: Step) => {
    setError(null);
    setNotice(null);
    setStep(next);
  }, []);

  useEffect(() => {
    heading.current?.focus();
    window.scrollTo(0, 0);
  }, [step]);

  const showOffer = useCallback((o: PortalOffer) => {
    setOffer(o);
    go(o.state === 'Open' ? 'offer' : 'answered');
  }, [go]);

  /** Anything that refused because the session ended goes back to the code; an expiry, to its page. */
  const fail = useCallback((e: unknown, fallback: string) => {
    if (e instanceof ApiError && e.status === 401) {
      go('link');
      setNotice('For your privacy, the page locked itself. Get a new code to continue.');
      return;
    }
    if (e instanceof ApiError && e.status === 404) {
      go('invalid');
      return;
    }
    const message = messageOf(e, fallback);
    if (/expired/i.test(message) && !/code/i.test(message)) {
      go('expired');
      return;
    }
    setError(message);
  }, [go]);

  useEffect(() => {
    let live = true;
    (async () => {
      try {
        const opened = await api<PortalOfferLink>(base);
        if (!live) return;
        setLink(opened);
        if (opened.state === 'Expired') {
          setStep('expired');
          return;
        }
        // A session from earlier in the last two hours still opens it, without a new code.
        try {
          const o = await api<PortalOffer>(`${base}/offer`);
          if (live) showOffer(o);
        } catch {
          if (live) setStep('link');
        }
      } catch {
        // One page for unknown, mistyped and withdrawn links: the API answers them alike, on purpose.
        if (live) setStep('invalid');
      }
    })();
    return () => { live = false; };
  }, [base, showOffer]);

  async function sendCode(again: boolean) {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await api(`${base}/code`, { method: 'POST' });
      setCode('');
      if (!again) go('code');
      if (again) setNotice('We have sent a new code. Use that one — the earlier code no longer works.');
    } catch (e) {
      fail(e, 'We could not send a code just now. Please try again in a minute.');
    } finally {
      setBusy(false);
    }
  }

  async function verify(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api(`${base}/verify`, { method: 'POST', body: JSON.stringify({ code }) });
      showOffer(await api<PortalOffer>(`${base}/offer`));
    } catch (err) {
      fail(err, 'That did not work. Please check the code and try again.');
    } finally {
      setBusy(false);
    }
  }

  async function accept(e: React.FormEvent) {
    e.preventDefault();
    if (!offer) return;
    setBusy(true);
    setError(null);
    try {
      const body: AcceptOfferRequest = { typedName: name, consent, letterSha256: offer.letterSha256 };
      showOffer(await api<PortalOffer>(`${base}/accept`, { method: 'POST', body: JSON.stringify(body) }));
    } catch (err) {
      // The letter changed since it was read: show the new one rather than sign blind.
      if (err instanceof ApiError && err.status === 409 && /changed since you opened it/.test(err.detail ?? '')) {
        try {
          setOffer(await api<PortalOffer>(`${base}/offer`));
        } catch {
          // The message below still says what to do.
        }
      }
      fail(err, 'Your answer could not be recorded. Please try again.');
    } finally {
      setBusy(false);
    }
  }

  async function decline(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const body: DeclineOfferRequest = { reason: reason.trim() || null };
      showOffer(await api<PortalOffer>(`${base}/decline`, { method: 'POST', body: JSON.stringify(body) }));
    } catch (err) {
      fail(err, 'Your answer could not be recorded. Please try again.');
    } finally {
      setBusy(false);
    }
  }

  function readLetter(from: Step) {
    setLetterFrom(from);
    go('letter');
  }

  const contact = offer?.contactEmail ?? link?.contactEmail ?? null;
  const company = offer?.companyName ?? link?.companyName ?? '';

  const back = (to: Step, label = 'Back to the offer') => (
    <button type="button" onClick={() => go(to)} className="no-print inline-flex h-11 items-center gap-1.5 text-base font-medium text-brand-700">
      <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M10 3.5L5.5 8 10 12.5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" /></svg>
      {label}
    </button>
  );

  const alert = error && <p role="alert" className="mt-4 text-base text-critical-700">{error}</p>;
  const status = notice && <p role="status" className="mt-4 text-base text-ink-700">{notice}</p>;
  const h1 = 'text-3xl font-bold tracking-tight focus:outline-none';

  return (
    <div className="flex min-h-screen flex-col">
      <header className="no-print border-b border-line bg-white">
        <div className="mx-auto flex h-14 max-w-[560px] items-center gap-2.5 px-5">
          <span className="text-base font-semibold tracking-tight">{company}</span>
          <span className="ml-auto text-xs text-ink-600">Secure link</span>
        </div>
      </header>

      <main className="flex-1">
        <div className="mx-auto max-w-[560px] px-5 py-8">
          {step === 'loading' && <p className="text-ink-600">Loading…</p>}

          {step === 'invalid' && (
            <section>
              <h1 ref={heading} tabIndex={-1} className={h1}>This link doesn&apos;t work</h1>
              <p className="mt-3 text-lg text-ink-700">
                Check that you opened the most recent email about your offer. If it still doesn&apos;t work,
                reply to that email and the recruitment team will help.
              </p>
            </section>
          )}

          {step === 'expired' && (
            <section>
              <p className="text-sm text-ink-600">Offer of employment</p>
              <h1 ref={heading} tabIndex={-1} className={`mt-1.5 ${h1}`}>This offer has expired</h1>
              {link && (
                <p className="mt-3 text-lg text-ink-700">It was open until {longDate(link.expiresOn)}, so this link no longer works.</p>
              )}
              {contact && (
                <p className="mt-6 text-base text-ink-600">
                  If you still want to talk about it, contact{' '}
                  <a href={`mailto:${contact}`} className="font-medium text-brand-700">{contact}</a>.
                </p>
              )}
            </section>
          )}

          {step === 'link' && link && (
            <section>
              <p className="text-sm text-ink-600">Offer of employment</p>
              <h1 ref={heading} tabIndex={-1} className={`mt-1.5 ${h1}`}>You have an offer from {link.companyName}</h1>
              {link.maskedEmail ? (
                <>
                  <p className="mt-3 text-lg text-ink-700">To keep it private, we&apos;ll email a 6-digit code to the address you applied with:</p>
                  <p className="mt-3 rounded-lg border border-line bg-white px-5 py-3.5 font-mono text-lg font-medium">{link.maskedEmail}</p>
                  {status}
                  {alert}
                  <button type="button" onClick={() => void sendCode(false)} disabled={busy} className={`mt-7 ${primary}`}>
                    {busy ? 'Sending…' : 'Email me a code'}
                  </button>
                  {contact && (
                    <p className="mt-4 text-sm text-ink-600">
                      No longer use that address? Contact{' '}
                      <a href={`mailto:${contact}`} className="font-medium text-brand-700">{contact}</a> — the code can only go to
                      the address on your application.
                    </p>
                  )}
                </>
              ) : (
                <p className="mt-3 text-lg text-ink-700">
                  There is no email address on your application, so we can&apos;t send you a code.
                  {contact ? <> Please contact <a href={`mailto:${contact}`} className="font-medium text-brand-700">{contact}</a>.</> : ' Please contact the recruitment team.'}
                </p>
              )}
            </section>
          )}

          {step === 'code' && link && (
            <section>
              <p className="text-sm text-ink-600">Offer of employment</p>
              <h1 ref={heading} tabIndex={-1} className={`mt-1.5 ${h1}`}>Enter the code we emailed you</h1>
              <p className="mt-3 text-lg text-ink-700">
                Sent to <span className="font-mono font-medium">{link.maskedEmail}</span>. It works for 10 minutes.
              </p>
              <form className="mt-6" onSubmit={verify}>
                <label htmlFor="otp" className="block text-base font-medium">6-digit code</label>
                {/* One field, not six boxes: paste and the phone's one-time-code autofill both just work. */}
                <input
                  id="otp" inputMode="numeric" autoComplete="one-time-code" maxLength={12} placeholder="000000"
                  value={code} onChange={(e) => setCode(e.target.value)}
                  aria-invalid={error !== null} aria-describedby={error ? 'otp-error' : undefined}
                  className={`mt-2 h-14 w-full rounded-md border bg-white px-4 text-center font-mono text-3xl tracking-[0.5em] focus:outline-none focus:ring-2 focus:ring-brand-700 ${error ? 'border-critical-500' : 'border-line'}`}
                />
                {error && <p id="otp-error" role="alert" className="mt-2 text-base text-critical-700">{error}</p>}
                {status}
                <button type="submit" disabled={busy || code.trim().length === 0} className={`mt-6 ${primary}`}>
                  {busy ? 'Checking…' : 'Continue'}
                </button>
              </form>
              <div className="mt-6 rounded-lg border border-line bg-white p-4">
                <p className="text-base font-medium">Didn&apos;t get it?</p>
                <p className="mt-1 text-sm text-ink-600">
                  It can take a minute. Check spam or promotions, then{' '}
                  <button type="button" onClick={() => void sendCode(true)} disabled={busy} className="font-medium text-brand-700">send a new code</button>.
                  A new code replaces this one.
                </p>
              </div>
            </section>
          )}

          {step === 'offer' && offer && (
            <section>
              <p className="text-sm text-ink-600">Offer of employment</p>
              <h1 ref={heading} tabIndex={-1} className={`mt-1.5 ${h1}`}>{offer.position}</h1>
              <p className="mt-2 text-lg text-ink-700">{offer.departmentName}</p>

              {/* The facts a candidate opens this to check, before the letter. */}
              <dl className="mt-6 divide-y divide-line rounded-lg border border-line bg-white">
                <div className="flex items-baseline justify-between gap-4 px-5 py-3.5">
                  <dt className="text-base text-ink-600">Monthly salary</dt>
                  <dd className="tnum font-mono text-lg font-semibold">{mmk(offer.baseSalary)}</dd>
                </div>
                <div className="flex items-baseline justify-between gap-4 px-5 py-3.5">
                  <dt className="text-base text-ink-600">Allowances</dt>
                  <dd className="tnum font-mono text-base font-medium">{mmk(offer.allowances)}</dd>
                </div>
                <div className="flex items-baseline justify-between gap-4 px-5 py-3.5">
                  <dt className="text-base text-ink-600">Joining date</dt>
                  <dd className="text-base font-medium">{longDate(offer.joiningDate)}</dd>
                </div>
                <div className="flex items-baseline justify-between gap-4 px-5 py-3.5">
                  <dt className="text-base text-ink-600">Probation</dt>
                  <dd className="text-base font-medium">{probation(offer.probationMonths)}</dd>
                </div>
              </dl>

              <button type="button" onClick={() => readLetter('offer')} className="mt-4 flex w-full items-center gap-3 rounded-lg border border-line bg-white px-5 py-4 text-left transition-colors hover:border-line-strong">
                <span className="min-w-0 flex-1">
                  <span className="block text-base font-medium">Read the full offer letter</span>
                  <span className="block text-sm text-ink-600">Opens here — print it or save it as a PDF</span>
                </span>
                <svg className="h-4 w-4 shrink-0 text-ink-600" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M6 3.5L10.5 8 6 12.5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" /></svg>
              </button>

              {/* Expiry stated plainly, because the link really does stop working. */}
              <p className="mt-4 text-sm text-warn-700">
                Please reply by <span className="font-medium">{longDate(offer.expiresOn)}</span>. After that this link stops working
                and you will need a new one from the recruiter.
              </p>

              {alert}
              <div className="mt-7 space-y-2.5">
                <button type="button" onClick={() => go('sign')} className={primary}>Accept &amp; sign</button>
                <button type="button" onClick={() => go('decline')} className={secondary}>Decline this offer</button>
              </div>
              <p className="mt-4 text-sm text-ink-600">
                You sign by typing your full name. A copy of what you signed is emailed to you straight away.
              </p>
              {contact && (
                <div className="mt-6 rounded-lg border border-line bg-white p-4">
                  <p className="text-sm text-ink-700">
                    Something wrong with these terms? Contact{' '}
                    <a href={`mailto:${contact}`} className="font-medium text-brand-700">{contact}</a> before accepting — the offer
                    cannot be edited after you sign.
                  </p>
                </div>
              )}
            </section>
          )}

          {step === 'letter' && offer && (
            <section>
              {back(letterFrom, letterFrom === 'answered' ? 'Back' : 'Back to the offer')}
              <h1 ref={heading} tabIndex={-1} className="sr-only">Offer letter</h1>
              {/* Plain text from the server, rendered as text: exactly what is (or was) signed. */}
              <article className="mt-2 whitespace-pre-wrap rounded-lg border border-line bg-white px-5 py-6 text-md leading-relaxed" data-testid="letter">
                {offer.letterText}
              </article>
              <button type="button" onClick={() => window.print()} className={`no-print mt-4 ${secondary}`}>Print or save as PDF</button>
            </section>
          )}

          {step === 'sign' && offer && (
            <section>
              {back('offer')}
              <h1 ref={heading} tabIndex={-1} className={`mt-2 ${h1}`}>Accept and sign</h1>
              <p className="mt-2 text-lg text-ink-700">
                {offer.position} · {mmk(offer.baseSalary + offer.allowances)} a month · from {longDate(offer.joiningDate)}
              </p>
              <form className="mt-6 space-y-5" onSubmit={accept}>
                <div>
                  <label htmlFor="sig-name" className="block text-base font-medium">Your full name</label>
                  <p id="sig-hint" className="mt-0.5 text-sm text-ink-600">Typed as you would sign it. This is your signature.</p>
                  <input
                    id="sig-name" autoComplete="name" value={name} onChange={(e) => setName(e.target.value)} maxLength={200}
                    aria-describedby="sig-hint"
                    className="mt-2 h-12 w-full rounded-md border border-line bg-white px-4 text-lg focus:outline-none focus:ring-2 focus:ring-brand-700"
                  />
                </div>
                <label className="flex items-start gap-3 rounded-lg border border-line bg-white p-4">
                  <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} className="mt-0.5 h-5 w-5 shrink-0 accent-brand-700" />
                  <span className="text-base">I have read the offer letter and I accept this offer of employment.</span>
                </label>
                <p className="text-sm text-ink-600">
                  We keep a record of your typed name, the date and time, the device and network you signed from, and a
                  fingerprint of the exact letter you accepted — so either side can show later what was agreed.
                </p>
                {alert}
                <button type="submit" disabled={busy || !consent || name.trim().length === 0} className={primary}>
                  {busy ? 'Signing…' : 'Accept & sign'}
                </button>
              </form>
            </section>
          )}

          {step === 'decline' && offer && (
            <section>
              {back('offer')}
              <h1 ref={heading} tabIndex={-1} className={`mt-2 ${h1}`}>Decline this offer?</h1>
              <p className="mt-2 text-lg text-ink-700">
                We&apos;ll let the recruitment team know. Once declined, the offer cannot be accepted from this link.
              </p>
              <form className="mt-6 space-y-5" onSubmit={decline}>
                <div>
                  <label htmlFor="reason" className="block text-base font-medium">
                    Anything you&apos;d like to tell us? <span className="font-normal text-ink-600">(optional)</span>
                  </label>
                  <textarea
                    id="reason" rows={4} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)}
                    placeholder="For example, you have accepted another offer"
                    className="mt-2 w-full rounded-md border border-line bg-white p-4 text-base focus:outline-none focus:ring-2 focus:ring-brand-700"
                  />
                </div>
                {alert}
                <button type="submit" disabled={busy} className="h-12 w-full rounded-md border border-critical-500 bg-white text-base font-semibold text-critical-700 transition-colors hover:bg-critical-50 disabled:opacity-60">
                  {busy ? 'Declining…' : 'Decline the offer'}
                </button>
              </form>
            </section>
          )}

          {step === 'answered' && offer?.state === 'Accepted' && (
            <section>
              <div className="rounded-lg border border-positive-500/30 bg-positive-50 px-5 py-4">
                <h1 ref={heading} tabIndex={-1} className="text-base font-semibold text-positive-700 focus:outline-none">Offer accepted — welcome</h1>
                <p className="mt-1 text-sm text-positive-700/90">
                  Signed by {offer.signedName}{offer.answeredAt ? ` on ${longDay(offer.answeredAt)}` : ''}. A copy has been emailed to you.
                </p>
              </div>
              <h2 className="mt-7 text-2xl font-semibold tracking-tight">What happens next</h2>
              <p className="mt-2 text-base text-ink-600">
                The recruitment team will be in touch about your first day, {longDate(offer.joiningDate)}. You can come back to
                this link to read the letter you signed.
              </p>
              <button type="button" onClick={() => readLetter('answered')} className={`mt-6 ${secondary}`}>Read the signed letter</button>
            </section>
          )}

          {step === 'answered' && offer?.state === 'Declined' && (
            <section>
              <div className="rounded-lg border border-line bg-white px-5 py-4">
                <h1 ref={heading} tabIndex={-1} className="text-base font-semibold focus:outline-none">You declined this offer</h1>
                <p className="mt-1 text-sm text-ink-600">
                  {offer.answeredAt ? `On ${longDay(offer.answeredAt)}. ` : ''}The recruitment team has been told.
                </p>
              </div>
              <p className="mt-6 text-base text-ink-600">
                Thank you for letting us know, and for the time you gave to the process.
                {contact && <> If you change your mind, contact <a href={`mailto:${contact}`} className="font-medium text-brand-700">{contact}</a>.</>}
              </p>
            </section>
          )}
        </div>
      </main>

      <footer className="no-print border-t border-line bg-white">
        <div className="mx-auto max-w-[560px] px-5 py-5">
          <p className="text-sm text-ink-600">This link is personal to you. Please do not forward it.</p>
        </div>
      </footer>
    </div>
  );
}
