# Module 2 — Applicant Tracking System (ATS) & Sourcing

**Status:** 🚧 Partial — re-measured 2026-08-29 against the shipped container, because this line
had drifted in **both** directions at once.

| | |
|---|---|
| 2.1 · 2.2 · 2.7 | ✅ built |
| 2.6 search | ✅ built — `SearchService` + `pg_trgm`. This line used to say "not started" |
| 2.3 upload | ⏸ **PDF/DOCX text only. There is no OCR** — paused by the product owner 2026-08-29. Images are rejected at upload; a scanned PDF is marked `Skipped` with a reason rather than imported as a blank candidate. The spec text below still describes the intended feature |
| 2.4 Smart Match | 🚧 **API works; no UI reaches it.** `SmartMatchBreakdown` has zero production importers, so it is absent from the bundle |
| 2.5 pipeline | 🚧 **list only.** The Kanban board and the 360° candidate view are both written and both orphaned |
**Priority:** Core — this is the product's daily-use surface.

## Built so far

The join between Module 1 and Module 2: **an approved requisition becomes a posting, the
posting becomes a public page, the page produces applications, applications enter a pipeline.**

Rules worth knowing before changing anything here:

- **A posting requires an Approved requisition, and there is one posting per requisition.**
  Both in the service and as a unique index. This is the product's central guarantee — that
  nothing is advertised the business has not approved — so it does not depend on any single
  code path remembering to check.
- **Title and description are copied from the requisition, not referenced.** The recruiter
  rewrites an internal JD into candidate-facing copy; that must not alter what approvers
  signed off on.
- **The public link is minted once and kept.** Re-publishing does not re-issue it, because a
  link already shared to Facebook or sent to a candidate must keep working.
- **Salary is private unless the posting opts in.** The budget travels from the requisition,
  but publishing it by default would expose the company's pay bands the first time anyone
  published a job. `PublicJobDto` is deliberately a narrower type than the internal one.
- **The anonymous path has no tenant claim** — the token is what identifies the company. See
  `PublicJobService` for how the query filters are bypassed and the tenant re-applied.
- **Stage history is written from the application's arrival**, including when nobody is
  logged in. Module 5 is built on these rows and they cannot be reconstructed after the fact.
- **Custom-field answers are rebuilt from the schema, never stored as submitted.** The
  schema comes from a recruiter and the answers from an anonymous stranger; the two paths
  share `Domain/ApplicationFormSchema` precisely so they cannot disagree about what a field
  means. Field keys are generated and never editable — the key is the JSONB key answers live
  under, so renaming it would orphan everything already collected.

## Purpose

Get candidates into the system from every direction, profile them automatically, rank
them against the JD, and move them through a visible pipeline.

## Features

### 2.1 Multi-Channel Job Posting & Shareable Link
Publish a job to the **company career page**. If the company has no career page, the
system generates a **standalone job page (shareable link)** that can be posted to
Facebook, LinkedIn, etc.

> ⚠️ Not to be confused with the legacy agency "client portal" link. This link is
> **public, for applicants**.

### 2.2 Customizable Application Form
The form behind the shareable link supports **custom fields** (e.g. expected salary,
earliest start date). Submissions land **directly in the Talent Pipeline**.

### 2.3 Resume Upload & OCR Auto-Profiling
Upload externally-sourced CVs — **PDF, Word, JPG, PNG** — one at a time or in **bulk
up to 50 files**. OCR reads the documents and **auto-builds candidate profiles**.
After upload, a pop-up summarises each file as **Success / Skipped / Canceled**.

> ⏸ **Built as: PDF and Word only** (paused 2026-08-29, product owner). JPG/PNG are rejected at
> upload and a scanned PDF is reported `Skipped`, because there is no OCR engine in the build —
> shipping it as "supported" imported photographed CVs as nameless, unsearchable candidates.
> The paragraph above is the **target**, not the current state. Reaching it needs an OCR engine
> with Burmese support or a vision-model call; both are new dependencies, decision open.
> This matters more here than the spec implies: in this market a CV is often a phone photo.

### 2.4 AI-Powered Candidate Matching (Smart Match)
When a new vacancy is created, the system reads the JD requirements, compares against
the candidate database, and returns a **match percentage** (e.g. 80% Match, 50% Match),
surfacing the best fits as **Recommended Candidates**.

### 2.5 Visual Talent Pipeline + 360° Candidate History
Manage candidates by stage in a **Kanban board or list view**. Opening a candidate
shows a 360° view: profile, **previously applied positions**, interview dates, and
past interview feedback — all in one place.

