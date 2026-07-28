# ADR-0019 — The panel picker gets a narrower directory, not a wider policy

**Status:** Accepted · **Date:** 2026-07-28
**Relates to:** [ADR-0017](ADR-0017-interview-and-assessment.md), [ADR-0018](ADR-0018-approver-candidate-data-exclusion.md)
**Found by:** building Module 3's UI — the first consumer of the scheduling API

## Context

Scheduling an interview is `RecruitmentStaff` work (ADR-0017): Admin, HrDirector and Recruiter.
`ScheduleInterviewRequest.ParticipantUserIds` is `[Required, MinLength(1)]`, because an
interview with nobody on it cannot be scored and would sit in the pipeline looking scheduled.

So a Recruiter must name the panel. The only endpoint that lists people is
`GET /api/users`, which is `AdminOnly` — it exists for the approval-chain builder, where
picking an approver is an Admin task. `GET /api/departments/{id}/members` is `AdminOnly` too.

The result is an API that cannot be driven by the role it was opened to. A Recruiter can
schedule an interview only by obtaining user GUIDs some other way and pasting them in. This was
invisible until a UI was written against the contract; the Module 3 test suite posts ids it
already holds, so nothing failed.

## Decision

Add `GET /api/users/selectable`, open to `RecruitmentStaff`, returning `SelectableUserDto`:
**id, display name, role — no email address.** `GET /api/users` keeps its `AdminOnly` policy
and its shape.

The alternative was one line: widen `GET /api/users` to `RecruitmentStaff`. We didn't, because
the two endpoints answer different questions and only one of them needs an email address. The
picker needs a name to show and an id to post. Widening the existing endpoint would publish
every email address in the company to every recruiter as a side effect of wanting a dropdown —
a payload growing an audience it was not written for is how a directory becomes an export.

Three properties of the new endpoint are deliberate and each is easy to "fix" wrongly later:

**Approvers are included.** ADR-0018 removed an Approver's *standing* reach into candidate
data, and it reads at a glance like "an Approver has no business near a candidate, so keep them
off the list". It says the opposite. Panel membership is precisely how an excluded role reaches
one application, on purpose, by the same route a Hiring Manager from another department takes
(ADR-0017 §4). Filtering them out here would quietly delete that escape hatch, and it would do
so in a controller, far from the ADR that granted it.

**It is not department-scoped.** A panel routinely crosses departments — a Finance interviewer
on a Sales hire is the normal case, not the exception. Scoping the picker would make the
cross-department panel unbuildable while leaving the API that accepts it wide open.

**The enum is projected in memory.** EF Core 10 does not translate `enum.ToString()` into SQL.
`GET /api/users` does exactly that inside its `Select` and has never run against Postgres — see
the follow-up below.

## Consequences

- A Recruiter can build a panel by name. This is a prerequisite for Module 3's UI, not a
  convenience.
- Recruitment staff can enumerate active users and their roles. That is a real widening, and
  the judgement is that it is proportionate: this is an in-house TA tool whose users already
  read candidate PII daily, and the org chart is not the secret in the building. Email
  addresses stay behind `AdminOnly`.
- The name is `selectable`, not `panel`: the approval-chain builder wants the same shape, and a
  second copy under a Module 3 name is how the third one gets written.

### Follow-ups

- 🟡 **`GET /api/users` projects `u.Role.ToString()` inside the query.** If EF Core 10 cannot
  translate it, that endpoint throws against Postgres and only in-memory tests pass. Verify
  against a real database, and fix with the two-step pattern if confirmed.
- This change touches authorization and has **not been compiled or tested** — it was written in
  an environment with no .NET SDK and no container registry access. It needs a build, a test for
  the new policy boundary (a Recruiter gets 200 on `selectable` and 403 on `/api/users`), and a
  human review before it is considered done.
