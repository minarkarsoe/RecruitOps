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

`Approved`, `Rejected` and `Cancelled` are **terminal** — nothing moves out of them.

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
- Does an approved requisition auto-create the job posting, or does a recruiter publish it manually?
- Does a requisition have to draw against an approved **budget/headcount plan** (Module 6), and what happens if the plan is exhausted?
