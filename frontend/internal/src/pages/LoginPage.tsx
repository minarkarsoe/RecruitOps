import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import type { LoginRequest, LoginResponse } from '@recruitops/types';
import { api, ApiError } from '../lib/api';
import { auth } from '../lib/auth';

// Built against `design/internal/login.html` (ADR-0025). Two things on this screen are
// decisions rather than styling, and both come from ADR-0004 — one instance and one database
// per company:
//
//   1. THERE IS NO WORKSPACE FIELD. The URL already is the company. Asking for a workspace
//      would invent an identity the deployment does not have, and would imply to a buyer that
//      their data sits beside someone else's.
//   2. THERE IS NO SIGN-UP LINK, and the absence is stated in words rather than left as a
//      missing element people hunt for. Accounts are provisioned by an admin on the Users
//      screen; self-signup into a system holding salary bands would be a defect.
//
// The failure states come from ADR-0016, which is accepted and implemented:
//   · wrong credentials → 401 with NO body. The page cannot say how many attempts remain,
//     because nothing tells it, and it must not say WHICH field was wrong — that difference
//     tells a stranger whether an address belongs to a real employee.
//   · locked → 429 + `Retry-After` in seconds. The countdown below is that number, not a guess.

/** The RecruitOps mark. Inline rather than an asset so it cannot 404 on the one screen
 *  a signed-out person can reach. */
function Logo() {
  return (
    <svg width="26" height="26" viewBox="0 0 26 26" fill="none" aria-hidden="true">
      <rect x="1" y="1" width="24" height="24" rx="7" fill="#0F766E" />
      <circle cx="9" cy="7.5" r="2.1" fill="#fff" />
      <circle cx="9" cy="13" r="2.1" fill="#fff" />
      <circle cx="9" cy="18.5" r="2.1" fill="#F59E0B" />
      <path
        d="M13.4 7.5h4.2M13.4 13h4.2M13.4 18.5h2.6"
        stroke="#99F6E4"
        strokeWidth="1.4"
        strokeLinecap="round"
      />
    </svg>
  );
}

function Spinner() {
  return (
    <svg className="h-4 w-4 animate-spin" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <circle cx="8" cy="8" r="6.2" stroke="currentColor" strokeOpacity=".3" strokeWidth="1.8" />
      <path d="M14.2 8A6.2 6.2 0 0 0 8 1.8" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

/** "14 min 32 s" — the shape the design draws. Seconds stay visible throughout so a stalled
 *  countdown is obvious rather than looking like a rounded-down minute. */
function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return minutes > 0 ? `${minutes} min ${seconds} s` : `${seconds} s`;
}

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [lockedFor, setLockedFor] = useState<number | null>(null);

  const locked = lockedFor !== null && lockedFor > 0;

  // The lock clears itself, so the screen has to as well — otherwise the form stays disabled
  // after the server would already accept an attempt, and the user's only recourse is a reload.
  // Counting down to 0 is all that is needed — `locked` above is already false at 0, so there
  // is no separate "clear the lock" step. There used to be one; removing it changed no test
  // and no behaviour, which is what redundant state looks like.
  useEffect(() => {
    if (lockedFor === null || lockedFor <= 0) return;
    const timer = window.setTimeout(() => setLockedFor((s) => (s === null ? null : s - 1)), 1000);
    return () => window.clearTimeout(timer);
  }, [lockedFor]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const body: LoginRequest = { email, password };
      const res = await api<LoginResponse>('/auth/login', {
        method: 'POST',
        body: JSON.stringify(body),
      });
      auth.set(res);
      const from = (location.state as { from?: string } | null)?.from ?? '/requisitions';
      navigate(from, { replace: true });
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) {
        // Fall back to the full 15 minutes only if the header is missing — never invent a
        // shorter one, which would send someone back to a door that is still locked.
        setLockedFor(err.retryAfterSeconds ?? 15 * 60);
        setPassword('');
      } else {
        setError(err instanceof ApiError ? err.message : 'Sign in failed.');
      }
    } finally {
      setBusy(false);
    }
  }

  const fieldBase =
    'mt-1.5 h-10 w-full rounded-md bg-white px-3 text-base ' +
    'placeholder:text-ink-400 outline-none transition-colors focus:border-brand-700 ' +
    'disabled:bg-canvas disabled:text-ink-500';

  // ⚠️ The error border REPLACES the default one; it must not be appended to it.
  // `border-line` and `border-critical-500` are both border-color utilities with equal
  // specificity, so which one wins is decided by Tailwind's output order — not by the order
  // they appear in this string. Appending the error colour looked right in the source and
  // rendered the ordinary grey border. Caught by reading the computed style in a browser.
  const fieldClass = `${fieldBase} border border-line`;
  const passwordClass = error ? `${fieldBase} border-2 border-critical-500` : fieldClass;

  return (
    <div className="grid min-h-screen place-items-center bg-canvas p-8">
      <div className="w-full max-w-[380px]">
        <div className="rounded-xl border border-line bg-white p-7 shadow-card">
          <div className={`flex items-center gap-2.5 ${busy ? 'opacity-60' : ''}`}>
            <Logo />
            <span className="text-lg font-semibold tracking-tight">RecruitOps</span>
          </div>

          {locked ? (
            <div role="alert" className="mt-5 rounded-md border border-warn-100 bg-warn-50 px-3.5 py-3">
              <p className="text-base font-medium text-warn-700">Too many attempts</p>
              <p className="mt-1 text-sm text-warn-700">
                Too many failed sign-in attempts. Try again in{' '}
                <span className="tnum font-medium">{formatCountdown(lockedFor!)}</span>.
              </p>
            </div>
          ) : error ? (
            <div role="alert" className="mt-5 rounded-md border border-critical-100 bg-critical-50 px-3.5 py-3">
              <p className="text-base font-medium text-critical-700">{error}</p>
            </div>
          ) : null}

          <form onSubmit={submit} className="mt-5 space-y-4">
            <div>
              <label htmlFor="email" className="block text-sm font-medium text-ink-700">
                Work email
              </label>
              <input
                id="email"
                type="email"
                required
                autoComplete="username"
                placeholder="name@company.com"
                disabled={busy || locked}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className={fieldClass}
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-ink-700">
                Password
              </label>
              <input
                id="password"
                type="password"
                required
                autoComplete="current-password"
                disabled={busy || locked}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                // Only the password is outlined on a rejection, because only it is worth
                // retyping — and outlining the email would hint that the address was the
                // problem, which is exactly what the identical 401 refuses to reveal.
                className={passwordClass}
              />
            </div>

            <button
              type="submit"
              disabled={busy || locked}
              className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-md bg-brand-700
                text-base font-medium text-white transition-colors hover:bg-brand-800
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-700 focus-visible:ring-offset-2
                disabled:cursor-not-allowed disabled:bg-ink-400 disabled:hover:bg-ink-400"
            >
              {/* The one place in this app where a spinner is correct: nothing is arriving
                  whose shape could be skeletoned, and the wait is a round trip the user
                  just started. */}
              {busy && <Spinner />}
              {busy ? 'Signing in…' : 'Sign in'}
            </button>
          </form>

          {locked && (
            <p className="mt-4 text-sm text-ink-600">
              The lock clears itself. No administrator can lift it early.
            </p>
          )}
        </div>

        <p className="mt-4 text-center text-sm text-ink-500">
          Accounts are created by your administrator. There is no self-signup.
        </p>
      </div>
    </div>
  );
}
