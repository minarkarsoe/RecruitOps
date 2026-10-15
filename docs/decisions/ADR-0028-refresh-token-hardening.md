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
   smoke test) teaches auditors to ignore the alarm. Three more limits came from the security
   review of this ADR:
   - an **expired** token, rotated or not, is just refused — otherwise any old rotated token (a
     backup, a forgotten laptop) could end a user's sessions on demand, for ever;
   - a rotated token back **within 30 seconds** of its rotation is the client's own duplicate
     (parallel requests after a laptop wakes, a lost response retried), and is just refused;
   - the alarm is written **once per sweep that ended something** — replaying the same copy
     ends nothing more and must not keep writing permanent rows.
6. **The SPA keeps its side of this.** After a 401 it reads the refresh token from storage at
   that moment, not the one the failed request was sent with — requests queued behind one that
   already refreshed would otherwise send the rotated token back and set off the alarm. A
   refresh answered 429, or not answered at all, keeps the session (it used to sign the user
   out). Signing out calls `/api/auth/revoke`; before, it only cleared the tab, and the token
   stayed valid on the server.
7. **Reactivation also revokes**, and the migration revokes the tokens of users already inactive:
   a refresh in flight during a deactivation could otherwise save a fresh token that the
   reactivation would revive.

## Consequences

- **Pre-migration copies still hold plaintext.** Hashing in place does not erase the old column
  from backups, replicas, WAL or old heap pages. Live tokens expire within 14 days of issue, so
  the exposure ends then; expire pre-migration backups after that, and `VACUUM FULL
  "RefreshTokens"` (outside a transaction) if it matters sooner.
- Two refreshes with the same token at the same instant can still both succeed (no concurrency
  token on rotation) — no worse than before this ADR; a compare-and-set rotation is the fix if it
  ever matters.
- Rolling the migration back cannot recover plaintext tokens: `Down` revokes every refresh token,
  and everyone signs in again.
- Users sign in at least every 30 days. A customer that wants longer or shorter changes
  `Jwt:SessionMaxDays`.
- **Not done:** the SPA still keeps the refresh token in `sessionStorage`, readable by script on
  the page. An HttpOnly, `SameSite=Strict` cookie scoped to `/api/auth` would take it out of XSS's
  reach; it needs CSRF handling on `/refresh` and changes the SPA's auth flow, so it is its own
  change. Expired and revoked rows are not purged yet.
