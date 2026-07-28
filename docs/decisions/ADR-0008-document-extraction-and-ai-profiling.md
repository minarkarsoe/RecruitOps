# ADR-0008 — Document text extraction now, AI-assisted profiling behind an API key

- **Date:** 2026-07-27
- **Status:** Accepted
- **Resolves:** the on-premise AI/OCR blocking constraint raised in
  [ADR-0004](ADR-0004-single-tenant-deployment.md) and Module 2
- **Related:** [ADR-0006](ADR-0006-mvp-scope.md), [ADR-0007](ADR-0007-productization-and-addons.md)

## Context

Module 2.3 requires CV upload (PDF, Word, JPG, PNG — bulk up to 50 files) with
**auto-profiling**, and 2.4 requires **AI Smart Match**. But deployment is often
**on the customer's own server** with no guaranteed internet egress, and CVs are exactly
the sensitive data that motivated an on-prem purchase. A hard dependency on a cloud AI
API would make the product unsellable to those customers.

## Decision — two separable phases

### Phase 1 (MVP): local text extraction, no network

Extract text **in-process, on the customer's server**. No data leaves the install.

| Input | Approach |
|---|---|
| PDF (digital text) | Text extraction from the PDF content stream |
| Word `.docx` | Open XML parsing |
| Images (JPG/PNG) | Local OCR engine |
| **Scanned/image-only PDF** | Extraction returns empty → render pages to images → local OCR fallback |

The result — raw text plus whatever fields simple rules can confidently pull (email,
phone, obvious headings) — pre-fills a **candidate form that a human reviews and
confirms**. This alone satisfies "reduce manual data entry" and works fully offline.

### Phase 2: AI structuring, optional, behind an API key

Extracted text → LLM with a structured-output prompt → **JSON payload matching the
candidate form schema** → pre-fills the form for human confirmation.

- **Strictly optional.** No key configured ⇒ feature is off and Phase 1 still works.
  Never a hard dependency.
- Key is **configuration**, not code — per install, via env/secret store,
  **never stored in the database in plaintext** (Module 7).
- Provider-agnostic behind an interface, so a customer can point at a different vendor —
  or eventually a self-hosted model — without a code change.

### API key ownership — tiered

| Deployment | Key | Rationale |
|---|---|---|
| **Vendor-hosted** | Our key, sold as a **paid add-on** ([ADR-0007](ADR-0007-productization-and-addons.md)) | Turnkey for the customer; the add-on price must cover token cost, since a one-time licence does not ([ADR-0005](ADR-0005-commercial-model.md)) |
| **On-premise** | **Customer's own key** | They own the vendor relationship and the data-processing decision — the point of choosing on-prem |

⚠️ If we supply the key, **token usage is a recurring cost against a one-time fee.**
Meter it per install and set a quota, or a bulk upload of 50 CVs repeated daily will
quietly erode the margin.

## Guardrails

1. **Human confirmation is mandatory.** AI-extracted PII is never written straight into a
   candidate profile. Show the parse, let a person accept or correct it. This protects
   data quality *and* means an AI error is never silently authoritative.
2. **Persist provenance** — raw file, extracted text, parser version, AI output, and a
   `needs_review` flag. Without this, a bad parse can't be diagnosed or re-run.
3. **Bulk (50 files) must be asynchronous.** A background job with per-file status; the
   spec's Success / Skipped / Canceled summary is the job result, not a synchronous response.
4. **Disclose AI processing.** If CV text is sent to a third party — even on the
   customer's own key — that must be stated in the product documentation so the customer
   can meet their own obligations to candidates.
5. **Smart Match (2.4) follows the same rule.** Ship a local, explainable baseline
   (skills/keyword/experience scoring against the JD). AI ranking is an enhancement, not
   the foundation — a match score that can't be explained is hard to defend when it
   influences a rejection.

## Library selection — ⚠️ licence review required before adding

Per `CLAUDE.md`, new packages need approval. **Licence compatibility is the deciding
factor here: this is closed-source commercial software, so a copyleft/AGPL library is
disqualifying** — some popular PDF libraries are AGPL and would force source disclosure.
Prefer permissive (MIT / Apache-2.0 / BSD) and record the chosen licence for each.

Candidates to evaluate: a permissive .NET PDF text extractor, Microsoft's Open XML SDK
for `.docx`, and a permissive local OCR engine for images.

**On the v2.0 draft's proposed stack (Apache Tika / PaddleOCR) — two findings:**

- **Apache Tika is a Java project.** Using it from a .NET backend means shipping a JVM in
  the container or running `tika-server` as a sidecar — extra weight on every install,
  including on-premise ones. Native .NET extraction libraries avoid the JVM entirely.
  Evaluate both; the JVM cost is a real deployment consideration, not a detail.
- **PaddleOCR does not officially support Burmese.** It is listed as needing dictionary and
  corpus contributions ("call for contribution"), so it is *worse* than Tesseract for this
  market — Tesseract at least ships a (weak) `mya.traineddata`. Neither solves Burmese OCR
  out of the box. This strengthens [ADR-0009](ADR-0009-myanmar-script-handling.md): keep the
  OCR engine swappable and treat Burmese OCR as deferred.

**Known risks to test before committing:**
- **Burmese script** — see [ADR-0009](ADR-0009-myanmar-script-handling.md). Two distinct
  problems: **Zawgyi↔Unicode normalization** (affects Phase 1 text extraction too, and is
  *not* optional) and **Burmese OCR accuracy** (deferred, with a defined evaluation plan;
  the OCR path must be parkable without affecting anything else).
- **Legacy `.doc`** (binary, pre-2007) is not covered by Open XML and may need conversion
  or rejection with a clear message.
- **Password-protected / corrupt files** must land in the "Skipped" bucket, not crash the batch.

## Consequences

- ✅ **The on-prem blocker is resolved** — the MVP works with no internet.
- Module 2 can proceed. The AI layer is a later, sellable enhancement.
- Extraction quality becomes a support surface: customers will report bad parses. The
  human-review gate keeps those as annoyances rather than data corruption.
- Two code paths (with/without AI) need testing — the no-key path is the *default* and
  must never regress.
