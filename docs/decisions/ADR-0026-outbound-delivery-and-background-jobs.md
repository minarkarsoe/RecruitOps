# ADR-0026 — Outbound delivery and background jobs: one durable queue, SMTP as the floor

- **Date:** 2026-08-18
- **Status:** Proposed — **needs a decision on the dependency question in §3 before work starts**
- **Scopes:** Module 3 (3.1, 3.2), Module 4 (4.1, 4.2, 4.3), Module 5 (5.3), Module 2 (2.3), Module 8
- **Constrained by:** [ADR-0004](ADR-0004-single-tenant-deployment.md) — one instance + one
  database per company, on our infrastructure **or the customer's**
- **Follows the pattern set by:** [ADR-0013](ADR-0013-infrastructure-and-storage.md) (abstraction
  with hosted and on-premise implementations), [ADR-0008](ADR-0008-document-extraction-and-ai-profiling.md)
  (a local-first default that must never regress)
- **Amends in spirit:** [ADR-0016](ADR-0016-login-brute-force-protection.md), which accepted
  in-process state on the same single-instance grounds and recorded the condition that would
  end it

## Context

Four modules are blocked on one absent capability, and it has been recorded as four separate
gaps for three weeks. `grep` across `backend/src` for `SmtpClient|IEmailSender|MailKit|SendGrid`
returns nothing, and for `BackgroundService|IHostedService|Hangfire|Quartz` returns nothing.

| Blocked | Needs |
|---|---|
| 3.1 / 3.2 — interview invitations and calendar sync | send mail, on a schedule |
| 4.1 / 4.2 — send an offer, remind a candidate | send mail, later, and know whether it arrived |
| 4.3 — the IT / Admin pre-boarding handoff | send mail on a trigger, and record that it fired |
| 5.3 — scheduled report delivery | run on a cron, then send mail with an attachment |
| 2.3 — bulk CV upload, 50 files | run work outside the request, durably |
| 8 — candidate status notifications | send to a channel adapter, and record delivery |

Treating these as six features produces six half-mechanisms. They are one: **something happens,
work must be done outside the request that caused it, and somebody needs to know whether it
actually happened.**

### What exists today is a warning, not a foundation

`BulkResumeService` already solves its half of this, and the way it does so is the argument for
this ADR:

```csharp
private static readonly ConcurrentDictionary<Guid, BatchStateHolder> Batches = new();
...
Batches[batchId] = batchState;
_ = Task.Run(async () => await ProcessBatchAsync(batchId));
```

The batch — including the **raw uploaded file bytes** — lives in a static dictionary and is
never written to the database. Consequences, none of them theoretical:

- **A restart loses the batch entirely.** Not "the status goes stale" — the entry is gone, so
  `GetBatchStatusAsync` returns null and the recruiter's 50 files return a 404. They have no
  way to know whether any candidate was created.
- **The files sit in RAM for the whole batch.** Fifty CVs, several MB each, per concurrent
  upload, on a server sized by a guide that does not account for it.
- **An exception inside `Task.Run` is unobserved.** No handler, no log, no retry.
- **Two replicas would not see each other's batches**, which is the same trap ADR-0016 recorded
  for `LoginThrottle` and is now present twice.

ADR-0008 required bulk upload to be asynchronous. This is the *shape* of asynchronous, not the
thing.

## Decision

### 1. Email goes behind `IEmailSender`, and SMTP is the only implementation we promise

One interface in Application, implementations in Infrastructure. **Plain SMTP is the required
one**; anything else is optional and additive.

This is not the usual choice — the usual choice is a transactional API provider — and the reason
is ADR-0004. Data sovereignty is a headline value proposition, so on-premise installs exist, and
some of them are banks whose application servers have **no outbound internet at all**. Such a
customer has an internal mail relay and nothing else. A product whose only send path is
`api.sendgrid.com` simply does not deliver mail for them, and it fails at the worst moment: the
offer was "sent" and the candidate never heard.

⇒ SMTP is the floor because it is the only transport that works in **every** deployment we sell.
A hosted install may configure SES or SendGrid as an adapter for deliverability and bounce
handling; nothing in the product may depend on that adapter existing.

`design/internal/settings-integrations.html` already renders this: Microsoft 365, Google
Workspace and **Plain SMTP** as a first-class option rather than a hidden advanced setting.

### 2. Nothing is fire-and-forget: a transactional outbox

Every outbound message is **written to the database in the same transaction as the thing that
caused it**, then sent by the worker. Not sent inline, and never sent without a row.

New entity, roughly:

```
OutboundMessage
  Id, TenantId
  Kind            (InterviewInvitation | OfferSent | OfferReminder | PreboardingHandoff | ScheduledReport | ChannelNotification)
  Recipient       (address or channel handle)
  SubjectRef      (the entity it is about — offer id, interview id)
  Payload         (JSONB — rendered at send time, not at enqueue time)
  Status          (Pending | Sent | Failed | Suppressed)
  Attempts, NextAttemptAt, LastError
  CreatedAt, SentAt
```

The reason this is a row rather than a call is Module 4 and Module 8, where **the recruiter's
next action depends on whether the candidate was actually told.** Silence is the failure mode
that costs a hire. `design/internal/channels.html` already draws the delivery log this table
feeds, including the two states that matter and are not errors: `Failed — outside the 24-hour
messaging window, she was not told, send by email instead`, and `Not sent — opted out`.

