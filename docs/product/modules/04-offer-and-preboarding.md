# Module 4 — Offer Management & Pre-boarding

**Status:** ⬜ Not started · **Priority:** Medium — after pipeline works end-to-end.
**Scope last revised:** 2026-08-18, from a sales requirement document
(`Module 4_Offer Management & Pre-boarding.pdf`). That document is the source for the
sub-module structure, the status vocabulary, the offer approval workflow and the HRMS
handoff below.

## Purpose

Produce, send, sign and track offers, then collect joining paperwork before day one, and
hand the new employee to the company's HRMS without re-typing anything.

## Data inflow

A candidate who passes interview in Module 3 moves to the **`Offer`** pipeline stage in
Module 2, which is what brings them into this module. Name, email, applied position and
department carry over from Module 2, and the **approved salary budget carries over from
Module 1** — the offer form is pre-populated, not re-entered.

## Sub-modules

The requirement specifies **three menu items**, not four features. This replaces the
earlier 4.1–4.4 split.

### 4.1 Offer Dashboard (overview list)

One list showing the state of every offer.

- **Columns:** Offer ID, Candidate Name, Position, Department, Offered Salary, Sent Date,
  Status. Statuses are colour-differentiated.
- **Filters:** Date Range, Status, Department, Position Title.
- **Actions:** `Create Offer`; `Remind Candidate` — sends an email reminder when an offer
  has been *viewed* but not signed.

### 4.2 Offer Generation & Approval

Offer letters are written **in the system**, not in Word.

- **Dynamic template selection** from templates stored in System Settings.
- **Form fields:**
  - Candidate Name, Position, Department — auto-filled
  - Base Salary, Allowances — manual entry
  - Joining Date
  - Probation Period (e.g. 3 months, 6 months)
  - Offer Expiry Date — **after this date the offer link stops working**
- **Budget check:** if the entered salary exceeds the budget approved in Module 1, the
  system raises a **warning** at entry time.
- **Internal approval workflow:** when the offer is over budget, or when policy requires
  it, `Send for Internal Approval` routes it to **HR Director / Finance** to approve or
  reject before it can be sent.
- **Send:** `Send to Candidate` emails a **secure link**.

### 4.3 Pre-boarding & E-Signature (candidate experience)

A candidate-facing web portal reached by secure link. Recruiters can review everything the
candidate submits.

- **E-signature:** the candidate opens the link, reads the full offer letter as a PDF, then
  `Accept & Sign` or `Decline`. Accepting captures a digital signature and flips the status
  to `Accepted` immediately.
- **Document collection:** after acceptance, a second step collects joining documents —
  **NRC (front and back), bank account details, education certificates, profile photo** (for
  the employee card). Which fields are **required is configurable in System Settings**.
- **Automated IT/Admin handoff:** once pre-boarding is complete, `Notify Departments` fires
  automatically — **IT** ("create the new email account, prepare a laptop") and **Admin**
  ("prepare a desk and employee card"), by system notification and email.

## Permissions

| Role | Can do |
|---|---|
| Recruiter | See the whole Offer Dashboard; create and delete offers; fill in and draft the offer letter; send to the candidate; review and approve uploaded documents, and email the candidate to re-submit when something is missing |
| Hiring Manager | **View-only** offer status (Accepted/Rejected) for candidates in their own department. **Salary can be hidden from them** via a setting |
| Approver (HR Director / Management) | Open, review and Approve/Reject offers that are over budget or otherwise require approval |
| Candidate (external) | Via the secure link only: accept or decline the offer, and upload documents |

## HRMS integration (final handoff)

On the new employee's **first day**, their personal details, salary, documents and
department data sync **via API** into the company's existing HRMS as a new Employee Profile,
so nothing is entered twice.

Named target systems: **QHRM, BetterHR, GlobalTA, CityHR**.

> ⚠️ Per [ADR-0007](../../decisions/ADR-0007-productization-and-addons.md), a *specific*
> external integration is an **extension point plus a paid add-on**, not core scope. Build
> one HRMS export contract and adapt per system; do not put four vendors' APIs in the core
> product, and do not fork per customer.

## Status vocabulary

**Changed by the 2026-08-18 requirement.** The previous proposal was
`Draft → Sent → Viewed → Signed / Declined / Expired`.

`Draft` → `Pending Approval` → `Sent` → `Viewed` → `Accepted` / `Rejected` / `Expired`

Three differences worth noting when implementing:

1. **`Pending Approval` is new**, and it is what makes 4.2's internal approval a real state
   rather than a side conversation.
2. `Signed` became **`Accepted`** and `Declined` became **`Rejected`**.
3. ⚠️ **`Rejected` now means two different things in the product** — an offer the candidate
   turned down, and a candidate rejected from the pipeline (`PipelineStatus.Rejected`).
   These are different enums with the same label. `StatusPill`'s vocabulary is deliberately
   the union of the backend enums with no free-form labels
   (`packages/ui/src/StatusPill.tsx`), so `OfferStatus` joins it as a fifth enum and the
   two `Rejected` values must not be conflated in code or in analytics.

## Entities

- `OfferTemplate` — letter template with merge fields
- `Offer` — application ref, salary/terms, probation period, generated document, status,
  expiry date
- `OfferApproval` — the internal approval step(s) for over-budget offers
- `OfferSignature` — signature evidence, signed-at, audit trail
- `PreboardingRequest`, `PreboardingDocument` — secure link, required-document checklist
- `DepartmentNotification` — which internal teams to alert, and whether the alert was sent
- `HrmsSyncRecord` — what was pushed, when, and the remote employee id

## Open questions

**Answered by the 2026-08-18 requirement** (previously open):

- ~~Does the offer need its own approval chain?~~ **Yes** — over-budget or policy-driven,
  routed to HR Director / Finance.
- ~~Document retention for candidates who decline?~~ Still governed by Module 7 retention,
  but the requirement confirms declined offers keep their record rather than being purged
  at decline time.

**Still open, and now more urgent:**

- **E-signature provider.** The requirement says "Digital Signature" but names no provider.
  Still a legally significant choice for Myanmar. In-house click-to-accept with a captured
  audit trail may satisfy it; that must be confirmed, not assumed.
- **Secure link security model** — expiring token alone, or OTP to the candidate's phone or
  email as well? The link now carries salary and collects NRC and bank details, so this is a
  higher-stakes decision than when it only showed a letter.
- **Does the offer approval reuse Module 1's approval-chain machinery?** It should — rounds,
  snapshotting and the "who actually acted" record already exist
  ([ADR-0023](../../decisions/ADR-0023-revise-and-resubmit-rounds.md),
  [ADR-0024](../../decisions/ADR-0024-senior-skip-ahead-approval.md)). Building a second,
  simpler approval mechanism beside the first is how the two drift apart.
- **Email sending does not exist yet.** `Remind Candidate`, `Send to Candidate` and the
  IT/Admin notifications all require an email sender; per
  `docs/status/FEATURE-STATUS.md` there is none in the codebase. This is the same gap that
  blocks Module 3's interview invitations, and it now blocks three features here.
- **Salary visibility for Hiring Managers** interacts with
  [ADR-0018](../../decisions/ADR-0018-approver-candidate-data-exclusion.md) and department
  scoping. Confirm whether hiding salary is a per-company setting, a per-role permission, or
  both.
