# ADR-0013 — Infrastructure: AWS RDS PostgreSQL, Cloudflare R2, JSONB

- **Date:** 2026-07-27
- **Status:** Accepted
- **Related:** [ADR-0004](ADR-0004-single-tenant-deployment.md), [ADR-0009](ADR-0009-myanmar-script-handling.md), [ADR-0011](ADR-0011-commercial-model-v2.md)

## Decisions

### Database — PostgreSQL on AWS RDS
Confirms the existing PostgreSQL choice, now with a hosting target. **Every tier gets a
dedicated database** — Mid-Tier included. The "Dedicated DB" line in the Enterprise tier
refers to a dedicated *RDS instance* (with Multi-AZ), not to database-level separation,
which all customers get.

⇒ [ADR-0004](ADR-0004-single-tenant-deployment.md) stands unchanged: one instance + one
database per company, tenant plumbing kept **dormant** as a misconfiguration safety net.

### JSONB for dynamic fields — endorsed
The v2.0 design calls for JSONB, and it maps directly onto features already specified:
custom application-form fields (Module 2.2), configurable approval chains (Module 1.3),
scorecard criteria (Module 3.3). This is the mechanism that keeps customization in the
**"configuration" bucket** of [ADR-0007](ADR-0007-productization-and-addons.md) instead of
becoming per-customer code.

Guidance: JSONB for genuinely customer-defined shapes. Anything queried, sorted, or
reported on across customers stays a real column — JSONB is not a substitute for a schema.

### ⚠️ "Native Full-Text Search" — does not work for Burmese
The v2.0 doc lists Postgres native FTS as a reason for the choice. Per
[ADR-0009](ADR-0009-myanmar-script-handling.md) this is **not usable for Burmese content**:
PostgreSQL ships no Burmese text-search configuration, and Burmese has no consistent word
spacing for the tokeniser to split on.

Native FTS remains fine for **English** content. Burmese keyword search (Module 2.6) needs
**trigram indexing (`pg_trgm`)** or a segmentation-based approach, over normalized Unicode
text. Do not plan Module 2.6 around native FTS.

### Object storage — Cloudflare R2, behind an abstraction
R2 is a good fit: S3-compatible with **zero egress fees**, which matters because CVs are
downloaded repeatedly by recruiters and hiring managers — egress is the cost that quietly
grows under a fixed annual subscription ([ADR-0011](ADR-0011-commercial-model-v2.md)).

⚠️ **But R2 is a cloud service, and on-premise customers exist** — data sovereignty is a
headline value proposition. Storage must therefore sit behind an **S3-compatible
abstraction**: R2 for hosted installs, MinIO (or equivalent local S3) for on-premise. The
application must never call R2 APIs directly.

## Consequences

- Cloud cost is now a **recurring cost against a fixed annual price** — reserved instances
  and R2's zero egress are what protect that margin. Cost per install should be tracked
  from the first customer.
- Multi-AZ is an Enterprise-tier feature ⇒ another **tier flag**, and a difference in
  infrastructure provisioning between tiers.
- Backup/restore differs by mode: RDS automated snapshots (hosted) vs. the customer's own
  process (on-prem). The ADR-0004 runbook must cover both.
- An on-premise install now needs: Postgres, the app containers, and a local S3
  (MinIO) — this belongs in the server sizing guide.

## Open questions

- AWS region for hosted installs, and whether any customer will demand in-country data
  residency (which R2/AWS may not satisfy — and which would push them to on-prem).
- Does the demo environment for sales partners ([ADR-0011](ADR-0011-commercial-model-v2.md))
  share this infrastructure, and who pays for it?
