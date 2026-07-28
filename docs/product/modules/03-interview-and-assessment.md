# Module 3 — Interview & Assessment

**Status:** 🚧 3.3 + 3.4 built; 3.1/3.2 blocked on Module 7 · **Priority:** High — the
recruiter↔manager collaboration core.

Design decisions are recorded in
[ADR-0017](../../decisions/ADR-0017-interview-and-assessment.md).

## Purpose

Remove the scheduling back-and-forth and make interview evaluation consistent and
comparable across managers.

## Features

### 3.1 Smart Interview Scheduling ⬜ blocked
Connects to the **Hiring Manager's calendar** so free slots can be picked and booked easily.

> **Not built.** No calendar client exists in this codebase and Module 7 owns integrations.
> What *is* built is manual scheduling: a recruiter fixes the slot, mode and location/link.

### 3.2 Automated Invitations & Reminders ⬜ blocked
Email invitations and reminders sent automatically to both the **candidate** and the **manager**.

> **Not built.** No email sender exists. `NotificationLog` is deliberately absent until one
> does — see ADR-0017 follow-ups.

### 3.3 Standardized Scorecards ✅
Managers score and evaluate **directly in the system** during the interview, against a
standard scorecard — so candidates are comparable.

- Criteria come from a **department-level template with a per-posting override** (ADR-0017 §1)
- A submitted scorecard **snapshots its criteria**, so editing a template later cannot
  retroactively change what someone answered (§2)
- **Blind until you submit**: a panel member sees only their own scorecard until theirs is in (§3)

### 3.4 Collaborative Notes & @Mentions ✅
Recruiters and managers discuss **inside the system** using `@tagging`, instead of
side conversations in chat/email.

- Mentions are resolved to user ids **server-side**, never trusted from the client
- Note bodies are stored raw and escaped on output

## Entities

- `Interview` — job application, scheduled slot, mode, location/link, status ✅
- `InterviewParticipant` — interviewer/panel member; **grants narrow read access** to that one
  application, which is what makes cross-department panels possible (ADR-0017 §4) ✅
- `ScorecardTemplate`, `ScorecardCriterion` — the configurable criteria set ✅
- `Scorecard`, `ScorecardResponse` — per-interviewer evaluation ✅
- `Note`, `NoteMention` — collaborative comments with @mentions ✅
- `NotificationLog` — invitations/reminders sent ⬜ deferred with 3.2

## Resolved questions

| Question | Answer | Where |
|---|---|---|
| Scorecard criteria global or per job/department? | Department template, posting may override | ADR-0017 §1 |
| Can an interviewer see others' scores first? | No — blind until they submit their own | ADR-0017 §3 |
| Cross-department panel member access? | Participation grants read on that one application | ADR-0017 §4 |
| Does scheduling move the pipeline stage? | Yes, in the same transaction as the stage-history write | ADR-0017 §5 |

## Still open

- Calendar integration depth: read free/busy only, or **create the event** in the manager's calendar? (Module 7 covers M365/Google sync.)
- Does the candidate self-select a slot from proposed options, or does the recruiter fix the
  time? Currently the recruiter fixes it; deferred, not designed out.
