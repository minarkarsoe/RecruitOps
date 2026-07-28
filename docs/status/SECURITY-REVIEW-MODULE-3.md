# Security review — Module 3 (Interview & Assessment)

**Date:** 2026-07-28 · **Scope:** the three authorization surfaces Module 3 added
· **Reviewer brief:** `.claude/agents/security-reviewer.md`

Surfaces reviewed, as named in `NEXT-SESSION.md`:

1. `IApplicationAccess` / `ApplicationAccess` — participation as an access grant (ADR-0017 §4)
2. `ScorecardService.GetForInterviewAsync` — the blind-scoring filter (ADR-0017 §3)
3. `NoteService.ResolveMentionsAsync` — mentions as a disclosure channel (ADR-0017 §4)

Also read, because the rule under review reaches them: `InterviewService`, `PipelineService`,
`DepartmentAccess`, `CurrentUser`, `MentionParser`, `Policies`, `Program.cs` policy wiring,
`AppDbContext` filters and tenant stamping, and the Module 3 test suite.

---

## Summary

| # | Finding | Severity | Status |
|---|---|---|---|
| F1 | `@mention` resolves for an `Approver` — the case the code's own doc comment gives as the thing it prevents | 🔴 High | **Fixed** |
| F2 | An `Approver` reaches every application's notes, interviews, scorecards, pipeline and stage history, company-wide | 🔴 High | **Fixed** (ADR-0018) |
| F3 | The reach rule was written twice; the second copy would not have followed a fix to the first | 🟠 Medium | **Fixed** |
| F4 | Blind filter and `hiddenCount` | ✅ No finding | — |
| F5 | `CanWrite` doc and ADR-0017 §4 contradict shipped, tested behaviour | 🟡 Low | **Fixed** (docs) |
| F6 | Mention resolution loads every active user, then N+1s | 🔵 Perf | Open — logged in FEATURE-STATUS |

No findings on injection (all EF LINQ, no raw SQL or string interpolation into queries), XSS,
secrets, CORS, or tenant isolation. Details under "What held up" below.

---

## F1 — 🔴 A mention resolves for an Approver

**Where:** `NoteService.CanUserReachAsync`, the line `var scoped = user.Role is UserRole.HiringManager;`

The method's own XML doc, directly above it, states the threat:

> A handle only resolves if that user could reach this application themselves. Otherwise a
> mention becomes a disclosure channel: `"@finance.approver what do you think of this
> candidate"` would put a name and a judgement in front of someone with no business seeing
> either, and (again, with notifications) mail it to them.

The implementation tested for exactly one role and returned `true` for everything else, so
`@finance.approver` — the handle named in the comment — resolved and was recorded. The existing
test `A_Mention_Of_Someone_Who_Cannot_See_The_Application_Is_Not_Recorded` used
`@finance.manager`, a `HiringManager`, and passed throughout.

**Impact today:** a `NoteMention` row and a `<span class="mention">` naming a user who cannot
open the application. **Impact once Module 7 delivers notifications:** an email containing a
candidate's name and an assessment, sent to someone outside the department.

**Fix:** `CanUserReachAsync` is deleted. `IApplicationAccess.ResolveForUserAsync` answers the
question for a third party using the same implementation the caller path uses.
**Test:** `A_Mention_Of_An_Approver_Is_Not_Recorded`.

## F2 — 🔴 An Approver reaches every candidate in the company

**Where:** the interaction of `ICurrentUser.IsDepartmentScoped` (HiringManager only, per
ADR-0003), `DepartmentAccess.CanAccessAsync`, `ApplicationAccess` clause 1, and
`Policies.InternalUser` (which includes `Approver`).

An `Approver` is not department-scoped, so `CanAccessAsync` returned `true` for every
department, so `ApplicationAccess.ResolveAsync` returned `Kind = Department, CanWrite = true`
against **every job application in the company**. Reachable over HTTP with no participation in
anything:

| Endpoint | Policy | What an Approver got |
|---|---|---|
| `GET /api/applications/{id}/notes` | InternalUser | any candidate's debrief thread |
| `POST /api/applications/{id}/notes` | InternalUser | write into it |
| `GET /api/applications/{id}/interviews` | InternalUser | every round |
| `GET /api/interviews/{id}` | InternalUser | panel, agenda, who has submitted |
| `GET /api/interviews/{id}/scorecards` | InternalUser | not a participant → **not blinded** → every submitted evaluation |
| `GET /api/applications/{id}/history` | InternalUser | full stage history |
| `GET /api/jobpostings/{id}/pipeline` | InternalUser | any department's pipeline board |

The Module 3 write endpoints (`schedule`, `reschedule`, `panel`, `cancel`, `complete`) are
`RecruitmentStaff` and were never reachable. `POST .../stage` likewise. Notes were.

Nothing here was a bug in the ordinary sense — every layer did exactly what it documented. The
aggregate was a privilege nobody granted on purpose, and it traces to ADR-0003 answering a
question about **requisitions** ("an approver must see what they're approving") that every
service then applied to **candidates**.

