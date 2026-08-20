# ADR-0026 — Outbound delivery and background jobs: one durable queue, SMTP as the floor

- **Date:** 2026-08-18
- **Status:** ✅ **Accepted 2026-08-18.** The §3 dependency question was put to the product owner
  and answered: **hand-rolled, no new package.**
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

> **Implemented 2026-08-20 — and the choice of client narrows two of those three tiles.**
> `SmtpEmailSender` is built on `System.Net.Mail.SmtpClient`, honouring §3's "no new package"
> decision. That covers the floor this section actually specifies, and it brought one unplanned
> benefit: `SmtpDeliveryMethod.SpecifiedPickupDirectory` gives local development a path that
> renders and writes the *real* message with no server and no network — a development mode that
> fabricates nothing, unlike the AI fallback ADR-0008 had to fence off.
>
> What it cannot do, recorded here rather than left to be discovered by the first customer:
> **no XOAUTH2**, so Microsoft 365 and Google Workspace cannot be authenticated against at all;
> and **no implicit TLS on port 465**, so a relay offering only that is unusable. Both are
> MailKit's to solve. That is a package decision on the same axis this ADR already took once —
> take it deliberately, when a customer needs one of those tiles, not as a quiet dependency
> added under a bug fix.

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

1. claims due `OutboundMessage` rows (`Pending`, `NextAttemptAt <= now`) by pushing
   `NextAttemptAt` forward by a visibility timeout and incrementing `Attempts`;

> **Amendment, 2026-08-20 — the claim is EF-level, not `FOR UPDATE SKIP LOCKED`.**
> This section originally specified `UPDATE … RETURNING` under `FOR UPDATE SKIP LOCKED`, so that
> claiming would stay atomic "even if the single-instance assumption is ever broken". It was
> implemented as a read-then-update through EF instead. Recorded rather than quietly changed,
> because it narrows a guarantee the ADR made:
>
> - **What is unchanged:** crash safety. A row is never marked in-flight, only pushed into the
>   future, so a process that dies mid-send leaves work that becomes due again on its own.
> - **What is weaker:** with **two** workers against one database, both could read the same due
>   batch before either saves, and the message would be sent twice. `SKIP LOCKED` would have
>   made that impossible.
> - **Why it was accepted:** ADR-0004 ships one instance per company, so there is no second
>   worker. Raw provider-specific SQL would also mean the test suite exercises a different claim
>   path from production — and the in-memory suite is where this worker's behaviour is actually
>   pinned.
> - **The trigger to change it:** the moment a customer is given two app replicas. That is
>   already the documented trigger for `LoginThrottle` (ADR-0016) and for the bulk-upload
>   dictionary; this is the **third** in-process assumption riding on it, and they should be
>   audited together rather than one at a time.
2. renders and sends via `IEmailSender` or a channel adapter;
3. writes the outcome, and on failure sets `NextAttemptAt` by exponential backoff up to a cap,
   then `Failed`.

Bulk CV batches move onto the same table shape — a persisted batch row plus per-file rows, with
the uploaded bytes going to object storage (ADR-0013) rather than to RAM.

**Hangfire was the alternative and was rejected**, on two points that are specific to this
product rather than general preferences:

1. **We need `OutboundMessage` either way, so Hangfire adds a second source of truth.**
   "Was this candidate actually told?" is a product question with a UI — the delivery log in
   `design/internal/channels.html` — not an ops question. That table exists whether or not a job
   framework does. Adding Hangfire means `OutboundMessage` *and* `hangfire.job` both describe the
   same send, and they will disagree eventually.
2. **`Enqueue` would break the transactional guarantee that is the whole point.** Hangfire writes
   to its own storage in its own transaction, so an offer can commit while its enqueue fails —
   producing an offer that is sent in the database and unsent in reality, which is precisely the
   failure this ADR exists to remove. One `DbContext` and one `SaveChanges` makes it atomic by
   construction.

Secondary, and still real: Hangfire's schema is created by Hangfire rather than by EF, putting
two migration systems in one database; its jobs are serialized *method calls*, so renaming a
method breaks work already queued; and its dashboard displays job arguments — for us, candidate
names and offered salaries — which becomes one more authenticated surface to secure inside a
bank's on-premise install.

**What we are accepting by hand-rolling, stated plainly so nobody is surprised:** retry
correctness, backoff, poison-message handling and observability are now ours to get right, and
Hangfire's are battle-tested. The scope that makes this a fair trade is one queue on one
instance. **Revisit if any of these becomes true:** many job types with real scheduling
complexity, a customer given two app replicas, or an operations requirement that would make us
build a dashboard anyway.

### 4. A job carries its own tenant, and the query filters stay ON

**This is the part with teeth, and it is independent of §3 — Hangfire would have needed it too.**

`CurrentTenant` reads the tenant from the HTTP request:

```csharp
var value = _http.HttpContext?.User.FindFirstValue(AppClaims.TenantId);
return Guid.TryParse(value, out var id) ? id : Guid.Empty;
```

