# ADR-0027 — The audit log: append-only, written with the change, readable by `system:audit`

- **Date:** 2026-10-15
- **Status:** ✅ Accepted — first slice built the same day (Module 7 §7.5)
- **Scopes:** Module 7 (7.1 RBAC, 7.4 data security), every future module that changes access
- **Constrained by:** [ADR-0004](ADR-0004-single-tenant-deployment.md) (one instance + one
  database per company), [ADR-0021](ADR-0021-role-assignment-and-custom-role-scope.md) (the
  grantor ceiling whose refusals are recorded here)
- **Design:** `design/internal/settings-integrations.html` fixes retention at **Forever** — "the
  product's evidence that a decision happened … stated as permanent rather than offered as a
  choice". The viewer was not drawn (`design/internal/index.html`, "Still not drawn"); it was
  drawn into the Users & Roles screen's existing "Audit log" tab for this ADR, from the kit's own
  components, and needs a designer's review.

## Context

Until now nothing recorded who changed access. ADR-0021 made roles and their reach changeable by
delegates, with a ceiling that refuses escalation — and both the changes and the refusals vanished
the moment the response was sent. A customer asking "who made Jane an Admin, and when?" or "has
anyone been guessing passwords?" could only be answered from server logs, which are not kept,
not per company, and not readable by the people asking.

## Decision

1. **One table, `AuditLogEntries`, append-only.** No endpoint updates or deletes an entry.
   `AppDbContext.SaveChanges` throws if one is ever modified or deleted, and the migration adds
   Postgres triggers that refuse `UPDATE`, `DELETE` and `TRUNCATE` on the table — so a bug, a
   cascade or a hand-written query cannot quietly rewrite history. It has no foreign keys:
   deleting a role or user leaves its entries intact, which is why each entry **snapshots names**
   (actor, target) instead of joining for them.
2. **Written in the same unit of work as the change it records** (`IAuditTrail.RecordAsync`): the
   entry is added to the request's `DbContext` and saved by the service's own `SaveChanges`, so a
   change and its entry commit together or not at all.
3. **Events whose request saves nothing are written on their own** (`RecordStandaloneAsync`) — a
   failed sign-in, a grantor-ceiling refusal — **through a separate `DbContext`.** The refusal is
   raised mid-request, after the service may already have modified tracked entities; saving the
   request's context to record the refusal would commit exactly the change that was refused.
4. **What an entry holds:** when; who (user id + display name or email *at the time*); the action;
   the target (type, id, name at the time); a one-line summary written for a person; the
   before/after details as JSON; the client IP and the request's trace id — the same id
   problem-details error responses show, so a support ticket links to the entry. **Never**
   passwords, tokens, or business content.
5. **Tenancy.** Entries carry the tenant they happened in. A failed sign-in for an email that
   matches no active account has no tenant; such entries are shown to every tenant's auditors.
   That is right for ADR-0004's one-company install and would leak attempted emails across
   tenants on a shared one — revisit if a shared install ever exists.
6. **Readers hold `permission:system:system:audit`** (Admin and HR Director by default), through
   `GET /api/audit-log`, newest first, filterable by category, action, actor, target and time.
   Reading is not itself audited yet.
7. **Retention is forever,** per the design. Volume is bounded by the login rate limits (failed
   sign-ins are the only entries an outsider can cause).

## Consequences

- Any new code path that changes users, roles or permissions must record an entry, or the log
  silently has a hole. The services that change access today all do; the tests pin it.
- The triggers are what make "append-only" true at the database. A DBA can still drop them —
  tamper-*evidence* (a hash chain, or shipping entries off the box) is the next step if a customer's
  compliance needs it, and is deliberately not built yet.
- Erasure requests (a person asking to be forgotten) cannot be met by deleting entries; when they
  arrive they need a decision — redaction in place would require lifting the trigger for one
  statement, deliberately.

## Not in this slice

Exports, reads of candidate data, requisition decisions (already permanent in the approval
chain), `[HasPermission]` denials in general, and reads of the audit log itself.
