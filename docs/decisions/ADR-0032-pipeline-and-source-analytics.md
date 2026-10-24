# ADR-0032 — Pipeline & source analytics (Module 5.2): where a candidate came from, how far they got, and who sees the staff numbers

**Status:** Accepted · **Date:** 2026-10-24 · **Module:** 5 (Reporting & Analytics), touching 2.1

## Context

Spec 5.2 (`docs/product/modules/05-reporting-and-analytics.md`) and `design/internal/analytics-pipeline.html`
ask for three things: the share of candidates from each channel and the share of each channel that was
hired; a drop-off funnel in four bands (Sourcing → Screened → Interview → Offered); and a recruiter
leaderboard (CVs brought in, interviews arranged, hires). The spec and design both leave three things open
— the funnel's mapping onto the eight pipeline statuses, who may see the leaderboard, and (implicitly)
where a channel comes from.

The last turned out to be the blocker. **Every application in the database says `Direct`**: the public
form and the bulk CV upload both hard-code it, and nothing sets Facebook, LinkedIn, Telegram or Referral.
A source chart built on today's data would be a single 100% slice.

The product owner answered on 2026-10-24: the leaderboard is for **HR Director and Admin only**; channels
come from **tagged links plus the referrer**; a hire is an **accepted offer**.

## Decision

### 1. Capturing the channel (Module 2.1)

- `SourceChannel` gains **`Viber`** and **`ManualUpload`**. `Direct` means "the career page, from no
  channel we can name". `ExcelImport` stays, unused (no spreadsheet import exists).
- **The public form sends the channel it resolved**, in this order:
  1. a `via` tag on the page's address — `?via=facebook|linkedin|telegram|viber|referral`;
  2. Facebook's own `fbclid` click tag → Facebook;
  3. the referrer's host — `facebook.com`, `fb.com`, `fb.me` → Facebook; `linkedin.com`, `lnkd.in` →
     LinkedIn; `t.me`, `telegram.me`, `telegram.org` → Telegram; `viber.com` → Viber (subdomains included);
  4. otherwise nothing, recorded as `Direct`.
- **The API accepts only those five names** from an anonymous caller; anything else, `ManualUpload`
  included, is recorded as `Direct`. It is a hint from the browser, not a fact: a candidate could type a
  `via` tag. That costs an analytics figure, never access — the channel decides nothing else.
- Only the **host** of the referrer is ever read, in the browser; neither it nor the full address is sent.
- A candidate who already exists keeps the channel of their **first** application on their record; each
  application carries its own.
- **Bulk CV uploads record `ManualUpload`**, and a data migration re-labels the applications and
  candidates already created by one — found through `BulkUploadFiles`, or, for the first implementation
  (2026-08-09 to 08-21) which wrote no file rows, through its stage-history note "Created via Bulk CV
  Upload" (database review, R1). Its `Down` restores `Direct`.
- **Tagged links are offered where links are shared:** the public job page's share buttons (Facebook,
  Telegram, Viber — `design/public/job.html`) tag what they share; the posting screen's public link gains
  a per-channel copy row, drawn first in `design/internal/postings.html`.

### 2. The page's rows and period

- **A cohort of applications by the day they arrived** (`AppliedAt`, on the company's calendar): the last
  90 days (the default) or this year to date. Filters: **position** (a posting) and **source**.
- The **recruiter filter is not built.** Nothing assigns a recruiter to a candidate (the gap ADR-0031 §3
  names); narrowing the charts to "candidates a recruiter touched" would give numbers that do not add up to
  the page's own totals. The leaderboard is the per-recruiter view.

### 3. Where candidates come from — two charts

- **Share:** applications per channel ÷ all applications in the cohort. The spec's four channels —
  **Facebook, LinkedIn, Career page (`Direct`), Manual upload** — take the design's four validated colours
  in that fixed order; Telegram, Viber, Referral and Excel import are summed as **Other**, in a neutral grey,
  and only shown when non-zero.
- **Which of them get hired:** of each channel's applications in the cohort, the share with an
  **accepted offer** (ADR-0031's hire), at any time so far. A separate chart on its own scale, as the
  design insists — volume is not quality.

### 4. Where candidates drop off — the four bands

| Band | Pipeline statuses |
|---|---|
| Sourcing | Sourced, Applied |
| Screened | Screening, Shortlisted |
| Interview | Interview |
| Offered | Offer, Hired |

- A band counts the cohort's applications that **reached it**: the furthest band in the application's
  stage history or its current status. Reaching Interview counts in Screened too — a candidate moved
  straight from Applied to Interview was not lost at screening.
- **Rejected has no band.** It is an exit from every stage; a rejected candidate counts in each band they
  reached before it. So Sourcing is always the whole cohort.
- Each band after the first shows its drop-off from the band before, as a whole percentage.

### 5. Recruiter leaderboard — HR Director and Admin only

- Rows are the people who did any of: **CVs in** — applications they created by bulk upload, arriving in
  the period; **Interviews** — rounds they scheduled (`Interview.ScheduledByUserId`) whose start falls in
  the period, cancelled rounds excluded; **Hires** — their offers (`Offer.CreatedByUserId`) accepted in
  the period. The position and source filters apply to the applications behind each count.
- **Listed by name, never ranked**, as the design requires: no column alone says who is doing best.
- **Omitted by the API** for everyone else — the response carries no leaderboard, not an empty one hidden
  by the screen. Display names only (an email is `users:read`, ADR-0019).
- **Follows the base role, not a permission** (security review, Low): a custom role on an HR Director base
  sees it, as it sees everything else an HR Director does. A dedicated permission is the change to make
  if a company needs to withhold it from such a role.

### 6. Who sees the page

| Who | Sees |
|---|---|
| Admin, HR Director | Everything above |
| Recruiter | Both source charts and the funnel — every candidate, as they already see every candidate; no leaderboard |
| Hiring Manager, Approver | Nothing — the API answers 404 (spec 5.2: "no access to this page at all") |
| Interviewer | Nothing — 403 (the `InternalUser` policy) |

The endpoint also needs `permission:requisitions:requisitions:read`, as the executive dashboard does.
Custom roles follow their base role.

### 7. The screen

`/analytics/pipeline` is rebuilt to the design. Its August content — time in each stage, the eight-status
funnel, the source bars — leaves the SPA; `GET /api/analytics/time-to-hire`, `/conversion` and
`/source-of-hire` stay for API callers — **and still answer a Hiring Manager**, over their own departments,
as they did in August (security review, Low). No screen calls them; closing them to Hiring Managers is
for 5.3, which settles the analytics endpoints' per-role rules together.

## Consequences

- Source figures start from the day this ships, apart from bulk uploads, which the migration re-labels.
  Older public applications stay `Direct` — there is no way to know where they came from.
- A link shared without a tag through an app that sends no referrer (Viber, Telegram's in-app browser)
  still counts as Career page. The tagged links are the cure; the referrer is the fallback.
- "Time in each stage" is no longer on any screen. If it is wanted, it needs a place in a design.
- Not decided here: scheduled reports (5.3), and whether the leaderboard should also sit behind a company
  setting.
