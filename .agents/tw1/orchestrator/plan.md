# Orchestrator Plan — teamwork run `tw1`

**Started:** 2026-08-12T13:34:24Z · **Driver:** Claude Code `/teamwork` · **Branch:** `develop`

## Request

"ကျန်တဲ့ finding သုံးခုကို ဆက်ပြင်ပေးပါ" — fix the three findings left open after the
AI-fallback fix earlier the same day. Recorded verbatim in `ORIGINAL_REQUEST.md`.

## Fit note — read this before judging the cost of this run

The Orchestrator raised, before Phase 0, that the six-agent gate is a poor fit for this work:

- Findings 1 and 2 are **three lines of change** between them, and the two facts that make them
  safe were verified in two commands before the run started (no test depends on the shipped
  rate-limit value; nothing references the orphan migration's namespace).
- Finding 3 is **documentation**. A Challenger's method is "run it and try to break it", and there
  is nothing to execute against a doc update — that gate can return APPROVE without having done
  anything, which is the empty-instrument failure `NEXT-SESSION.md` warns about.

The user reaffirmed the full run, so it runs as specified. This note exists so a later reader knows
the cost was deliberate and questioned, not accidental. **The Challenger verdicts on M3 should be
read as weak evidence** regardless of what they say.

## Milestones

One Worker owns one milestone. M1 and M2 are independent; M3 runs last because the docs must
describe the state the first two leave behind.

| # | Milestone | Scope | Depends on | Status |
|---|---|---|---|---|
| 1 | Restore the rate limits | `appsettings.json` Login `10 → 60`, PublicApply `10 → 120`; reconcile `appsettings.Development.json`; confirm the per-account throttle really is what stops brute force, as the inline note claims | none | PLANNED |
| 2 | Remove the orphan migration | Delete `Infrastructure/Persistence/Migrations/20260811000000_AddPgTrgmAndSearchIndexes.cs`, keeping the discovered copy under `Infrastructure/Migrations/` | none | PLANNED |
| 3 | Backfill the status docs | `FEATURE-STATUS.md` + `NEXT-SESSION.md` to describe what is actually in the tree: refresh tokens, CV + AI pipeline, Search, Analytics, health/rate-limit, and the real test counts | M1, M2 | PLANNED |

## Gate

Each milestone passes only on **4 × APPROVE + CLEAN** — two Reviewers, two Challengers, one
Auditor. Two remediation loops maximum; a third failure stops the run and goes to the user.

## Explorers dispatched (Phase 1)

| Id | Area |
|---|---|
| `explorer_m1_1` | rate-limit configuration and every consumer of it |
| `explorer_m2_1` | EF migrations layout; verify the orphan is genuinely undiscovered |
| `explorer_m3_1` | what shipped on the backend that the docs do not record |
| `explorer_m3_2` | what shipped on the frontend, plus the structure of the doc files to edit |

## Standing constraints

- Do not commit; the user has not asked. ~190 files were already uncommitted when this run began.
- Never apply an EF migration — M2 deletes a file, it does not touch a database.
- Anything touching auth goes to `security-reviewer` before the run is called done. M1 is a
  rate-limit change on the login path, so it qualifies.
