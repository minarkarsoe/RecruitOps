# ADR-0023 — A rejected requisition can be revised, and each submission is a round

**Status:** Accepted · **Date:** 2026-08-16 · **Related:** [ADR-0003](ADR-0003-department-scoping.md),
[ADR-0022](ADR-0022-permission-driven-requisition-authority.md), [ADR-0024](ADR-0024-senior-skip-ahead-approval.md)
**Requested by:** the product owner — *"if it gets rejected, let it be corrected and resubmitted."*

## Context

`Rejected` was terminal. `UpdateAsync` permitted edits only in `Draft`
(`RequisitionService.cs:242`) and `SubmitAsync` permitted submission only from `Draft` (`:148`), so a
rejected requisition could never be corrected. The only path forward was to raise a brand-new
requisition, which loses the thread: the rejection comment — the single most useful sentence in the
whole record, because it says what to fix — stays attached to a dead row nobody opens again.

Notably, this rule was enforced **only** by those two `if` statements and was covered by **no test**.
Nothing had to be deleted or inverted to change it, which also means nothing was guarding it either
way.

## Decision

`Rejected` is no longer terminal. The requester may return a rejected requisition to `Draft`, edit
it, and submit it again. `Approved` and `Cancelled` remain terminal.

Each submission is a **round**. `RequisitionApproval` gains a `Round` column; `SubmitAsync` stamps
out a fresh set of steps at round *n+1* and leaves round *n* exactly as it was.

### Why rounds, rather than resetting the existing rows

Resetting the round-1 rows to `Waiting` and reusing them is cheaper and needs no migration. It is
also the one thing the module spec explicitly forbids. From the cancellation rule
(`docs/product/modules/01-job-requisition-approval.md:88-91`), which predates this change:

> Cancelling **does not touch the approval steps** … rewriting the steps would fabricate decisions
> nobody made.

A reset would erase *"Finance rejected this on 12 Aug because the headcount of 3 was not
justified"* — the very sentence the revision exists to answer. The round-1 rows must survive
verbatim, so a second round needs somewhere else to live.

### Why each round restarts at step 1

Carrying an earlier `Approved` forward into the new round would credit an approval to a document
nobody approved. If the requester raised the salary budget after Finance rejected it, the Dept
Head's round-1 approval was granted to a different requisition than the one now in flight. Every
round is decided afresh.

The chain itself is re-resolved at submit time, so a chain edited between rounds applies to the new
round only — consistent with how the first submission already works.

### Why `Draft` and not a new status

Recorded with the product owner on 2026-08-14. A new `Revising` status would have to be learned by
every consumer — status badges, list filters, the inbox, analytics, `packages/types` — and each is a
place to forget it. `Draft` already carries every rule a revision needs: it is editable, submitting
from it already stamps out approval rows, and the post-submit 409 freeze already applies.

**The cost, stated plainly: `Draft` stops meaning "never submitted."** It now also means "rejected
and being revised". Any code inferring "never submitted" from the status is wrong after this change;
round history is the source of truth. The known readers were checked —
`DepartmentService.OpenRequisitionQuery` counts `Draft || PendingApproval` as *open*, which stays
correct, and the frontend's `canEdit`/`canCancel` gates are correct by construction rather than by
luck: a revision genuinely is an editable, cancellable draft.

### Only the requester may revise

Mirrors cancellation (`module doc :83-86`). An approver's tool is Reject, which records a decision;
being asked to approve something is not authority to rewrite it and put it back in the queue.

## The part that is easy to get wrong

Five sites read `RequisitionApprovals`, and every one assumed a single round existed. Left
unscoped, a second round does not throw — it silently misbehaves:

| Site | Failure with two rounds |
|---|---|
| `GetInboxAsync` pass 1 | A leftover round-1 `Waiting` row makes a stale approver a candidate again. |
| `GetInboxAsync` pass 2 | `MinBy(Sequence)` across rounds picks a dead round's step as "whose turn". |
| `FetchListAsync` label | Labels the row with a dead round's step. |
| `BuildDetailAsync` | Renders both rounds interleaved by sequence, with no separator. |
| `DecideAsync` completion | `approvals.All(a => a.Decision == Approved)` **can never be true** once a rejected round is preserved, so a fully-approved round 2 sits in `PendingApproval` with no `Waiting` step — invisible in every inbox, and silent. |

