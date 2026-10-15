# Module 1 — Job Requisition & Approval

**Status:** ⬜ Not started · **Priority:** Foundational — every job originates here.

## Purpose

A Hiring Manager requests a new headcount inside the system, and it routes through
the company's approval chain automatically. Replaces email/verbal approval requests.

## Features

### 1.1 Smart Requisition Form
Hiring Managers submit a request directly: position/title, salary budget, and the
role's requirements.

### 1.2 JD Template Library
Pre-saved **Job Description master templates** that can be pulled into a requisition,
so JDs are consistent and fast to produce.

### 1.3 Dynamic Approval Workflow
Approval routes automatically according to the **company's own org structure** —
e.g. Department Head → Finance → HR Manager. The chain is configurable per company,
not hard-coded.

### 1.4 Real-time Status Tracking
The requester sees, on a dashboard, exactly **whose desk the request is sitting on**
right now.

## Entities

- `Requisition` — requester, department, title, headcount, salary budget, JD, status
- `RequisitionApproval` — ordered steps: approver, sequence, decision, decided-at, comment
- `ApprovalChain` / `ApprovalChainStep` — per-company configurable template
- `JdTemplate` — reusable JD master content
- `Department` — org unit that owns the requisition

## Status vocabulary (implemented)

`Draft` → `PendingApproval` → `Approved` | `Rejected` · `Cancelled`
Per-step: `Waiting` · `Approved` · `Rejected`

`Approved` and `Cancelled` are **terminal** — nothing moves out of them. Approved work must not be
reopened silently, and a withdrawn request stays withdrawn.

`Rejected` is **not** terminal: see *Revise and resubmit* below.

### Revise and resubmit

A `Rejected` requisition can be sent back to `Draft` by **the person who raised it**, corrected, and
submitted again. Without this a rejection ends the thread — the requester must raise a brand-new
requisition, and the reviewer's reasoning is stranded on a dead record.

Resubmitting opens a **new round** of approvals. It does not reuse or rewrite the previous round:
"Finance rejected v1 on 12 Aug, because the headcount of 3 was not justified" stays readable
forever, beside the revised version it produced. This is the same principle cancellation already
follows — *rewriting the steps would fabricate decisions nobody made*.

Each round is **decided afresh from step 1**. If the requester changed the salary budget after
Finance rejected it, an earlier `Approved` was given to a different document; carrying it forward
would credit an approval nobody granted to the version now in flight.

Consequently `Draft` no longer means "never submitted" — it may mean "rejected and being revised".
The round history, not the status, tells you which.

### Seniority and skipping ahead

Steps are ordered, and **a later step outranks an earlier one**. An approver may approve every step
at or below their own position in one action, without waiting for the people below them.

So on a chain of Dept Head → Finance → HR, the HR Manager can close all three at once. The Dept
Head cannot: they may only approve their own step, because everything above them is more senior.

**This applies to approval only.** An approver may reject, but only their own step. Rejecting on
behalf of someone junior would end the request before that person ever saw it, which is precisely
the visibility this rule is meant to protect.

**The record always shows who actually acted.** A step closed by a senior reads as approved *and*
names the person who approved it alongside the person it was assigned to. The chain is a record of
what happened, not a record of what the template expected.

### Cancellation

A `Draft` or `PendingApproval` requisition can be **withdrawn** by the person who raised it,
or by a company-wide role (Admin / HrDirector). An **approver cannot cancel**: being asked
to approve something is not authority to withdraw it — the approver's tool is Reject, which
records a decision rather than erasing the request.

Cancelling **does not touch the approval steps**. A chain left half-decided is the honest
record of what happened ("cancelled while waiting on Finance"); rewriting the steps would
fabricate decisions nobody made. The approver's inbox therefore filters on requisition
status, not on the presence of a `Waiting` step.

## Open questions

- Can approval steps run in **parallel** (Finance + HR at once) or strictly sequential?
  *(Currently strictly sequential.)*
- ~~Should a `Draft` be **editable** before submission?~~ **Yes** — `PUT /api/requisitions/{id}`,
  Draft only. After submit it is frozen (409), because approvers must not be deciding on
  contents that can change underneath them.
- Can an approver **delegate** while on leave? Is there an escalation timeout?
  *(Partly answered: a **senior** approver can now close a junior's step, so an approver on leave no
  longer blocks the chain from above — see* Seniority and skipping ahead *. Delegating to a peer or
  a junior, and timing out an idle step, are both still open.)*
- Does an approved requisition auto-create the job posting, or does a recruiter publish it manually?
- Does a requisition have to draw against an approved **budget/headcount plan** (Module 6), and what happens if the plan is exhausted?
