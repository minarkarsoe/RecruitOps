# Deployment Model

**Decided in:** [ADR-0004](../decisions/ADR-0004-single-tenant-deployment.md) ·
**Commercial context:** [ADR-0005](../decisions/ADR-0005-commercial-model.md)

## Shape

**One application instance + one PostgreSQL database per company.** Not a shared SaaS.
Isolation is physical. A company is reached by **subdomain**.

Two hosting modes:

| Mode | Who runs the server | Subdomain resolves via | Maintenance |
|---|---|---|---|
| **On-premise** | The customer | Their DNS → their server | ~20% of licence |
| **Vendor-hosted** | Us, dedicated instance per customer | Our reverse proxy → their instance | ~10% of licence |

Server specification (CPU / RAM / SSD) is **sized per customer** — it is a priced line
item, not a fixed platform.

## What this means for the code

- **Configuration by environment variables only.** No per-customer code branches or
  builds. One artifact, many configurations.
- **No cross-company queries are possible** — anything resembling a global admin view
  across customers cannot exist by design.
- **Migrations must run automatically and idempotently on startup.** An on-prem
  customer cannot be asked to run EF tooling by hand.
- **`Company` is a single-row table** per deployment: name, logo, branding, settings.
  Used by career pages, offer letters and emails.
- **Tenant filters remain but are dormant** — a safety net against misconfiguration, not
  the primary isolation boundary. The security-critical filter is now
  **department scoping** ([ADR-0003](../decisions/ADR-0003-department-scoping.md)).

## Required before the first customer install

| # | Item | Why |
|---|---|---|
| 1 | ~~Docker / Compose packaging~~ ✅ **done** (unverified — never built) | [ADR-0015](../decisions/ADR-0015-containerisation.md) |
| 2 | Automated EF migrations on startup | Unattended upgrades on customer hardware |
| 3 | `/api/version` endpoint | Answer "which version is this customer on?" |
| 4 | Customer & version registry | Support triage |
| 5 | Documented support policy (latest, latest-1) | Caps support cost |
| 6 | Server sizing guide by company size | Prevents under-specced installs |
| 7 | Backup & restore runbook | Especially for self-hosted customers |
| 8 | Upgrade runbook, safe to run unattended | Upgrades happen on their server |

Item 1 is now written (though never built). Items 2–8 do not exist yet.

## Subdomain routing

- **Vendor-hosted:** wildcard DNS + reverse proxy (nginx / Traefik / Caddy) mapping
  `<company>.<domain>` → that customer's container. TLS via wildcard or per-host certs.
- **On-premise:** the customer's own DNS and TLS; we document the requirement.

Because each instance serves exactly one company, the subdomain is **routing only** —
the application does not need to resolve a tenant from the hostname. If a shared-hosting
tier is ever introduced, that changes, and the dormant tenant plumbing is what makes it
possible.

## ⚠️ Open constraint — AI/OCR on-premise

Module 2's OCR auto-profiling and Smart Match cannot assume outbound internet or
third-party APIs. On-prem customers may have neither connectivity nor willingness to
send CVs off-site. Needs an ADR before Module 2. Affects architecture, server sizing
and pricing.
