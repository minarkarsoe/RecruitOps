# ADR-0004 — Single-tenant deployment (one instance per company), subdomain routing

- **Date:** 2026-07-27
- **Status:** Accepted
- **Supersedes:** the shared multi-tenant SaaS assumption in earlier docs
- **Related:** [ADR-0011](ADR-0011-commercial-model-v2.md) (commercial), [ADR-0013](ADR-0013-infrastructure-and-storage.md) (infrastructure)
- **Confirmed 2026-07-27:** **every tier gets a dedicated database**, Mid-Tier included.
  The Enterprise tier's "Dedicated DB" means a dedicated *RDS instance* with Multi-AZ, not
  database-level separation. This ADR therefore stands unchanged, and the tenant plumbing
  stays **dormant** rather than becoming load-bearing.

## Context

Earlier work assumed a shared multi-tenant SaaS: one deployment, many companies,
isolated by `tenant_id` query filters. The actual go-to-market is different — **each
company gets its own deployment**, on a server sized for that company (CPU/RAM/SSD vary),
either on the customer's own hardware or hosted for them. Companies are reached via
**subdomain routing** (e.g. `acme.recruitops.…`).

This is a common and sensible model for HR software in this market: HR data is highly
sensitive (PII, salary, interview notes), and many enterprises prefer it to stay on
infrastructure they control. It also fits capex-oriented buying (see ADR-0005).

## Decision

**One application instance + one database per company.** Isolation is *physical*.
Subdomain maps to that company's instance — via DNS to their server (self-hosted), or
via a reverse proxy to their dedicated instance (vendor-hosted).

### The tenant plumbing stays, demoted to a dormant safety net

`Company` (renamed from `Tenant`), the `tenant_id` claim, `ITenantScoped` and the global
query filters are **kept**, with one company row per deployment.

**Why keep it:**
- It's already built and tested; removing it means touching every entity, the auth
  pipeline and the isolation tests — real work for no immediate gain.
- It's a **second line of defence** against a configuration error (e.g. an instance
  pointed at the wrong database, or a restored backup from another customer).
- It preserves the option of a **shared-hosting tier** for small customers who won't
  pay for a dedicated server, without a re-architecture.

**Why it must be demoted in our thinking:** physical separation is now the real
isolation boundary. The security-critical, *non-optional* filter is now
**department scoping** ([ADR-0003](ADR-0003-department-scoping.md)). Do not let the
presence of tenant filters create false confidence — a bug there is now low-impact,
whereas a missing department predicate is a live data leak between colleagues.

## Consequences

### Positive
- Strongest possible data isolation; an easy answer to enterprise security review.
- Customer-controlled data residency — a genuine selling point for HR data.
- Noisy-neighbour and blast-radius problems disappear; one customer's load or outage
  can't affect another.
- Per-customer sizing lets server cost track customer size (ADR-0005).

### Negative — these are the real costs
- **N deployments to operate.** Ten customers means ten environments, ten databases,
  potentially ten versions. This is the single largest hidden cost of the model.
- **Version fragmentation.** Bug reports become "on which version?"
- **No central telemetry** unless explicitly built and permitted.
- **Migrations run N times**, in environments we may not directly control.

### Therefore these are not optional, from day one
1. ✅ **Containerised deployment** (Docker/Compose) — implemented in
   [ADR-0015](ADR-0015-containerisation.md), though the images have never been built.
2. **Automated, idempotent EF migrations on startup** — an on-prem customer cannot be
   asked to run `dotnet ef` by hand.
3. **A version endpoint** (`/api/version`) and a **customer/version registry**, so we
   always know who runs what.
4. **A documented support policy** — e.g. only *latest* and *latest-1* are supported.
5. **A server sizing guide** tied to company size, so sales doesn't guess.
6. **Config via environment variables only** — no per-customer code branches, ever.

### ✅ Constraint this placed on Module 2 — now resolved
**OCR auto-profiling and AI Smart Match cannot assume cloud APIs.** An on-premise
customer may have no outbound internet, or may refuse to send CVs to a third party —
CVs are exactly the data they chose on-prem to protect. Options: ship self-hostable
models (bigger server spec, higher cost), make cloud AI an opt-in feature with graceful
degradation, or restrict those features to the hosted tier. **Resolved by [ADR-0008](ADR-0008-document-extraction-and-ai-profiling.md):** local
extraction in the MVP (no network), AI structuring optional behind a per-install API key.
The local OCR path still affects **server sizing** — factor it into the sizing guide.

## Alternatives considered
- **Shared multi-tenant SaaS** — rejected: doesn't match the go-to-market or buyer preference.
- **Single deployment, database-per-tenant** — a middle ground that keeps one app to
  operate. Rejected because it doesn't allow on-premise, which is the customer preference here.
