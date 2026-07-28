# Module 6 — Strategic Recruitment Planning & Budgeting

**Status:** ⬜ Not started · **Priority:** Medium-High — this is the differentiator for HR Directors.

## Purpose

Plan next year's hiring and money up front, get it approved, then track spend against
it in real time. This is what replaces the agency model's contract/tier concept.

## Features

### 6.1 Yearly Budgeting & Headcount Planning
The Recruitment Team drafts and submits, to Management, the **recruitment budget** and
**per-department headcount plan** for the coming year, aligned to business strategy.

### 6.2 Approval Flow for Planning
Management **approves the submitted budget and plan step by step** inside the system.

### 6.3 Budget Tracking
Watch, in real time, **how much of the approved budget has already been spent** across
recruitment activities.

## Entities

- `RecruitmentPlan` — fiscal year, company, status
- `HeadcountPlan` — per department: planned headcount, budgeted salary
- `BudgetLine` — planned recruitment spend by category (job ads, agency fees, events, tooling)
- `BudgetSpend` — actual spend, linked to a requisition/job where applicable
- `PlanApproval` — reuses the approval-chain pattern from Module 1

## Design notes

- The approval-chain mechanism is **the same shape as Module 1**. Build it once as a
  reusable component and apply it to both requisitions and plans.
- Linking `Requisition` → `HeadcountPlan` is what makes "are we over headcount?"
  answerable. Decide this relationship early (see Module 1 open questions).

## Open questions

- Fiscal year definition — calendar year, or company-configurable start month?
- Is budget tracked in one currency, or does multi-currency matter?
- Can a plan be **revised mid-year** (re-forecast), and does that need re-approval?
- What exactly counts as recruitment "spend" — is it entered manually, or pulled from an external finance system?
