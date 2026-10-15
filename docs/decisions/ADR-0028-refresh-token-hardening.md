# ADR-0028 — Refresh tokens: hashed at rest, revoked with the account, bounded in time

- **Date:** 2026-10-15
- **Status:** ✅ Accepted — built the same day
- **Amends:** [ADR-0002](ADR-0002-jwt-auth.md), which said there would be no refresh token. Develop
  added one (rotation and reuse detection included) without an ADR; this records it and hardens it.
- **Relates to:** [ADR-0016](ADR-0016-login-brute-force-protection.md) (rate limits),
  [ADR-0021](ADR-0021-role-assignment-and-custom-role-scope.md) (every request re-checks the
  account), [ADR-0027](ADR-0027-audit-log.md) (audit log)
- **Found by:** the security review of the develop merge (B3), 2026-10-15

## Context

A refresh token is a 14-day credential: whoever holds it can mint access tokens. The review found:

1. **Stored in plaintext** — `RefreshTokens.Token`, and the next token in the chain in
   `ReplacedByToken`. Anyone who reads the table — a backup, a replica, a support query — holds
   every live session.
2. **Not revoked on deactivation.** A deactivated user's refresh was refused only while they were
   inactive; reactivating them within 14 days revived every token they had, including one an
   attacker had copied.
3. **No rate limit** on `/api/auth/refresh` (or `/revoke`).
4. **No end.** Each rotation granted 14 more days, so an active session never expired.

## Decision

1. **Store a SHA-256 hash, never the token.** Lookups hash the presented token and compare hashes.
   No salt or slow hash is needed: the tokens are 512 random bits, so there is nothing to guess —
   the hash only has to make a stolen table useless. `ReplacedByToken` becomes
   `ReplacedByTokenHash`. The migration hashes existing rows in place (Postgres `sha256()`), so
   signed-in users stay signed in.
2. **Deactivating a user revokes all their refresh tokens**, in the same save as the
   deactivation. Reactivation restores the account, not its sessions.
3. **Absolute session lifetime.** A session is everything descended from one sign-in; it records
   `SessionStartedAt`, carried through rotations, and ends `Jwt:SessionMaxDays` (default 30) after
   it — the token's own `ExpiresAt` is capped at that point too, so the client is told the truth.
4. **Rate-limit `/refresh` and `/revoke` per client IP** (`RateLimit:Refresh`, 60/60 s), reusing
   the login limiter's partitioning (IPv6 /64).
5. **Reuse means a *rotated* token came back.** Only a token that was traded for a successor
   (`ReplacedByTokenHash` set) is evidence of a copy: it revokes all of the user's sessions and is
   recorded in the audit log (`RefreshTokenReused`, shown as a refusal). A token ended any other
   way — sign-out, deactivation, swept up by a reuse response — is simply refused: a browser
   refreshing it is not an attack, and logging it as one (as the first build did, caught by the
   smoke test) teaches auditors to ignore the alarm.

## Consequences

- Rolling the migration back cannot recover plaintext tokens: `Down` revokes every refresh token,
  and everyone signs in again.
- Users sign in at least every 30 days. A customer that wants longer or shorter changes
  `Jwt:SessionMaxDays`.
- **Not done:** the SPA still keeps the refresh token in `sessionStorage`, readable by script on
  the page. An HttpOnly, `SameSite=Strict` cookie scoped to `/api/auth` would take it out of XSS's
  reach; it needs CSRF handling on `/refresh` and changes the SPA's auth flow, so it is its own
  change. Expired and revoked rows are not purged yet.
