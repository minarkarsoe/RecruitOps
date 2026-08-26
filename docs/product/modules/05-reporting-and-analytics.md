# Module 5 — Reporting & Analytics

**Status:** ⬜ Not started · **Priority:** Medium — needs pipeline data to exist first.
**Scope last revised:** 2026-08-18, from a sales requirement document
(`Module 5_Reporting & Analytics.pdf`). That document is the source for the sub-module
structure, the metric definitions, the per-role visibility rules and the scheduled-email
requirement below.

## Purpose

Give Management and HR Directors decisions grounded in data, not anecdote.

## Data inflow

This module reads from Modules 1–4 and stores little of its own: how long a request waited
from submission to offer acceptance, which channel a candidate arrived through, and what
interviewers scored. It computes and presents; it does not own the facts.

## Sub-modules

The requirement specifies **three menu items**.

### 5.1 Executive Dashboard (high-level KPIs)

For Management and HR Directors — the whole recruitment process as charts.

- **Time-to-Hire** — average duration from the **candidate's application date** to **offer
  accepted**.
- **Time-to-Fill** — average duration from the **requisition's approval date** to **offer
  accepted**.
- **Offer Acceptance Rate** — percentage of issued offers that were accepted.
- **Active Vacancies Overview** — open positions per department, as a bar chart.
- **Filters:** Date Range (Year, Quarter, Month), Department, Recruiter Name.

> ⚠️ **Both clocks were re-defined by this requirement.** The earlier note in this file said
> Time-to-Fill ran "requisition opened → filled". It now runs from **requisition approved**,
> and both metrics **end at offer acceptance**, not at hire. Two consequences: the clock
> excludes the approval wait entirely, so a requisition stuck for twelve days in an approval
> chain shows a *shorter* Time-to-Fill than one approved immediately; and **neither metric
> can be computed until Module 4 exists**, because "offer accepted" is a Module 4 event.

### 5.2 Pipeline & Source Analytics

Where candidates come from, and where they fall out.

- **Source of Hire** (pie chart) — Facebook, LinkedIn, Career Page, Manual Upload: share of
  candidates from each channel, and the share that were hired.
- **Funnel / Drop-off Rate** (funnel chart) — `Sourcing → Screened → Interview → Offered`,
  showing how many remain at each step.
- **Recruiter Leaderboard** — per-recruiter performance: CVs brought in, interviews
  arranged, hires made.
- **Filters:** Date Range, Position Title, Source, Recruiter Name.

> ⚠️ The funnel's four labels are **not** the `PipelineStatus` enum, which is
> `Sourced / Applied / Screening / Shortlisted / Interview / Offer / Hired / Rejected`.
> Either the chart maps several statuses into each band, or it invents a second vocabulary.
> Decide which before building, and write the mapping down — an unmapped funnel is how two
> different numbers for "how many reached interview" start circulating.

### 5.3 Custom Report Builder

For meetings, where the needed columns are never the ones a fixed report has.

- Pick data fields with checkboxes — e.g. Requisition ID, Job Title, Approved Budget,
  Candidate Name, Stage, Offered Salary, Joining Date.
- `Generate Report` renders a table view.
- **Actions:**
  - **Export** — download as Excel or PDF.
  - **Save Template** — keep a report configuration that will be run repeatedly.
  - **Schedule Email (automated)** — send a chosen report to Management on a recurring
    schedule (the requirement's example: every Friday, weekly).

## Permissions

Visibility differs **per sub-module**, which answers this file's previous open question of
"who may see whose numbers".

| Role | Executive Dashboard | Pipeline & Source | Custom Report Builder |
|---|---|---|---|
| Management / HR Director | Whole company | Everything | All data fields; may schedule automated emails |
| Recruiter | Only KPIs for **vacancies they handle** — explicitly *not* other recruiters' data | Source of Hire and Funnel only | Only data their access level already permits, exported as a custom report |
| Hiring Manager | Only their **own department's** Time-to-Fill and Active Vacancies | **No access to this page at all** | Only data their access level already permits |
| System Admin | — | — | All data fields; may schedule automated emails |

> The Recruiter rule ("cannot see another recruiter's data") is a **row-level** restriction
> on an aggregate, which is harder than it looks: a company-wide average silently leaks the
> other recruiters' numbers. Aggregates must be computed over the caller's permitted rows,
> not computed once and filtered for display.

## Data outflow to Module 6

Yearly Time-to-Fill durations, per-channel success rates and budget-usage figures feed
**Module 6 (Strategic Planning & Budgeting)** as baseline data for planning the next year's
recruitment strategy.

## Design notes

- These metrics are **derived, not stored** — they depend on accurate stage-transition
  timestamps. `ApplicationStageHistory` (append-only, from Module 2) is the source of truth
  and must be written from day one, or these reports can never be back-filled.
- The dashboard is read-heavy and the underlying tables are transactional. Decide between
  live queries and a refreshed read model before building, not after the first slow page.

## Entities

- `ApplicationStageHistory` — append-only stage transitions (**must exist early**)
- `ReportDefinition` — a saved custom report configuration (`Save Template`)
- `ReportSchedule` — recipients, cadence, and the report it runs
- Optionally a read model / materialised view for dashboard performance

## Open questions

**Answered by the 2026-08-18 requirement** (previously open):

- ~~Report scope: who may see whose numbers?~~ Answered in full by the permissions table.
- ~~Export: server-side or client-side?~~ Excel **and** PDF are both required, and
  `Schedule Email` sends a report with **no browser present** — so generation must be
  **server-side**. Client-side export cannot satisfy the scheduled case.

**Still open, and new:**

- **Live queries or a refreshed reporting table?** Still undecided, and now weightier: the
  per-recruiter row-level rule above means the aggregate cannot simply be cached once
  company-wide.
- **Scheduled email needs a scheduler and an email sender, neither of which exists.** Per
  `docs/status/FEATURE-STATUS.md` there is no email sender in the codebase; a recurring job
  runner is also absent. This is the same missing capability that blocks Module 3's
  interview invitations and three Module 4 features — it is now a shared prerequisite, not
  a per-module detail.
- **Recruiter Leaderboard is staff performance monitoring**, not recruitment analytics.
  Ranking named employees by output has employment-relations implications and is personal
  data about staff rather than candidates. Confirm with the customer that it is wanted, who
  may see it, and whether it belongs behind a setting.
- **Funnel band mapping** — see the warning in 5.2.
- **Time-to-Hire excludes the approval wait by definition.** Confirm that is intended,
  because the product's own pitch is that approval delay is the thing worth measuring. A
  separate "time in approval" metric may be what management actually wants.
