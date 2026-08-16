# ADR-0024 — A later approver may approve forward, but never reject forward

**Status:** Accepted · **Date:** 2026-08-16 · **Related:** [ADR-0003](ADR-0003-department-scoping.md),
[ADR-0018](ADR-0018-approver-candidate-data-exclusion.md),
[ADR-0022](ADR-0022-permission-driven-requisition-authority.md), [ADR-0023](ADR-0023-revise-and-resubmit-rounds.md)
**Requested by:** the product owner — *"I'm number 2 and you're number 1: I can skip over you and
approve both 1 and 2. But it has to show the record of what I did."*

⚠️ **This is an authorization change.** Per CLAUDE.md it requires a `security-reviewer` pass before
the work is called done.

## Context

Approval steps are strictly sequential. `DecideAsync` selected the lowest-sequence `Waiting` step and
required the caller to be *that* step's approver (`RequisitionService.cs:203-205`); `GetInboxAsync`
ran a second filtering pass to hide anything not yet the caller's turn (`:67-81`).

**That behaviour is deliberate, not accidental.** The inbox comment says the lowest-sequence
`Waiting` step must be the caller's *"or a later approver could act early"*, and three tests assert
it: `A_Later_Approver_Cannot_Jump_The_Queue`, `Sequential_Approval_Logic_Enforces_Step_Order`, and
`Inbox_Only_Shows_Requisitions_Waiting_On_You`. This ADR reverses a decision the code was written
specifically to enforce, which is why the audit requirement below is not decoration.

The product reason is ordinary: the chain models an org hierarchy — Dept Head → Finance → HR — and
the people later in it outrank the people earlier in it. A senior who is willing to sign off should
not have to wait for a junior who is on leave, and today they must.

## Decision

**A later step outranks an earlier one.** An approver may approve every `Waiting` step in the current
round at or below their own sequence, in one action.

**Approve only.** A senior may reject, but only their own step. Reject-forward is refused.

**The record names who actually acted.** `RequisitionApproval` gains `DecidedByUserId`, written only
when the decider is not the assigned `ApproverUserId`.

### Seniority is chain position, and nothing else

Confirmed with the product owner. No rank attribute is introduced on users or roles. This matters
because seniority was *deliberately removed* from the role model earlier —
`docs/architecture/auth-and-tenancy.md:46` collapses SeniorRecruiter/JuniorRecruiter into Recruiter
with the note *"seniority isn't a permission boundary here"*. Chain position is the only ordering
this system has, and reintroducing a user-level rank to serve one feature would contradict that.

The rule is therefore self-contained: authority to skip comes from being named later in **this**
chain, on **this** requisition. It confers nothing anywhere else.

### Why reject-forward is refused

Approving forward and rejecting forward are not symmetric, and treating them as one feature is the
mistake this section exists to prevent.

Approving forward removes a junior's step but not their say: the requisition proceeds, which is what
the junior's approval would have caused anyway. **Rejecting forward ends the requisition before the
junior ever sees it** — the senior's opinion silently replaces a review that never happened.

A senior who wants it stopped can still reject their own step, and that is terminal for the round.
The outcome is available; only the erasure of the junior's step is not.

### Why the actor is recorded rather than the assignee overwritten

Same principle ADR-0023 leans on: the chain is a record of what happened, not of what the template
expected. Overwriting `ApproverUserId` with the senior would make the row claim the senior was
always the assigned approver, which is false and unfalsifiable after the fact. Keeping both fields
lets the timeline read *"approved by the HR Manager, on behalf of Finance"* — which is what the
product owner asked for in the same sentence as the feature itself.

`DecidedByUserId` is nullable, and null means the assignee decided it themselves. That keeps every
existing row correct with no backfill.

### The inbox shows junior-step work, marked

Confirmed with the product owner. `GetInboxAsync` drops its second pass and returns every
requisition where the caller has a `Waiting` step in the current round; the response carries whose
turn it actually is so the UI can mark rows the caller *may* act on but which are not yet theirs.