A background job has no `HttpContext`. So `TenantId` is `Guid.Empty`, and **every one of the
twenty-odd global query filters in `AppDbContext` matches nothing.** Worse, `AppDbContext` also
stamps `scoped.TenantId = _tenant.TenantId` on insert — so a job that writes would write rows
belonging to tenant `Guid.Empty`.

The repo already has two places that hit this and one way of dealing with it —
`PublicJobService` and `BulkResumeService` both call `IgnoreQueryFilters()` and carry the tenant
by hand:

```csharp
.IgnoreQueryFilters()
.Where(c => c.TenantId == batchState.TenantId ...)
```

**We are not extending that pattern to the job runner.** It works, and it is exactly the shape
ADR-0003 warns about — a filter "applied explicitly and therefore possible to forget". One more
handler that forgets it reads another company's data. With one job type that is a code review;
with eight it is an incident.

Instead: **the worker establishes the tenant for the scope, and handler code looks like request
code.**

- `ICurrentTenant` gains a background-capable implementation: it returns the HTTP claim when
  there is a request, and otherwise a value held on a **DI-scoped, settable** tenant holder.
- For each claimed row the worker creates a DI scope, sets the holder from
  `OutboundMessage.TenantId` **before resolving `AppDbContext` or any service**, then dispatches.
- Inside the handler, query filters work normally and `TenantId` is stamped correctly on write.
  **No handler calls `IgnoreQueryFilters()`.** If one needs to, that is the signal something is
  wrong, not a pattern to copy.

`IgnoreQueryFilters()` stays confined to `PublicJobService`, where it is genuinely unavoidable
because the public token is what establishes the tenant in the first place.

**Identity is a separate problem with a separate answer.** `ICurrentUser.UserId` is also null in
a job. Anything a job writes that records an actor must attribute it to an explicit **system
actor**, not to null and not to the user who happened to trigger it hours earlier. And a job must
never call a department-scoped service path: `IDepartmentAccess` and `IApplicationAccess` answer
"may *this user* reach it", and there is no user. A job is not a privileged user — it is not a
user at all, and code that treats absence-of-user as permission is how ADR-0018's hole was
opened.

### 5. Scheduling is a due-time on a row, not a cron daemon

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
- **`ICurrentTenant` gains a second implementation, and that is a change to a security-critical
  seam.** Today it is a thin read of a claim; after this it can be *set*. A settable tenant is
  worth exactly one code review of its own — the failure mode is a scope that keeps a previous
  job's tenant, which reads as "works fine" until two companies' data cross. Assert it: a test
  that runs two queued messages for two tenants through the worker and checks each handler saw
  only its own rows.
- **Retry correctness, backoff, poison-message handling and observability are now ours.** This is
  the cost of §3 and it is not zero. Budget for it rather than discovering it: a message that
  fails forever must stop, be visible, and not occupy the queue.
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
- **Hangfire / Quartz.** See §3 — considered seriously and rejected on two product-specific
  grounds, not on general preference.
- **An external cron container invoking an endpoint.** Works hosted, and is one more moving part
  an on-premise customer must install and monitor. Rejected for the same reason as the provider
  default.

## Open questions

*(The dependency question that previously headed this list was answered on 2026-08-18 — see §3.
None of the remaining ones block the start of work.)*

- **Where do SMTP credentials live?** Inherited from Module 7's unresolved key-vault-vs-column
  question. A hosted install has a vault; an offline install may not.
- **Who is the sender?** A single company-wide `noreply@`, or the acting recruiter's own address
  via Microsoft 365 (Module 7.3), which is better for replies and needs delegated permission.
  Module 4's candidate-facing mail and Module 3's interview invitations may want different
  answers.
  > **Now live rather than hypothetical (2026-08-20).** Invitations are going out from one
  > company-wide `Smtp:FromAddress`, and the body invites the candidate to *reply* if the time
  > does not suit them — into a mailbox nobody may be reading. Either change the copy or answer
  > this question; the current pair is a promise the product does not keep.

- **Nothing renders `OutboundMessage`.** Added 2026-08-20, and it is the larger half of §2 still
  missing. The table answers "was this candidate told?" and no screen asks it, so a `Failed`
  invitation — wrong address, dead relay — is recorded faithfully and seen by nobody.
  `design/internal/channels.html` draws the log already; it needs an endpoint and a page.

- **Candidate-facing mail is English only.** `design/internal/postings.html` offers a per-posting
  language choice (Burmese / English / Both) with no backing field, so there is nothing to render
  from. A Yangon field role advertised in Burmese gets its interview invitation in English.
- **Retention on `OutboundMessage.Payload`.** It will contain a candidate's name and an offered
  salary. It is therefore in scope for the Module 7.4 retention policy and must not be the one
  table that quietly keeps everything.
- **What does a `Failed` message do to the recruiter's screen?** The channels design shows it in
  a delivery log; whether it also raises something in the app is a product decision.
