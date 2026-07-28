import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Button, Card } from '@recruitops/ui';
import type { LoginRequest, LoginResponse } from '@recruitops/types';
import { api, ApiError } from '../lib/api';
import { auth } from '../lib/auth';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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
      // The API returns the same 401 for unknown email and wrong password, so the
      // message stays deliberately vague here too — no user enumeration.
      setError(err instanceof ApiError ? err.message : 'Sign in failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto flex min-h-screen max-w-[420px] items-center px-6">
      <div className="w-full">
        <h1 className="mb-6 font-display text-2xl font-bold">Sign in to RecruitOps</h1>
        <Card>
          <form onSubmit={submit} className="space-y-4">
            <div>
              <label htmlFor="email" className="mb-1 block text-[13px] font-semibold">Email</label>
              <input
                id="email" type="email" required autoComplete="username"
                value={email} onChange={(e) => setEmail(e.target.value)}
                className="h-10 w-full rounded-sm border border-line-200 px-3 focus:outline-none focus:ring-2 focus:ring-primary-600"
              />
            </div>
            <div>
              <label htmlFor="password" className="mb-1 block text-[13px] font-semibold">Password</label>
              <input
                id="password" type="password" required autoComplete="current-password"
                value={password} onChange={(e) => setPassword(e.target.value)}
                className="h-10 w-full rounded-sm border border-line-200 px-3 focus:outline-none focus:ring-2 focus:ring-primary-600"
              />
            </div>

            {error && <p role="alert" className="text-[13px] text-danger-600">{error}</p>}

            <Button type="submit" disabled={busy} className="w-full">
              {busy ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>
        </Card>
      </div>
    </div>
  );
}
