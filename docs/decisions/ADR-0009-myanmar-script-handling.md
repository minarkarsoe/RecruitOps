# ADR-0009 — Myanmar script: normalize at ingest; OCR deferred and parkable

- **Date:** 2026-07-27
- **Status:** Accepted (normalization) · Deferred pending testing (Burmese OCR)
- **Related:** [ADR-0008](ADR-0008-document-extraction-and-ai-profiling.md), [ADR-0006](ADR-0006-mvp-scope.md)

## Context

The market is Myanmar, so CVs, JDs and typed input will contain Burmese script. Two
*separate* problems hide behind "Burmese support", and the smaller-looking one is
actually the more urgent.

### Problem 1 — Zawgyi vs Unicode (affects everything, including Phase 1)

Myanmar text exists in two incompatible encodings: **standard Myanmar Unicode** and the
legacy **Zawgyi-One** font encoding. Both occupy the same Unicode code block, so a
Zawgyi document *looks* like valid Myanmar text to software but the code points mean
different things. A `.docx` or digital PDF authored in Zawgyi will extract into text
that renders as garbage, sorts wrongly, and never matches a Unicode search term.

This hits the **MVP's Phase 1 path** — plain text extraction from Word/PDF — with no
OCR involved at all. It also affects anything typed into a form by a user on a
Zawgyi-configured machine.

### Problem 2 — OCR accuracy on Burmese (images and scanned PDFs)

A genuinely harder problem, and the one to be validated in practice.

## Decision

### 1. Normalize to Unicode at ingest — accepted

Every text entry point detects encoding and **converts Zawgyi → Unicode before storage**:
document extraction, form submissions, and pasted text.

- **Store both**: the normalized Unicode text (canonical, used for search/matching/display)
  **and** the original raw text plus a `detected_encoding` field. Normalization is lossy in
  edge cases; keeping the original makes bad conversions diagnosable and re-runnable.
- Normalize **once, at the boundary**. Never scatter conversion through query paths.
- Applies to **all** ingest, not only CVs.

**Tooling:** Google's [`myanmar-tools`](https://github.com/google/myanmar-tools) is the
reference Zawgyi detector/converter and is under the **Apache licence** — permissive, so
compatible with shipping this as closed-source commercial software (verify at adoption).

⚠️ **Known gap:** `myanmar-tools` ships clients for C++, Java, JavaScript, PHP, Ruby and
Python — **there is no official .NET client**, and our backend is .NET 8. Options to
evaluate: P/Invoke the C++ client, port the detector (it is a compact statistical model),
find a maintained community .NET port, or normalize in a small sidecar service. **Resolve
this before Module 2 ingest is built** — it is a real integration cost, not a footnote.

### 2. Burmese OCR — deferred, and the product must work without it

Reported experience with Tesseract's default `mya.traineddata` is weak (missing
punctuation, narrow font coverage, mixed-encoding training data). Community and research
models do substantially better — published work on Myanmar OCR reports ~9% word error
rate from a tuned optical model, dropping to well under 1% with post-OCR correction — so
the capability is achievable, but **not with the default model out of the box**.

Therefore:

- **The image/scanned-PDF OCR path is optional and independently switchable.** If Burmese
  OCR proves unusable, it can be **parked** with no impact on: digital PDF/Word extraction,
  English-language OCR, manual entry, or any other module.
- **The OCR engine sits behind an interface**, with the model/language pack as
  configuration. Swapping the default model for a better-trained one must be a config
  change, not a code change.
- **Do not advertise Burmese OCR accuracy** until measured (below).
- Files that fail OCR land in the **"Skipped"** bucket of the bulk-upload summary with a
  clear reason — never a silent empty profile.

### 3. Search must not assume word boundaries

> ⚠️ The v2.0 architecture draft cites PostgreSQL **"Native Full-Text Search"** as a reason
> for choosing Postgres. That holds for English content only — see
> [ADR-0013](ADR-0013-infrastructure-and-storage.md). Do not plan Module 2.6 around it.

Burmese does not use spaces between words consistently, and PostgreSQL full-text search
has no Burmese configuration. Module 2.6's "keyword search inside CV content" therefore
**cannot** rely on default FTS tokenisation. Plan for trigram indexing (`pg_trgm`) or an
ICU/segmentation-based approach, and decide when Module 2.6 is built. Normalized
(Unicode) text is a precondition either way.

## Evaluation plan — for the practical test

Run before committing to Burmese OCR as a feature:

1. **Sample set:** ~30–50 *real* CVs, deliberately mixed — digital PDF, Word, phone
   photos, flatbed scans; Unicode-authored and Zawgyi-authored; Burmese-only and
   mixed Burmese/English (mixed is the common real case).
2. **Measure separately** — they fail for different reasons:
   - Encoding detection/conversion accuracy (Problem 1)
   - OCR character/word error rate on images (Problem 2)
3. **Decision thresholds** (proposal, adjust after seeing results):
   - Good enough to ship as auto-fill: **WER ≲ 10%** — a reviewer corrects a few fields.
   - Usable only as an assist: WER 10–30% — pre-fill, but expect heavy correction.
   - **Park it:** WER > 30% — correcting is slower than typing, so the feature is negative value.
4. **If parked:** ship digital extraction + manual entry, and revisit with a better-trained
   model or by routing images through the Phase 2 AI layer (a vision-capable model may
   outperform classical OCR here).

The human-confirmation gate from [ADR-0008](ADR-0008-document-extraction-and-ai-profiling.md)
is what makes a mediocre parse tolerable rather than dangerous — but it does not make a
*bad* parse worth shipping.

## Consequences

- Encoding normalization is **not optional** and lands in the MVP — it affects Phase 1.
- Adds an unresolved .NET integration question that must be closed before Module 2.
- Frontend already accounts for display: the design system mandates the
  `Noto Sans Myanmar` fallback and a 1.7 line-height for Burmese. Correct *storage*
  encoding is what makes that rendering meaningful.
- Burmese OCR quality stays an **open risk with a defined test**, not an assumption.