That last one is the reason this ADR exists rather than a one-line status change. All five are now
scoped to the current round.

**The unique index `(RequisitionId, Sequence)`** (`AppDbContext.cs:295`) becomes
`(RequisitionId, Round, Sequence)`. Without that, a second round reusing sequences 1..n violates the
constraint on real Postgres — and would **not** fail in the test suite, which runs on the EF
in-memory provider and does not enforce unique indexes. A green suite over a constraint violation is
exactly the failure mode this repo has shipped before.

## Consequences

- The rejection comment now sits above the revision it produced, which is where it is useful.
- The detail timeline groups by round. React keys move from `step.sequence` to
  `round-sequence`; duplicate sequences across rounds would otherwise produce duplicate keys.
- One migration, shared with ADR-0024. **Proposed, not applied**, per CLAUDE.md.
- `Submitting_Twice_Is_A_Conflict` keeps its assertion but its name is now imprecise: the invariant
  it protects is "cannot submit something that is not a Draft", which is still true. Submitting
  twice *is* legal when a rejection and a revision happened in between.

## Schema review outcome

The `db-schema-reviewer` pass confirmed both columns are safe on a populated table — on Postgres 11+
a `NOT NULL` column with a *constant* default is a catalog-only change, so `Round` backfills with no
rewrite and no separate data migration. Three things came out of it that changed the work.

**The migration was rewritten to stop holding an exclusive lock across the index build.** The
generated version ran all four operations in EF's single implicit transaction with `DropIndex`
first. Postgres holds DDL locks until the transaction *commits*, not until the statement finishes —
so the old index's `ACCESS EXCLUSIVE` lock covered the whole `CREATE INDEX` build, blocking reads
*and* writes on `RequisitionApprovals` for its duration rather than just writes. One instance per
company (ADR-0004) bounds the blast radius to a single customer, but "that customer's approvals
stop responding during deploy" is exactly the outcome worth avoiding. It is now: add columns →
`CREATE UNIQUE INDEX CONCURRENTLY` (outside a transaction) → drop the old index. Building before
dropping also means the table is never without uniqueness enforcement, even briefly.

Note this makes the migration non-atomic, and a failed `CREATE INDEX CONCURRENTLY` leaves an
**invalid** index behind that enforces nothing. Recovery is manual; the steps are in the migration
file rather than here, so whoever hits it finds them where they are looking.

**`Down()` is destructive and can fail, and now says so.** Dropping `Round` collapses distinct
`(RequisitionId, Round, Sequence)` rows into duplicate `(RequisitionId, Sequence)` pairs, so
recreating the old unique index throws `23505` on any database that has seen a second round. The
operations were reordered so the index rebuild happens *first*, while `Round` still exists: on such
a database the rollback now fails before dropping either column, with nothing destroyed. The
generated order would have destroyed the audit data and *then* failed to restore the constraint.

**A silent-default trap was closed in the model, not just the migration.** EF inferred
`ValueGeneratedOnAdd()` from `HasDefaultValue(1)`, which means it omits `Round` from the `INSERT`
whenever the in-memory value is `0` and lets Postgres substitute `1`. Harmless today — every insert
path sets it — but a future path that forgot would not throw; it would file the step under round 1,
a wrong answer rather than a loud one. `.ValueGeneratedNever()` now forces the value we actually
mean onto every insert.

**Known and accepted: a rolling-deploy window.** `POST /{id}/revise` produces something that could
not previously exist — a `Draft` requisition that already has approval rows. If a new-code instance
revises a requisition and a subsequent `submit` lands on an **old-code** instance, that instance's
`SubmitAsync` knows nothing about rounds and would insert `Sequence 1..n` at the defaulted
`Round = 1`, colliding with round 1's preserved rows. The unique index catches it — the failure is a
`23505` surfaced as a 500 rather than the friendly 409 the new code produces. Not silent corruption,
but not a good error either. Deploy new-code instances to 100% before exposing `/revise` in the UI.
