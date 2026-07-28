# ADR-0018 — An Approver reaches requisitions, not candidates

**Status:** Accepted · **Date:** 2026-07-28 · **Supersedes part of:** [ADR-0003](ADR-0003-department-scoping.md)
**Found by:** the Module 3 security review (see `docs/status/SECURITY-REVIEW-MODULE-3.md`)

## Context

ADR-0003 lists the roles that are **not** department-scoped: `Admin`, `HrDirector`,
`Recruiter`, `Approver` — the last with the justification *"an approver must see what they're
approving, which may cross departments."*

That reasoning is sound for the thing it was written about. An approval chain routinely crosses
departments: Finance signs off on a Sales headcount, and fencing them to their own department
would make the chain unrunnable. So `Approver` was made company-wide.

But "not department-scoped" was implemented as a single boolean, `ICurrentUser.IsDepartmentScoped`,
and every scoping decision in the codebase asks that one question. `DepartmentAccess.CanAccessAsync`
therefore returns `true` for an Approver on **every** department — and every service that guards
candidate data with `CanAccessAsync` inherited that answer without anyone deciding it should.

Module 3 made the consequence concrete. `ApplicationAccess.ResolveAsync` returned
`Kind = Department, CanWrite = true` for an Approver against **every job application in the
company**, and the endpoints are `Policies.InternalUser`, which includes `Approver`. That meant
an Approver could, over HTTP, with no participation in anything:

| Endpoint | What they got |
|---|---|
| `GET /api/applications/{id}/notes` | any candidate's debrief thread |
| `POST /api/applications/{id}/notes` | write into it |
| `GET /api/interviews/{id}/scorecards` | not a participant → not blinded → **every submitted scorecard, company-wide** |
| `GET /api/applications/{id}/history` | any candidate's full stage history |
| `GET /api/postings/{id}/pipeline` | any department's pipeline board |

The `NoteService` mention resolver made the same mistake independently, and more sharply. Its own
doc comment gave the threat as:

> A handle only resolves if that user could reach this application themselves. Otherwise a mention
> becomes a disclosure channel: `"@finance.approver what do you think of this candidate"` would put
> a name and a judgement in front of someone with no business seeing either.

The code below it read `var scoped = user.Role is UserRole.HiringManager; if (!scoped) return true;`
— so `@finance.approver` resolved. **The named example was the case that passed.** The existing
test used `@finance.manager`, a `HiringManager`, and so was green throughout.

Nothing here was reachable through a bug. Every layer did exactly what it said, and the aggregate
was a privilege nobody granted on purpose.

## Decision

**Department scoping and candidate-data reach are two different questions, and `Approver` gets
different answers to them.**

1. `RoleScope` (in `Domain`) is the one place either question is answered:
   - `IsDepartmentScoped(role)` — the requisition/posting axis. `HiringManager` only.
     **Unchanged from ADR-0003**; an Approver stays company-wide here.
   - `IsExcludedFromCandidateData(role)` — applications, pipeline, stage history, interviews,
     scorecards, notes. `Approver` only.

2. `ICurrentUser` exposes both. Both **fail closed** on an unrecognised role.

3. `ApplicationAccess` applies the exclusion as *clause 0*, before department scoping.
   `PipelineService` applies it through one private helper shared by its three methods.

4. **An excluded role is not locked out permanently.** An Approver reaches an individual
   application by sitting on its interview panel, exactly as a Hiring Manager from another
   department does (ADR-0017 §4). If an approver genuinely needs to assess a candidate, put them
   on the panel — that is a decision someone makes, in a place that shows who made it, and it
   expires with the panel.

5. `IApplicationAccess.ResolveForUserAsync` answers the reach question about a **third party**,
   so mention resolution calls the same implementation instead of keeping a private copy.

## Consequences

- An Approver's day job is unaffected: requisitions, approval chains and decisions all run through
  `RequisitionService`, which asks `IsDepartmentScoped` and gets the same answer as before.
- A company that wants approvers in the pipeline must add a role or put them on panels. We think
  that is the right friction — "who can read what candidates say in interviews" should be an
  explicit answer, not a side effect of an approval-routing decision.
- **The blanket is not gone, only narrowed.** `HrDirector` and `Recruiter` are still company-wide
  over candidate data, which is intended. If that ever needs revisiting, `RoleScope` is where it
  is written.
- The role set is still coarse. `FEATURE-STATUS.md` already flags that it needs revision; this ADR
  makes the case sharper rather than settling it.

## Alternatives rejected

**Add `Approver` to `IsDepartmentScoped`.** One-line change, and it breaks approval outright: an
approver could no longer see the cross-department requisition they were asked to decide on. It also
under-fixes — an approver *in* the Sales department would still get every Sales candidate.

**Filter at the policy layer — drop `Approver` from `InternalUser`.** Cheaper, and wrong in the
direction this repo has been burned by before. It puts the rule in a place a new endpoint does not
inherit, and it would block an Approver who is legitimately on a panel. ADR-0003's position holds:
the policy layer is not the access control.

**Do nothing; document it as intended.** Considered seriously — it *was* the specified behaviour,
traceable to a real ADR. Rejected because no document says an approver may read candidate debriefs;
it fell out of an argument about headcount forms. A privilege that nobody wrote down and nobody
would defend out loud is not a decision, it is an accident with a paper trail.
