# ADR-0016 — Two-axis brute-force protection on login

- **Date:** 2026-07-27
- **Status:** Accepted (implemented)
- **Amends:** [ADR-0002](ADR-0002-jwt-auth.md), which chose self-issued JWTs but left the
  credential endpoint itself unprotected
- **Constrained by:** [ADR-0004](ADR-0004-single-tenant-deployment.md) — one instance, one
  database, per company

## Context

`POST /api/auth/login` is the only anonymous endpoint in the system that verifies a secret.
Until now nothing limited how often it could be called. That made the password policy the
sole barrier between an attacker and every account in the install — and RecruitOps holds
salary bands, offer letters and candidate personal data, so an account takeover is a
material breach, not an inconvenience.

Two attacks matter and they look completely different from the server:

1. **One source, many guesses.** A script hammering the endpoint from a single machine.
2. **Many sources, one account.** Credential stuffing distributed across a botnet or proxy
   pool, where each individual address makes only one or two attempts.

A per-IP limiter sees (1) and is blind to (2). A per-account limiter sees (2) and is blind
to (1). Choosing one leaves an open door.

## Decision

**Apply both, independently.**

| Axis | Mechanism | Default |
|---|---|---|
| Per client IP (IPv6 by /64) | ASP.NET Core built-in fixed-window rate limiter (`RateLimitPolicies.Login`) | 60 requests / 60s |
| Per account | `ILoginThrottle` / `LoginThrottle`, in-process | 5 failures → 15 min lockout |

Supporting decisions, each of which we could have got wrong:

- **Failures are counted for every email, real or not.** Counting only real accounts would
  make the 429 an existence oracle — an attacker could enumerate valid addresses by seeing
  which ones can be locked out. That would silently undo the identical-401 behaviour
  `AuthService` already implements for exactly this reason. `LoginThrottleTests` asserts it.
- **A successful login clears the counter.** Otherwise a user who mistypes twice on Monday
  and twice on Tuesday gets locked out on Wednesday for one more slip.
- **The lockout is short (15 minutes) and there is no admin unlock.** Any per-account
  lockout is a denial-of-service tool: anyone who knows a colleague's email can lock them
  out on purpose. Slowing an attacker to 5 guesses per 15 minutes already defeats brute
  force; hours-long or sticky lockouts buy almost nothing and hand out a griefing weapon.
- **Limits are configurable, and the default is generous.** An office behind one NAT address
  legitimately produces many logins from a single IP — and until a deployment enables
  forwarded headers, *the whole company* arrives from one nginx container. The window counts
  successful logins too. A limit tuned for a hostile single host would lock a customer out at
  09:00 on their first Monday, so the per-IP number is set to survive that; brute force is
  stopped by the per-account throttle, not by this number.
- **IPv6 is partitioned by /64, not by full address.** A single residential or VPS IPv6
  allocation is a /64 or larger, so keying on the address would hand one attacker 2^64 free
  buckets and the per-IP limit would exist on paper only.
- **The throttle's key is a hash of the normalised email, and `LoginRequest.Email` is length-
  capped.** `[EmailAddress]` only checks for a single '@', so without a cap an anonymous
  caller could make the server retain megabytes per failed attempt for the whole window.
- **`X-Forwarded-For` is NOT trusted by default.** Behind nginx every request appears to
  come from the proxy, which would collapse the per-IP limiter into a single global bucket.
  The fix is forwarded headers — but that header is client-supplied. On an API that is
  reachable directly, trusting it lets an attacker mint a fresh partition key per request
  and walk straight past the limiter, which is *worse* than having no per-IP limit, because
  it looks protected. It is therefore gated behind
  `ReverseProxy:TrustForwardedHeaders`, which the operator sets only after making the API
  unreachable except through the proxy.

## Consequences

**Accepted limitations, recorded rather than hidden:**

- **`LoginThrottle` is in-process.** Counters do not survive a restart, so an attacker who
  can force a recycle can reset them; and they are not shared between replicas. ADR-0004
  ships one instance per company, so this is adequate *today*. **The moment we run two
  replicas for one customer, this must move to Redis or a database table** — otherwise the
  effective limit silently becomes N × the configured value.
- **The default compose file publishes the API port** for local convenience, which is why
  `TrustForwardedHeaders` ships as `false`. A production install must remove that port
  mapping and enable the flag; until it does, the per-IP limiter treats all proxied traffic
  as one caller. This is documented in `docker-compose.yml` at the port mapping itself,
  where someone is actually looking when they change it.
- **No CAPTCHA, no progressive delay, no notification on lockout.** Deliberately out of
  scope for now; revisit if telemetry shows real attack traffic.

## Alternatives considered

- **Per-account lockout only** — simplest, but leaves a single host free to sweep across
  every account at full speed, one or two guesses each.
- **Per-IP only** — the more common default, and useless against distributed stuffing,
  which is the attack pattern that actually compromises accounts in practice.
- **Persisting failure counts on the `User` row** — survives restarts and would work across
  replicas, but writes to the users table on every failed guess, so the endpoint becomes a
  write-amplification lever an attacker controls. Revisit alongside the Redis option.
- **Account lockout requiring admin unlock** — strongest against brute force, and the
  easiest denial-of-service in the product. Rejected.

## Follow-ups

- Refresh tokens (still outstanding from ADR-0002)
- httpOnly cookie option for enterprise/bank deployments (trade-off documented in `auth.ts`)
- Structured audit logging of failed logins, so lockouts are observable rather than inferred
