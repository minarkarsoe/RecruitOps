import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import { ApiError } from '../lib/api';

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: vi.fn() };
});

const { api } = await import('../lib/api');
const mockApi = vi.mocked(api);

function renderLogin() {
  return render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>
  );
}

async function signIn(email = 'thura.win@yomabank.com', password = 'whatever') {
  fireEvent.change(screen.getByLabelText('Work email'), { target: { value: email } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: /sign in/i }));
}

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ------------------------------------------------------------ what it must not say

  // ADR-0016: the 401 is identical for a wrong password and an address that belongs to
  // nobody, because the difference tells a stranger whether someone works here. The screen
  // must not undo that by naming the field.
  it('never says which of the two was wrong', async () => {
    mockApi.mockRejectedValue(new ApiError(401, 'Email or password is incorrect.'));
    renderLogin();
    await signIn();

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Email or password is incorrect');
    expect(alert.textContent).not.toMatch(/no such|not found|unknown|no account|wrong password/i);
  });

  // Only the password is outlined on a rejection — outlining the email would hint that the
  // address was the problem, which is what the identical 401 refuses to reveal.
  //
  // Pinned because the first implementation appended `border-critical-500` to a class string
  // that already contained `border-line`. Both are border-color utilities of equal
  // specificity, so Tailwind's output order decided the winner and the grey border won. It
  // read correctly in the source and rendered wrong in the browser.
  it('outlines only the password field, and actually replaces the default border', async () => {
    mockApi.mockRejectedValue(new ApiError(401, 'Email or password is incorrect.'));
    renderLogin();
    await signIn();
    await screen.findByRole('alert');

    const password = screen.getByLabelText('Password');
    expect(password.className).toContain('border-critical-500');
    expect(password.className).not.toContain('border-line');

    expect(screen.getByLabelText('Work email').className).toContain('border-line');
    expect(screen.getByLabelText('Work email').className).not.toContain('border-critical-500');
  });

  // The 401 carries no body, so the page has nothing to count with. A hint would mean
  // changing the API, which is a security change rather than a copy change.
  it('offers no "attempts remaining" hint, because nothing tells it', async () => {
    mockApi.mockRejectedValue(new ApiError(401, 'Email or password is incorrect.'));
    renderLogin();
    await signIn();

    await screen.findByRole('alert');
    expect(screen.queryByText(/attempts? remaining|tries left/i)).toBeNull();
  });

  // ------------------------------------------------------------ the lockout

  it('renders the real countdown from Retry-After, not an invented one', async () => {
    // 14 min 32 s — the exact figure the design draws.
    mockApi.mockRejectedValue(new ApiError(429, 'Too many attempts', 872));
    renderLogin();
    await signIn();

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Too many attempts');
    expect(alert.textContent).toContain('14 min 32 s');
  });

  it('falls back to the full 15 minutes when the header is missing', async () => {
    // Never a shorter guess: that sends someone back to a door still locked.
    mockApi.mockRejectedValue(new ApiError(429, 'Too many attempts'));
    renderLogin();
    await signIn();

    expect((await screen.findByRole('alert')).textContent).toContain('15 min 0 s');
  });

  it('disables the form while locked, and says no administrator can lift it', async () => {
    mockApi.mockRejectedValue(new ApiError(429, 'Too many attempts', 60));
    renderLogin();
    await signIn();

    await screen.findByRole('alert');
    expect(screen.getByLabelText('Work email')).toBeDisabled();
    expect(screen.getByLabelText('Password')).toBeDisabled();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeDisabled();

    // Admin unlock was considered and rejected in ADR-0016 — any sticky lockout is a griefing
    // weapon. The screen must not offer an escape hatch the product refuses to build.
    expect(screen.getByText(/no administrator can lift it early/i)).toBeInTheDocument();
    expect(screen.queryByText(/contact your administrator/i)).toBeNull();
  });

  // The lock clears itself server-side. If the screen does not, the form stays dead after the
  // server would already accept an attempt and the only way out is a page reload.
  it('re-enables itself when the countdown reaches zero', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi.mockRejectedValue(new ApiError(429, 'Too many attempts', 2));
    renderLogin();
    await signIn();

    await waitFor(() => expect(screen.getByLabelText('Work email')).toBeDisabled());

    // One second at a time. Each tick's state update re-runs the effect, which schedules the
    // NEXT timeout — advancing 3000ms in one go moves past that point in virtual time before
    // React has scheduled it, and the countdown stalls at 1.
    for (let tick = 0; tick < 3; tick++) {
      await act(async () => {
        vi.advanceTimersByTime(1000);
      });
    }

    await waitFor(() => expect(screen.getByLabelText('Work email')).not.toBeDisabled());
    expect(screen.queryByText(/too many attempts/i)).toBeNull();
  });

  // ------------------------------------------------------------ ADR-0004 on the screen

  it('asks for no workspace, and states that there is no self-signup', () => {
    renderLogin();

    // The URL already is the company (ADR-0004). Asking would invent an identity the
    // deployment does not have.
    expect(screen.queryByLabelText(/workspace|subdomain|company|tenant|organisation/i)).toBeNull();

    // The absence of sign-up is stated in words rather than left as a missing element.
    expect(screen.getByText(/no self-signup/i)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /sign up|create account|register/i })).toBeNull();
  });
});