`Suppressed` is a first-class status for exactly that second case. An opt-out is a correct
outcome, not a failure, and the log must not colour it red.

### 3. One in-process `BackgroundService`, polling the database — **not** an external scheduler

ADR-0004 gives one instance and one database per company. That removes the problem distributed
job frameworks exist to solve, so buying one buys mostly its failure modes.

The worker is a single `BackgroundService` registered in `Api`, which on a fixed interval:

1. claims due `OutboundMessage` rows (`Pending`, `NextAttemptAt <= now`) with
   `UPDATE ... RETURNING` under `FOR UPDATE SKIP LOCKED`, so claiming is atomic even if the
   single-instance assumption is ever broken;
2. renders and sends via `IEmailSender` or a channel adapter;
3. writes the outcome, and on failure sets `NextAttemptAt` by exponential backoff up to a cap,
   then `Failed`.

Bulk CV batches move onto the same table shape — a persisted batch row plus per-file rows, with
the uploaded bytes going to object storage (ADR-0013) rather than to RAM.

> ⚠️ **This is the part that needs a human decision, per CLAUDE.md's rule on new dependencies.**
> The alternative is **Hangfire**, which brings a durable queue, retries, scheduling and a
> dashboard for roughly zero code — and brings its own schema, its own dashboard to secure, and
> a dependency inside an on-premise install we cannot patch on the customer's behalf. The
> hand-rolled option adds no package and is perhaps 200 lines, but every one of retries,
> backoff, claim-safety and observability becomes ours to get right.
>
> **Recommended: hand-rolled**, because the scope is one queue on one instance and the security
> surface of a job dashboard in a bank install is a real cost. But this is a judgement call with
> a defensible opposite, and it is not mine to make alone.

### 4. Scheduling is a due-time on a row, not a cron daemon

Module 5's scheduled reports and Module 4's reminders both reduce to "insert a row with a
`NextAttemptAt` in the future". Recurrence (a weekly report) is a small `ScheduledJob` table the
same worker reads to enqueue the next `OutboundMessage`. No second mechanism, no cron container,
nothing that only exists in the hosted deployment.

## Consequences

- **`BulkResumeService` must be rewritten**, not extended. Its static dictionary is the thing
  this ADR replaces; leaving it in place means two job mechanisms, which is the outcome the ADR
  exists to prevent.
- **New entities and a migration** — `OutboundMessage`, `ScheduledJob`, and persisted bulk-batch
  rows. Propose the migration; a human applies it (CLAUDE.md).
- **Every on-premise install now needs SMTP configuration**, and that belongs in
  `docs/architecture/server-sizing-guide.md` and the deployment runbook as a prerequisite, not a
  post-install nicety. An install without it is an install where offers cannot be sent.
- **Credentials for SMTP and any API provider are secrets**, and where they live is still open
  under Module 7 — key vault versus encrypted column. This ADR does not settle it, and
  `design/internal/settings-integrations.html` marks that field as "do not build until decided".
- **The single-instance assumption is now load-bearing in a third place** (after `LoginThrottle`
  and the bulk dictionary). `SKIP LOCKED` means the queue itself survives a second replica, but
  **write it down here as ADR-0016 did**: if a customer is ever given two app replicas, audit
  every in-process assumption before scaling, not after.
- **Module 5's metrics do not change**, but its *delivery* does — a scheduled report that fails
  to send must be visible somewhere a human looks, not only in a log file.

## Alternatives considered

- **A transactional API provider (SendGrid / SES) as the default, SMTP as a fallback.** The
  common industry choice, and better for deliverability and bounce handling. Rejected as the
  *default* because it inverts which deployments are first-class: it makes the air-gapped
  on-premise bank — the customer ADR-0004 was written for — the degraded case. Available as an
  adapter.
- **Send inline, in the request.** Cheapest to build and wrong immediately: a slow SMTP server
  makes approving an offer feel broken, and a failure after commit leaves the offer sent in the
  database and unsent in reality, with nothing recording the difference.
- **Keep `Task.Run`, add try/catch and logging.** Cheap, and does not fix the thing that
  matters: a restart still loses the work, and nothing is durable enough to retry or to answer
  "was this person told".
- **Hangfire / Quartz.** See §3 — a live option, deliberately left open rather than decided
  silently.
- **An external cron container invoking an endpoint.** Works hosted, and is one more moving part
  an on-premise customer must install and monitor. Rejected for the same reason as the provider
  default.

## Open questions

- **Hangfire or hand-rolled** (§3). Blocks the start of work.
- **Where do SMTP credentials live?** Inherited from Module 7's unresolved key-vault-vs-column
  question. A hosted install has a vault; an offline install may not.
- **Who is the sender?** A single company-wide `noreply@`, or the acting recruiter's own address
  via Microsoft 365 (Module 7.3), which is better for replies and needs delegated permission.
  Module 4's candidate-facing mail and Module 3's interview invitations may want different
  answers.
- **Retention on `OutboundMessage.Payload`.** It will contain a candidate's name and an offered
  salary. It is therefore in scope for the Module 7.4 retention policy and must not be the one
  table that quietly keeps everything.
- **What does a `Failed` message do to the recruiter's screen?** The channels design shows it in
  a delivery log; whether it also raises something in the app is a product decision.