Hiding them would make the feature undiscoverable — a senior would have to already know a
requisition existed to skip ahead on it.

## What this deliberately does not change

**Department scoping.** `DecideAsync` has no `CanAccessAsync` call, intentionally and pre-existing
(ADR-0022:126-127) — approval chains cross departments by design (ADR-0018). Skip-ahead does not add
one, and just as importantly does not *widen* anything: the caller must still be named on a step of
this requisition's chain. The set of people who can touch a given requisition is unchanged by this
ADR; only *when* they can act on it changes.

**The no-oracle rule.** A caller with no `Waiting` step in the current round still gets 404, not
403 — indistinguishable from a requisition that does not exist (ADR-0003). The new "is this caller
senior enough?" branch must not become a way to probe state, and the guard order that protects this
(identity checked before the status check, `RequisitionService.cs:199-205`) is preserved.

**Permission gating.** `POST /{id}/decision` still requires `permission:requisitions:requisitions:approve`
(ADR-0022). Skip-ahead is a per-row rule underneath that coarse gate, not a replacement for it.

## Consequences

- `A_Later_Approver_Cannot_Jump_The_Queue` asserts exactly what this feature enables. It is
  **re-scoped, not deleted**: it becomes a pair — a later approver *may* approve forward, and may
  *not* reject forward. Deleting it would remove the only proof that skip-reject stays blocked.
  `Sequential_Approval_Logic_Enforces_Step_Order` and `Inbox_Only_Shows_Requisitions_Waiting_On_You`
  are re-scoped for the same reason.
- Skipping **forward** stays blocked in both directions of the rule: a step-1 approver still cannot
  approve step 2. Only downward reach is granted.
- `ApprovalStepDto` and `packages/types` `ApprovalStep` gain the actual decider, so the timeline can
  attribute a skipped step without a second lookup.
- One migration, shared with ADR-0023. **Proposed, not applied.**

## Security review outcome

The `security-reviewer` pass CLAUDE.md mandates for authorization changes found **no exploitable
bypass, no oracle regression, and no way for a client to set `DecidedByUserId`** — it is written
only from `ICurrentUser`, and `ApprovalDecisionRequest` exposes no field that could bind to it.
The reach of the change was confirmed to be *when* a named approver may act, never *who*: the
caller must still hold a `Waiting` step personally assigned to them in the current round.

Two things came out of it that are worth keeping.

**A test gap, now closed.** The existing no-oracle test probed with a caller lacking `approve`,
who is stopped at the policy layer with a blanket 403 and therefore never reaches `DecideAsync` —
so nothing exercised the guard ordering the rule actually depends on.
`An_Approver_Who_Holds_The_Permission_Still_Learns_Nothing_From_A_Requisition_Not_Theirs` now
probes with a caller who does hold it, reaches the service, and must still get a 404 that is
indistinguishable from a nonexistent GUID. Without it, hoisting the status guard above the
`mine is null` check would turn the response into a 409 naming the status, and no test would notice.

**A concurrency nuance, known and accepted, not fixed here.** Previously exactly one user could
successfully call `/decision` at any given moment. Now every named approver with a `Waiting` step
in the round can, so two legitimate approvers can act simultaneously — e.g. a junior approving
their own step at the same instant a senior approves forward over it. There is no concurrency
token on `RequisitionApproval` (no `RowVersion`/`xmin`; this is a pre-existing pattern across the
entity model, not introduced here), so that race resolves last-write-wins on the step's `Comment`
and `DecidedAt`.

It is a data-integrity nuance rather than an authorization one — both parties are authorised, and
the outcome is "approved" either way. But this change genuinely widens the window in which it can
happen, which is the honest way to record it: the race existed before and was nearly unreachable;
now it is merely unlikely. Adding a concurrency token is a separate change with its own blast
radius across the entity model.
