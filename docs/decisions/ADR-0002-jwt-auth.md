# ADR-0002 — Self-issued JWT for authentication

- **Date:** 2026-07-27
- **Status:** Accepted
- **Context:** documented retroactively; the decision was made and implemented in the same session.

## Context

`CLAUDE.md` originally left auth as "TBD". Multi-tenant isolation cannot be implemented
without it — the tenant has to come from somewhere trustworthy — so it blocked all
data-scoped work. Options: self-issued JWT, ASP.NET Core Identity, or an external IdP.

## Decision

**Self-issued JWT bearer tokens** (HS256, 8-hour lifetime), carrying `tenant_id` and
role claims. Passwords hashed with `IPasswordHasher<User>` from
`Microsoft.Extensions.Identity.Core` (framework-provided, not hand-rolled).

## Rationale

- The token is the natural carrier for `tenant_id`, which the DbContext query filters
  need on **every** request.
- No external dependency or vendor account needed to develop against.
- Full control over claims; simplest thing that supports the isolation requirement.

## Consequences

- We own credential security: rate limiting, lockout, and password reset are **our**
  problems and are **not yet built** — a known gap.
- No refresh token; 8-hour access token is the only credential. Re-login on expiry.
- Symmetric key (HS256) means the signing key must be protected wherever the API runs.
  If a separate service ever needs to *verify* tokens, revisit and consider RS256.
- Login matches on email alone → email must be unique across all companies. Same-email
  multi-tenant users need a tenant selector; deferred.

## Revisit if

The customer is an enterprise requiring **SSO / SAML / Entra ID** — very plausible for
the in-house model, since buyers are companies with existing identity infrastructure.
This would supersede this ADR.
