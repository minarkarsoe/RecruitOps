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

> **Amendment, 2026-07-28 — policies must be declared per action, never on the class.**
> The first implementation left `[Authorize(Policy = AdminOnly)]` on `UsersController` and put
> `[Authorize(Policy = RecruitmentStaff)]` on the new action, expecting the action to opt
> *down*. **ASP.NET Core authorization attributes are additive**: the action-level attribute is
> evaluated *in addition to* the class-level one, not instead of it. The effective requirement
> was `AdminOnly` **AND** `RecruitmentStaff` — so only an Admin could call `selectable`, and a
> Recruiter got 403.
>
> The endpoint therefore shipped reproducing the very condition this ADR was written to remove.
> It was caught by `UserDirectoryTests` on its first run (CI #4: 8 of 11 cases failed, exactly
> the 8 that require a 200). The class now carries a bare `[Authorize]` and `AdminOnly` sits on
> `Get`. **There is no way to widen a policy from an action** — if a future endpoint here needs
> a weaker requirement, give it its own attribute rather than reintroducing a class-level one.

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
- This change touches authorization and was written in an environment with no .NET SDK and no
  container registry access. Status of the three things it needed:
  - **A build** — ✅ CI green 2026-07-28.
  - **A policy-boundary test** — ✅ `UserDirectoryTests.cs`, 11 cases (2026-07-28), including
    the 200-on-`selectable` / 403-on-`/api/users` pair asserted in one test. ✅ Green in CI
    (169/169 backend). On their **first** run 8 of the 11 failed, and correctly: the class-level
    `AdminOnly` was being ANDed with the action-level `RecruitmentStaff`, so the endpoint was
    reachable only by an Admin — the exact condition this ADR exists to remove. Fixed with a
    bare `[Authorize]` on the class and `AdminOnly` declared on `Get`.
  - **A human review** — ✅ done 2026-07-29.

  **This ADR is now fully realised.** The remaining 🟡 above (`enum.ToString()`) is a separate
  EF translation question, not an authorization one.

## Amendment, 2026-10-17 — the full directory follows `users:read`, and Recruiters do not hold it

Found by walking every menu as every role against a running install. Two things contradicted the
decision above:

1. **Recruiters could read every email anyway.** The seed gave Recruiter
   `permission:users:users:read`, and `GET /api/users/{id}` is guarded by exactly that. A Recruiter
   took ids from the email-free picker and asked for each one: **every user's email, permissions
   and SuperAdmin flag, 3 of 3** (measured). The picker's whole reason to exist was undone one call
   later. The same permission also put a "Users" item in their menu whose list then returned 403.
2. **HR Directors hold `users:read` too, but the list was `AdminOnly`** — and the test above pinned
   them to the Recruiter's side of the split. The seed (and their menu) said one thing, the
   endpoint another.

**Decision (product owner, 2026-10-17):** the HR Director reads the directory, read-only. So:

- **Recruiter no longer holds `users:read`.** The picker is their way to name a panel, and nothing
  else in the product needs more. Startup seeding now also *removes* permissions a system role no
  longer has in its seed — system roles are immutable through the API, so the seed is their
  definition — and records the removal in the audit log as `System (startup)`.
- **`GET /api/users` is guarded by `users:read`**, like `GET /api/users/{id}`. Admin and HR Director
  hold it by default; a custom role gets it only from someone who holds it (grantor ceiling,
  ADR-0021). Creating, editing and deactivating stay on their own permissions, which the HR
  Director does not hold — the page hides those controls.

The reasoning above still stands, unchanged in kind: a wider audience gets the narrower payload.
What changed is *who* counts as the wider audience — RecruitmentStaff minus HR, not including it.

### Follow-up, 2026-10-19 — the other two places an address leaked

The security review of the change above found the same disclosure by two more routes:

- **The interview panel.** `InterviewParticipantDto` carried each member's email, and whoever sets
  a panel names it from the picker — so a Recruiter could schedule a round with every selectable
  id and read every address in the response. The panel record now carries **no email** (the SPA
  never showed it), and a panel holds at most 20 people (`PanelLimits.MaxParticipants`), so it
  cannot be a way to address the company in one request.
- **The department roster.** `GET /api/departments/{id}/members` lists every active user, on
  `settings:read`. It now includes addresses only for a caller who also holds `users:read` — the
  directory's permission. Admin and HR Director hold both, so their screen is unchanged.

The rule this ADR now stands for: **a colleague's email address is `users:read`.** The panel, the
roster, the directory and — since the second review (F2b) — the user edit endpoints, which answer
with the full record, all need it. **One stated exception: the audit log** (`system:audit`), whose
sign-in and user-created entries name the address involved; it is evidence, and redacting it would
make a failed sign-in unattributable. Admin and HR Director hold both permissions by default, so
the exception matters only for a custom role given `system:audit` without `users:read`.
