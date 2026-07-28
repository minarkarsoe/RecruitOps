# Product Overview — In-house Recruitment Cloud System

Source: `docs/reference/In-house Recruitment - Product Overview.pdf` (Burmese).

## Purpose

A cloud system for **a company's own recruitment function** — explicitly *not* for
external recruitment agencies. Its central problem is **internal collaboration**:
the back-and-forth between In-house Recruiters and Department Hiring Managers, which
today happens over email, chat and spreadsheets. The system makes the whole hiring
lifecycle — from raising a vacancy to onboarding — fast, traceable and secure.

## Target market

Domestic enterprises above ~**1,000 staff** — banks, FMCG/retail chains, telecoms,
factories and MFIs. Sold **partner-led** through local sales partners / HR consultants,
as an **annual MMK subscription** ([ADR-0011](../decisions/ADR-0011-commercial-model-v2.md)).

Headline value propositions:
- **Data sovereignty** — candidate data and CVs stay in the customer's own AWS account or
  on-premise, never in a shared third-party SaaS.
- **Automation efficiency** — spam and duplicate CVs arriving from Facebook, Telegram and
  Viber are filtered automatically.

> ⚠️ Banks as a target segment raises the likelihood of **SSO / AD / Entra ID** being a
> procurement requirement — which would supersede [ADR-0002](../decisions/ADR-0002-jwt-auth.md).

## Users

| User | What they get |
|---|---|
| **In-house Recruiters** | Less manual data entry. Manage large candidate volumes systematically; AI surfaces the best-matching candidates quickly. |
| **Hiring Managers** | Raise a requisition without leaving the system; schedule interviews and score candidates without friction. |
| **Management & HR Directors** | Control recruitment budget; decide from accurate, data-driven reports; support annual strategic planning. |

## Modules

**MVP = 1, 2, 3, 5** ([ADR-0006](../decisions/ADR-0006-mvp-scope.md)). Modules 4, 6 and 7-integrations are deferred.

| # | Module | Spec |
|---|---|---|
| 1 | ⭐ Job Requisition & Approval | [01-job-requisition-approval.md](modules/01-job-requisition-approval.md) |
| 2 | ⭐ Applicant Tracking (ATS) & Sourcing | [02-ats-and-sourcing.md](modules/02-ats-and-sourcing.md) |
| 3 | ⭐ Interview & Assessment | [03-interview-and-assessment.md](modules/03-interview-and-assessment.md) |
| 4 | Offer Management & Pre-boarding | [04-offer-and-preboarding.md](modules/04-offer-and-preboarding.md) |
| 5 | ⭐ Reporting & Analytics | [05-reporting-and-analytics.md](modules/05-reporting-and-analytics.md) |
| 6 | Strategic Recruitment Planning & Budgeting | [06-planning-and-budgeting.md](modules/06-planning-and-budgeting.md) |
| 7 | Settings & Integrations | [07-settings-and-integrations.md](modules/07-settings-and-integrations.md) |
| 8 | Multi-Channel Sourcing (Viber/Telegram/FB) | [08-multi-channel-sourcing.md](modules/08-multi-channel-sourcing.md) — *first post-MVP* |
| 9 | Internal Mobility *(future, no decision taken)* | — |

## The core loop

```
Hiring Manager raises Requisition
        ↓ (Dynamic Approval Workflow: Dept Head → Finance → HR)
Approved → Job Posting created (career page / shareable job page / social)
        ↓
Candidates apply (form) OR recruiter bulk-uploads CVs (OCR auto-profiling)
        ↓ Smart Match ranks candidates against the JD
Talent Pipeline (Kanban / List) — screening → shortlist
        ↓
Interview scheduled (calendar sync) → Scorecards + collaborative notes
        ↓
Offer generated → e-signature → pre-boarding documents
        ↓
Hired → notify IT/Admin → analytics feed (time-to-hire, source of hire)
```

Budget and headcount planning (Module 6) wraps the whole loop; every requisition
draws against an approved plan.

## What this is NOT

Deliberately out of scope, to keep the pivot clean:

- No external client companies, client CRM, or client tiers (Gold/Silver/Bronze).
- No agency-client contracts or SLA expiry tracking as a commercial relationship.
  (SLA appears only in Module 7, as *system* configuration.)
- The shareable link is a **public job page for applicants**, not a client CV-review portal.
