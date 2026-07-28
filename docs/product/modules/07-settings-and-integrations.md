# Module 7 — Settings & Integrations

**Status:** 🚧 Partial — RBAC + multi-tenancy are **built**; integrations not started.
**Priority:** RBAC done; integrations later.

## Purpose

Per-company configuration: who can see what, and how the system connects to the
company's existing HR stack.

## Features

### 7.1 Role-Based Access Control (RBAC) — ✅ Built
Restrict system access and visibility **according to each user's role**.
See [architecture/auth-and-tenancy.md](../../architecture/auth-and-tenancy.md).

> Note: the currently-implemented role set is the **agency** one and must be revised
> for in-house — see the migration plan.

### 7.2 HRMS & Payroll Integrations — ⬜
Connect data to the company's core **HRMS (e.g. QHRM)** and payroll systems.

### 7.3 Email & Calendar Sync — ⬜
Full integration with **Microsoft 365** or **Google Workspace**. Required by Module 3
(scheduling) and Module 4 (offer emails).

### 7.4 Data Security & SLA Configuration — ⬜
Per-company configuration of **system recovery objectives**, **data retention policy**,
and security settings, in line with that company's SLA.

## Entities

- `IntegrationConfig` — provider, credentials ref (secret store — **never** in the DB in plaintext), status
- `RetentionPolicy` — per entity type, retention period, purge behaviour
- `AuditLog` — who did what, when (needed for both security and SLA claims)

## Open questions

- QHRM integration direction: push, pull, or bidirectional? Does it have a public API?
- On "hired", does the employee record get **created in the HRMS automatically**?
- Where do integration credentials live? (Key vault vs. encrypted column — a security decision, needs an ADR.)
- Retention policy vs. legal minimums — who is accountable if a company configures a shorter period than the law allows?
