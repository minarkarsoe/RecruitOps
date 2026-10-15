# Reviewer m1_1 — security and ADR conformance

**Filed by:** Orchestrator from the Reviewer's reply. **VERDICT: APPROVE** · 🔴 0 · 🟡 2 · 🟢 1

## The load-bearing claim is true in code, not just in the comment

- `LoginThrottle.cs:31,34` — `MaxFailures = 5`, 15-minute window. Real values.
- `LoginThrottle.cs:71-75` — keyed on `SHA256(email.Trim().ToLowerInvariant())`, **not** the IP, so
  it is blind to the attacker's source. That is the point.
- `AuthController.cs:32-43` — `RetryAfter` consulted **before** `LoginAsync` at `:46`. A locked
  account 429s without a password verification or a DB read.
- **Not an enumeration oracle:** `AuthController.cs:48-51` records a failure on any null result, and
  `AuthService.cs:54-59` returns null for an unknown email after a dummy PBKDF2 verify. Unknown
  addresses are counted identically. `LoginThrottleTests.cs:40-51` asserts it.
- `DependencyInjection.cs:50` registers it **singleton** — load-bearing; scoped would reset the
  counter every request.

**ADR-0016:33** specifies 60 requests / 60s per client IP and `:34` five failures → 15 minutes.
Lines 48-53 state the two-mechanism split verbatim. **The restored 60/120 is the ADR value; 10/10
was the deviation.**

## The finding that reframes the milestone

Credential stuffing across a proxy pool never touches the per-IP limiter at either value — 2
attempts per node is under both. `LoginThrottle` is what stops it: 5 guesses per account per 15
minutes regardless of source.

Password spray from one IP is not closed at either value; 10 buys a 6× delay (~50 min vs ~8.3 min)
on an attack it does not stop.

**What 10 actually costs:** `[EnableRateLimiting]` at `AuthController.cs:29` is unconditional, so
successful logins consume the window. With `ReverseProxy:TrustForwardedHeaders: false`
(`appsettings.json:42`), `ClientPartitionKey` sees only the proxy's address and every caller lands
in one bucket. At 10/60s the 11th employee at 09:00 gets a 429 — and an availability attack becomes
**6× cheaper**: 10 requests a minute from anywhere denies login to the whole company.

> Under the shipped proxy configuration, **60 is the safer of the two numbers, not the riskier one.**
> The hardening run traded a real outage for a delay on an attack neither value closes.

## 🟡 Nothing pins the shipped default — this drift can recur silently

`CustomWebAppFactory.cs:76-77` raises both limits to 10000 for the whole suite, and every
rate-limit test injects its own value. **No test reads the value that ships.** That is why 484 tests
stayed green while the config sat at 10, and they will stay green if it drifts again.

The existing assertions are refusal-shaped — `Challenger1AdversarialTests.cs:80-81,102-104` assert a
429 arrives, which a limiter set to 1 also satisfies. The one positive loop (`:73-77`) runs against
an injected 10, never against 60.

The repo already solved this for a sibling concern: `NoAiFallbackWebAppFactory` +
`AiApiKeyGatingDefaultsTests.cs` exist precisely because a suite that only exercises the stub path
stays green whatever the unconfigured default does. **The rate limits have no equivalent.**

## 🟡 Sibling gap on the anonymous auth endpoints (pre-existing)

`AuthController.cs:29` rate-limits `Login`; siblings `Refresh` (`:59`) and `Revoke` (`:76`) carry
nothing on an `[AllowAnonymous]` class. Not a guessing hole — `JwtTokenService.cs:69` mints 64
random bytes — but it is unmetered anonymous work: a revoked token drives `AuthService.cs:106-113`
into a query over all the user's active tokens **plus writes**, as fast as the network allows.

Arrived with refresh tokens in `be7b1ff`; untouched by this milestone. Flagged for scheduling.

## 🟢 Throttle counters do not advance when the IP limiter rejects first

The middleware rejects before the action runs, so a per-IP 429 never reaches `RecordFailure`.
Benign — the request never reached a password check either. Noted only so nobody later reads a
lockout counter as a complete tally of attempts.