### 2.6 Comprehensive Database Filtering & Search
Filter by age, gender, previous position (e.g. Sales, HR, Admin), and **keyword search
inside CV content**.

⚠️ Burmese has no consistent word spacing and PostgreSQL full-text search has no Burmese
configuration, so default FTS tokenisation will not work. Plan for trigram (`pg_trgm`) or
segmentation-based search, over **normalized Unicode** text
([ADR-0009](../../decisions/ADR-0009-myanmar-script-handling.md)).

### 2.7 Duplicate Detection
Incoming CVs are auto-checked against existing records by **phone number and email**.

## Entities

- `Candidate`, `CandidateDocument` (raw file + OCR text + parse status)
- `JobApplication` (candidate ↔ job posting, pipeline stage), `PipelineStage`
- `JobPosting`, `JobChannelPost` (career page / shareable link / social)
- `ApplicationForm`, `ApplicationFormField` (custom fields)
- `CandidateMatch` (score per candidate/job, with explanation)

## ✅ Resolved — how OCR and Smart Match work on-premise

Decided in [ADR-0008](../../decisions/ADR-0008-document-extraction-and-ai-profiling.md).

**Phase 1 (MVP): local text extraction, no network.** PDF/Word parsed in-process; images
and scanned PDFs via a local OCR engine. Results pre-fill a candidate form that **a human
reviews and confirms**. Works fully offline — this is the default path and must never regress.

**Phase 2: AI structuring, optional, behind an API key.** Extracted text → LLM → JSON
payload matching the form schema. No key ⇒ feature off, Phase 1 still works. Key ownership
is tiered: our key (paid add-on) for hosted, the customer's own key for on-prem.

**Smart Match (2.4) follows the same rule** — ship an explainable local baseline
(skills/keyword/experience vs. the JD); AI ranking is an enhancement, not the foundation.

Constraints carried forward: bulk upload must be **asynchronous** (50 files = background
job, and the Success/Skipped/Canceled pop-up is the job result); PDF/OCR library licences
must be permissive (this is closed-source commercial software).

> **"Asynchronous" was satisfied twice, and the first time did not count.** The original
> implementation was `_ = Task.Run(...)` over a static in-memory dictionary — the *shape* of a
> background job with none of the properties one is for. A restart erased the batch outright and
> the recruiter's fifty files answered 404. Rewritten 2026-08-21 onto
> [ADR-0026](../../decisions/ADR-0026-outbound-delivery-and-background-jobs.md): a
> `BulkUploadBatch` row plus one `BulkUploadFile` per CV, bytes in object storage, drained by a
> worker with retry, backoff and an attempt cap. The pop-up's Success / Skipped / Failed now comes
> from those rows, so it survives whatever happens to the process.
>
> **`Skipped` still has no producer.** Nothing in the pipeline decides a CV is correctly not
> processed, so that third state is reserved rather than reached. Worth deciding what should
> produce it — a duplicate CV of an existing application is the obvious candidate.

**Myanmar script — see [ADR-0009](../../decisions/ADR-0009-myanmar-script-handling.md).**
Two separate issues: (a) **Zawgyi→Unicode normalization at ingest is mandatory and lands in
the MVP** — a Word/PDF authored in Zawgyi extracts as garbage even with no OCR involved;
(b) **Burmese OCR accuracy is deferred** pending a real-CV test, and the image/scanned path
must be parkable without affecting digital extraction or manual entry.

## Open questions

- Which OCR engine? (Cloud API vs. self-hosted — affects cost, PII residency, and Burmese-script accuracy.)
- Smart Match: rules/keyword scoring, embeddings, or an LLM? Must the score be **explainable** to justify a rejection?
- Storing age/gender for filtering has **data-protection implications** — confirm this is lawful and intended for the target market.
- ~~Duplicate detection: auto-merge, or flag for human confirmation?~~ **Auto-reuse on the
  public path**: a match on normalised email or phone attaches the new application to the
  existing candidate, filling blank fields but never overwriting filled ones. Merging two
  candidates that were *already* created separately (the `MergedIntoCandidateId` path) still
  needs a human-confirmation UI.
- ~~Custom application fields (2.2) are stored but not yet rendered.~~ **Built.** Six field
  types (text, long text, number, date, dropdown, yes/no), max 20 per form. `Domain/
  ApplicationFormSchema` validates the schema on save and the answers on submit, and rebuilds
  the answer document rather than storing what the applicant sent. Still open: **file upload
  as a field type** — that waits on the object-storage abstraction (ADR-0013), same as 2.3.
- Max file size and retention period for CV files (ties into Module 7 retention policy).
