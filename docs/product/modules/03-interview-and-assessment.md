# Module 3 — Interview & Assessment

**Status:** 🚧 3.2 (candidate invitations) + 3.3 + 3.4 built; 3.1 still blocked on Module 7 ·
**Priority:** High — the recruiter↔manager collaboration core.

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

### 3.2 Automated Invitations & Reminders 🚧 candidate invitations built
Email invitations and reminders sent automatically to both the **candidate** and the **manager**.

> **Built 2026-08-20 — the candidate half.** Scheduling a round writes the invitation in the same
> transaction as the interview (ADR-0026 §2), and the delivery worker sends it over SMTP. The body
> is rendered at send time, not at enqueue, which is what gives the rest of the behaviour for
> free: rescheduling before it goes changes the time in the message that is already queued;
> rescheduling after it has gone sends a second message that reads as a change; cancelling the
> round suppresses a queued invitation; and a slot that has already passed is suppressed rather
> than sent.
>
> The interview time is rendered in `Company.TimeZoneId` — added for this, because Postgres
> normalises `timestamptz` to UTC and the recruiter's *o'clock* does not survive the round-trip.
>
> **Still missing, and each is a separate piece of work:**
> - **The panel is not emailed.** Interviewers see the round in the app and nowhere else.
> - **No reminders.** The recurrence mechanism exists (`ScheduledJob`), nothing uses it.
> - **`NotificationLog` was never needed.** `OutboundMessage` (ADR-0026) is that table, and it
>   is shared across every module rather than being Module 3's own. **But no screen reads it**,
>   so a failed invitation is recorded and shown to nobody — `design/internal/channels.html`
>   draws the log that closes this.
> - **English only.** The per-posting language choice in `design/internal/postings.html` has no
>   backing field.

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
- ~~`NotificationLog` — invitations/reminders sent~~ — **superseded**: `OutboundMessage`
  (ADR-0026) is the delivery record, shared by every module rather than owned by this one ✅

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