**Fix:** [ADR-0018](../decisions/ADR-0018-approver-candidate-data-exclusion.md) splits the two
questions. `RoleScope.IsDepartmentScoped` is unchanged; `RoleScope.IsExcludedFromCandidateData`
is new and covers `Approver`. Applied as clause 0 in `ApplicationAccess`, and through one
private helper in `PipelineService`. An Approver still reaches an individual application by
sitting on its panel — a deliberate, visible, expiring grant.
**Tests:** `ApproverReachTests` (7 cases, including that requisition approval still works).

## F3 — 🟠 The reach rule was written twice

`NoteService.CanUserReachAsync` re-derived what `ApplicationAccess` does, and did it against a
**role literal** rather than the shared predicate — the only place in the codebase that did.
Fixing F2 in `IsDepartmentScoped` would not have moved it. This is the failure mode
`IApplicationAccess`'s own doc comment describes ("a guard added to two of three sibling
methods"), reproduced one layer up.

**Fix:** one predicate pair in `Domain/RoleScope.cs`; `CurrentUser` and `ApplicationAccess`
both call it, and nothing else names a role. `ResolveForUserAsync` is now the single
third-party path.

## F4 — ✅ The blind filter is sound

Checked and found correct, with no change made:

- `readable` = submitted ∪ own. Another author's **draft never enters**, including for Admin
  and HrDirector — a draft is nobody's opinion yet.
- `blinded` keys on **participation**, not reach. A recruiter not on the panel is not anchoring
  an assessment they aren't writing; a recruiter who *is* on the panel is blinded like anyone.
- `hiddenCount` = other people's *submitted* count. It carries no content, and it is strictly
  less than `GET /api/interviews/{id}` already returns — `InterviewParticipantDto.HasSubmitted`
  names who has submitted. No leak; render it as a state, as `NEXT-SESSION` says.
- Submit is one-way and is checked **before** the draft is rewritten.
- Answers are rebuilt from the template, so an unknown criterion id is dropped, not persisted.
- All three of get/save/submit go through `LoadForParticipantAsync` — one door, no sibling gap.

## F5 — 🟡 Two docs contradicted the code

`ApplicationReach.CanWrite` said a panel member "cannot reschedule it, move its stage, **or
touch anything else**"; ADR-0017 §4 said participation confers "**no writes**". But
`NoteService.CreateAsync` deliberately does not check `CanWrite`, and
`A_Panel_Member_Can_Read_And_Join_The_Thread` pins that a participant *can* post. The behaviour
is right — debriefing in the thread is the job a panel member was added to do. The documents
were wrong, and left alone the next person adding a note-adjacent write would have guessed.

**Fix:** `CanWrite` now says it gates writes to the *process*; ADR-0017 §4 carries a clarifying
note; `NoteService.CreateAsync` carries a comment explaining why reach, not `CanWrite`, is the
right gate there.

## F6 — 🔵 Not security, but on a hot path

`ResolveMentionsAsync` loads **every active user in the tenant** into memory on every note POST,
then runs 2–3 queries per matched user. `InterviewService.ListForApplicationAsync` calls
`MapAsync` per interview (4 queries each). Neither is exploitable; both get expensive at a few
thousand users. Logged in `FEATURE-STATUS.md`, not fixed here.

---

## What held up

- **Injection** — every query is EF LINQ. No raw SQL, no string interpolation into a query.
- **XSS** — `MentionParser.ToSafeHtml` escapes `& < > " '` across all user text and only then
  inserts markup it generated itself, so no body text can become an element. The handle regex
  is bounded (`{0,62}`, not `*`) with a 200 ms timeout — the catastrophic-backtracking shape was
  already considered.
- **Mention forgery** — handles are parsed from the body, never taken from the request.
- **Tenant isolation** — `Interview`, `InterviewParticipant`, `Scorecard`, `ScorecardResponse`,
  `ScorecardTemplate`, `ScorecardCriterion`, `Note`, `NoteMention` all have query filters, and
  `SaveChangesAsync` stamps `TenantId` on added rows, so `NoteService` not setting it by hand is
  safe.
- **404 not 403** — `ResolveAsync` returns `null` for "no such application" and "not yours"
  alike, and every caller translates both to 404. Existence is not leaked.
- **Cross-application ids** — a note pinned to an interview is checked to belong to *this*
  application; a scorecard answer against a criterion outside the template is dropped.
- **Panel removal** — an interviewer who has started scoring cannot be silently dropped.

---

## ⚠️ Not verified by a run

**The suite was not executed for this review.** The environment has no .NET SDK and no Docker,
and the SDK download is blocked by the network allowlist, so neither `dotnet test` nor
`docker build --target test ./backend` could run.

Everything above is from reading the source. The fixes compile in principle and follow the
patterns already in the repo, but **they are unbuilt and the 8 new tests are unrun.** Anyone
picking this up should run the suite first and treat a failure as expected work, not a surprise.

For the same reason `FEATURE-STATUS.md`'s test figures stay marked as computed rather than
observed. Counted from source: **109** `[Fact]` in the API suite (now 117 with this change),
and **26** `[Fact]`/`[Theory]` plus 16 `[InlineData]` rows in the domain suite.
