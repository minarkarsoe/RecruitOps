# Module 5 — Reporting & Analytics

**Status:** ⬜ Not started · **Priority:** Medium — needs pipeline data to exist first.

## Purpose

Give Management and HR Directors decisions grounded in data, not anecdote.

## Features

### 5.1 Time-to-Hire / Time-to-Fill
Measure how long a vacancy takes to fill.

### 5.2 Source of Hire Analytics
Break down which **channel** produced the most hired candidates.

### 5.3 Pipeline Conversion Rates
Analyse **at which stage candidates drop off** most.

### 5.4 Custom Report Builder
Pick the fields needed for a management review and export to **Excel or PDF**.

## Design notes

- These metrics are **derived**, not stored — they depend on accurate stage-transition
  timestamps. `ApplicationStageHistory` (append-only, from Module 2) is the source of
  truth and must be written from day one, or these reports can never be back-filled.
- Time-to-Hire vs. Time-to-Fill are different clocks (offer accepted vs. requisition
  opened → filled). Define both precisely before implementing.

## Entities

- `ApplicationStageHistory` — append-only stage transitions (**must exist early**)
- `ReportDefinition` — saved custom report configuration
- Optionally a read-model / materialised view for dashboard performance

## Open questions

- Report scope: per department, per recruiter, company-wide? Who may see whose numbers?
- Live queries or a nightly-refreshed reporting table? (Affects DB design.)
- Export: server-side generation (needs a PDF/Excel library) or client-side?
