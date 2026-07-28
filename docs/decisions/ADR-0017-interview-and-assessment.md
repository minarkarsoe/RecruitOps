# ADR-0017 — Interview & assessment: criteria scope, blind scoring, panel access

- **Date:** 2026-07-28
- **Status:** Accepted (implemented)
- **Scopes:** [Module 3](../product/modules/03-interview-and-assessment.md)
- **Constrained by:** [ADR-0003](ADR-0003-department-scoping.md) — department scoping is the
  security-critical filter and is applied explicitly, never as a query filter
- **Defers to:** Module 7 for calendar sync and email delivery

## Context

Module 3 turns the `Interview` pipeline stage from a label into a workflow. The spec (3.1–3.4)
describes calendar integration and automated invitations, but **neither an email sender nor a
calendar client exists in this codebase**, and Module 7 owns integrations. This ADR covers the
slice that has no external dependency: scheduling by hand, in-system scorecards, and
collaborative notes.

Four decisions had to be made before any entity could be written, because each one changes the
schema or the authorization surface.

## Decision

### 1. Scorecard criteria are department templates with a per-posting override

`ScorecardTemplate` carries **at most one** of `DepartmentId` or `JobPostingId`; with neither
it is the company-wide default. Resolution for a posting is most-specific-wins:

```
posting-specific template  →  posting's department template  →  company-wide default
```

Rejected alternatives:

- **Global per company only.** Cheap, and wrong for the product: the criteria that make an
  engineer comparable ("system design", "code quality") make a salesperson incomparable. A
  single list forces every department to score against fields that don't apply to them, and
  the comparability the module exists to deliver quietly disappears.
- **Per posting only.** Maximum flexibility, and nobody would use it — a recruiter opening a
  new posting would face an empty criteria list every time and would paste in whatever they
  used last, which is exactly the inconsistency 3.3 is meant to remove. Templates only work
  if there is a sensible default already sitting there.

The department level is the default *because* it is the level at which comparison is
meaningful; the posting override exists for the genuinely unusual role.

### 2. A submitted scorecard snapshots its criteria

`ScorecardResponse` stores `CriterionLabel` and `CriterionType` **as they read at submission**,
alongside the `ScorecardCriterionId`.

Same reasoning as the approval-chain snapshot in Module 1: a template edited in September must
not silently rewrite what an interviewer answered in July. Without the snapshot, renaming a
criterion from "Communication" to "Stakeholder management" retroactively changes the meaning of
every score ever recorded against it, and the audit trail becomes a lie. The FK is kept so
current-template analytics still group correctly; the snapshot is what makes an old scorecard
readable on its own terms.

### 3. Scores are blind until the reader submits their own

**If the caller is a participant on the interview, they see only their own scorecard until
they submit it.** After submitting, they see every *submitted* scorecard on that interview.
A caller who is not a participant (a recruiter tracking the loop, an HR director) sees all
submitted scorecards immediately.

Drafts are visible only to their author, always — including to non-participants. An unfinished
evaluation is not an opinion yet.

The reasoning is anchoring bias: an interviewer who reads "Strong Yes, 5/5" before writing
their own assessment writes a different assessment. The whole value of a panel is independent
observations, and a panel that can read each other first is an expensive way to get one
opinion repeated four times.

Two consequences we accept:

- **It is enforced in the service, not the UI.** It is an authorization rule — a hidden
  element in the SPA is a decoration, not a control, and the API is directly reachable.
- **It is not tamper-proof against a recruiter.** A non-participant company-wide role can
  read submitted scores and could relay them verbally. This is a bias guardrail, not a
  confidentiality boundary, and pretending otherwise would mean locking recruiters out of
  their own pipeline.

### 4. Participation grants narrow access to the application

A panel member is often a Hiring Manager from **another department**, who under ADR-0003 has no
access to the application at all. Without an exception, cross-department panels — HR plus a
technical lead, the single most common real panel — are impossible.

**An `InterviewParticipant` row grants its user read access to that one job application**, its
interviews, its notes, and the scorecards the blind rule allows. It grants nothing else: no
department access, no other application in that department, and **no writes** to the pipeline.
Rescheduling, cancelling and moving the stage stay with department-scoped recruitment staff.

> **"No writes" means no writes to the *process*.** A participant does write their own
> scorecard and does post to the note thread — that is the work they were added to do, and
> both are pinned by tests (`A_Panel_Member_Can_Read_And_Join_The_Thread`). `CanWrite` gates
> rescheduling, panel changes and stage moves; `ScorecardService` and `NoteService.CreateAsync`
> deliberately gate on participation instead. Clarified 2026-07-28 after the Module 3 security
> review found this paragraph and the code disagreeing.

Participation is also the **only** way some roles reach an application at all: `Approver` has no
standing candidate reach ([ADR-0018](ADR-0018-approver-candidate-data-exclusion.md)), so putting
an approver on a panel is a deliberate, visible, expiring grant rather than a blanket one.

This is resolved in one place, `IApplicationAccess`, which returns both *whether* the caller can
reach the application and *how* (`ViaDepartment` vs `ViaParticipation`). Interviews, scorecards
and notes all call it. Spreading the same rule across three services is how this repo has
produced the same bug three times — a guard added to two of three sibling methods.

Rejected: a general `ApplicationAccessGrant` entity. It is the more flexible design and it
introduces a second, manually-curated permission system that nobody would remember to revoke.
Participation is already the fact we want to key on, and it expires naturally with the panel.

### 5. Scheduling an interview moves the stage, in one transaction

`ScheduleAsync` creates the interview **and**, when the application is not already at
`Interview`, sets the stage and appends `ApplicationStageHistory` — in a single
`SaveChangesAsync`.

Two separate writes would let an interview exist against an application still sitting at
`Screening`. Module 5 reads stage history to compute time-to-interview; a gap there cannot be
reconstructed afterwards, which is the same argument that put `ApplicationStageHistory` in the
codebase months before anything read it.

Scheduling against a `Hired` or `Rejected` application is a 409, not a silent stage move —
those statuses are terminal by ADR precedent, and re-opening them corrupts Module 5's figures.

## Consequences

- `ScorecardTemplate` needs an admin UI before customers can use anything but the default;
  until then the company-wide template is seeded.
- The blind rule means an interviewer's own draft is fetched by a different endpoint shape
  than the panel view. Two endpoints, deliberately, so the visibility rule lives in one of
  them and cannot be bypassed by a query parameter.
- Notes are user-authored text rendered in the SPA. They are stored raw and **escaped on
  output**; `@mentions` are resolved to user ids server-side rather than trusted from the
  client, so a mention cannot be forged to make a note appear addressed to someone.
- Candidate self-scheduling (spec open question 2) is **deferred, not designed out**:
  `Interview.ScheduledStart` is nullable-free today, but proposed-slot rows can be added
  later without touching what exists.
- Calendar event creation and invitation email remain open, owned by Module 7. Nothing in
  this ADR assumes either.

## Follow-ups

- `NotificationLog` (spec entity) is **not** created — there is nothing to log until a sender
  exists. Add it with Module 7.
- Interviewer availability / free-busy, once a calendar integration lands (3.1).
- Weighted overall scoring across criteria, once real templates exist to weight.
