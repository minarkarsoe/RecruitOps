# ADR-0031 — Executive dashboard (Module 5.1): what the three clocks measure, and whose rows they count

**Status:** Accepted · **Date:** 2026-10-24 · **Module:** 5 (Reporting & Analytics)

## Context

Module 5's spec (`docs/product/modules/05-reporting-and-analytics.md` §5.1, revised 2026-08-18) defines
the Executive Dashboard's three headline numbers and who may see them, and `design/internal/
analytics-dashboard.html` draws it. What shipped in August predates both: its "Average Time-to-Hire"
tile said *"from application to offer acceptance"* and measured application to the **Hired pipeline
stage** — a manual move, not an acceptance — and there was no Time-to-Fill, no acceptance rate, no
period, and a Recruiter saw company-wide numbers. Neither clock was computable until Module 4 could
record an acceptance; it can since 2026-10-23 (`Offer.Status = Accepted`, `Offer.RespondedAt`).

Some of what the spec leaves open has to be settled to compute anything. Points marked **(to confirm)**
take the reading the spec and design point to and are cheap to change; the product owner has not
answered them.

## Decision

### 1. The metrics

| Metric | From | To | Counted in a period when |
|---|---|---|---|
| **Time-to-Hire** | the application (`JobApplication.AppliedAt`) | the offer's acceptance (`Offer.RespondedAt`) | the offer was accepted in it |
| **Time-to-Fill** | the requisition's approval (`Requisition.DecidedAt`, final approval) | the offer's acceptance | the offer was accepted in it |
| **Offer acceptance rate** | — | — | accepted ÷ (accepted + declined + expired), over offers whose outcome fell in the period |

- Each is an **average over accepted offers** — one per hire — in days, to one decimal with a half
  rounded up (30.25 days is 30.3, as a person checking by hand would write it — not .NET's default
  round-to-even, which gives 30.2). A period with none shows "no offers accepted", never 0.
- **An outcome is dated on the company's calendar** (its time zone): an acceptance or a decline by
  `RespondedAt`, an expiry by the day after `ExpiresOn`. Offers still waiting are not in the rate.
- **Expiry is read, not written** (ADR-0030 §5a: anything that counts offers must expire them the same
  way first): an offer still Sent or Viewed past its expiry date counts as expired here, as `OfferService`
  would make it on its next read.
- Time-to-Fill excludes the approval wait **by definition** — the spec says so and the design flags it.
  The design's fourth tile, *time in approval*, is drawn as a **proposal outside the requirement** and is
  not built.

### 2. The period

**Month, quarter or year to date** in the company's time zone — the design's toggle — each compared
with the **whole previous** month, quarter or year (the design's "4 days faster than Q2"). Quarter is
the default. Filters: department, and recruiter (§3).

### 3. Whose rows

| Who | Sees |
|---|---|
| Admin, HR Director | Everything; may filter by department and by recruiter |
| Recruiter | **Only offers they made** — every tile recomputed over their rows, never a company average filtered for display (a company-wide average discloses the other recruiters' numbers) |
| Hiring Manager | **Only their departments**, and only **Time-to-Fill** and **active vacancies** — Time-to-Hire and the acceptance rate are omitted by the API, not hidden by the screen |
| Approver, Interviewer | Nothing (ADR-0018) |

- **"The vacancies a recruiter handles" means the offers they made (to confirm).** Nothing in the data
  says which recruiter handles a requisition or a posting — neither the spec nor any design assigns one.
  Every offer records its maker (`Offer.CreatedByUserId`), and every tile on this screen is a measure of
  offers. The recruiter filter lists the people who have made offers in reach — by display name only (an email
  is `users:read`, ADR-0019) — and does not shrink when a department is chosen. If the company wants an
  assigned recruiter per vacancy instead, that is a field on the posting and a screen to set it — not
  drawn.
- **Active vacancies are not recruiter performance**, and a recruiter already sees every requisition, so
  they are shown in full to recruiters; a Hiring Manager sees their departments'. The recruiter filter
  does not apply to them.
- Custom roles follow their base role (ADR-0021) — and the endpoint also needs
  **`permission:requisitions:requisitions:read`**, the permission the SPA's link has always been gated
  on: the vacancies are requisitions' seats, so a custom role trimmed of requisitions does not read them
  here instead (added on the security review's advice, 2026-10-24).

### 4. Active vacancies

Per department: for each **Approved** requisition, its headcount less the offers accepted for it
(through its postings), never below zero. A requisition is "open" until every seat has an accepted offer;
there is no "filled" requisition status, so nothing else closes it.

### 5. The screens

The analytics area becomes the three screens the design draws: **Executive dashboard** (`/analytics`),
**Pipeline & source** (`/analytics/pipeline` — the funnel, sources and time in each stage), **Reports**
(`/analytics/reports` — the report builder). Only the first is rebuilt here; the other two keep their
current content under their own headings. The old KPI tiles (`GET /api/analytics/kpis`) leave the SPA:
their "Time-to-Hire" measured something else under that name. The endpoint stays for API callers, and
its field is documented for what it is — days to the Hired stage.

## Consequences

- The dashboard's numbers start at the first accepted offer: an install with Module 4 unused shows "no
  offers accepted" until it is.
- Offers made by a recruiter who has since left still count — in the company's numbers, and under their
  name in the recruiter filter.
- Module 6 (planning) can take yearly Time-to-Fill from the same computation.
- Not decided here: the Pipeline & source screen's per-role rules (spec 5.2: a Hiring Manager has no
  access to it), the recruiter leaderboard, and scheduled reports.
