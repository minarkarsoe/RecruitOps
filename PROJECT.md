# Project: RecruitOps — close the gap between what the docs claim and what runs

**Run:** `tw1` (Claude Code `/teamwork`) · **Started:** 2026-08-12 · **Branch:** `develop`

## What this run is

It began as three loose ends after the AI-fallback fix: a rate-limit regression, an orphaned
migration, and stale status docs. The Phase 1 survey established that the third one is not a
documentation problem — **four shipped features do not do what their names claim**, each verified
directly against the source rather than taken from an agent's report.

The user reviewed the four and chose to fix them in this run rather than record them and move on.

## Scale, stated plainly

Eight milestones, each with a full gate (Worker → 2 Reviewers + 2 Challengers → Auditor). M3 alone
is a security-sensitive change to an endpoint family that currently has **no** authorization in its
call path. This is larger than the "one feature per session" rhythm `CLAUDE.md` prescribes and will
likely outlive this session; `.agents/tw1/orchestrator/progress.md` is written so a later session
can resume from any gate.

## Milestones

| # | Milestone | Why | Depends on | Status |
|---|---|---|---|---|
| 1 | Restore the rate limits | An office behind one NAT trips 429 on login. Four files, not one — the C# defaults and `appsettings.Development.json` regressed too, and Development is what `docker compose` actually runs | none | PLANNED |
| 1.5 | **Containerize: prove the stack actually runs** | The M1 gate found `dotnet publish` emitting an app missing `Application.dll` and `Infrastructure.dll` — a crash-looping container behind 507 green tests. The Dockerfile's publish stage uses `--no-build`, so the image reproduced the defect exactly. Only the artifact can catch this class of fault; the test suite structurally cannot | M1 fix | IN PROGRESS |
| 2 | Delete the orphan migration | Dead duplicate EF never discovers | 1.5 | PLANNED |
| 3 | **AI: fetch real data + apply authorization** | `AiIntegrationService` passes `null, null`, so a configured key analyses two GUIDs. No `IApplicationAccess`/`IDepartmentAccess` anywhere in the AI path | none | PLANNED |
| 4 | `PUT /applications/{id}/profile` | ADR-0008's mandatory human-confirmation gate 404s; `ICandidateService` is an empty TODO | none | PLANNED |
| 5 | Stop fabricating CV text | Image and scanned-PDF uploads return `"Image Document: … Dimensions: 800x1200"` as extracted text, which flows into search and AI parsing | none | PLANNED |
| 6 | Make search use its indexes | Whole tables loaded via `ToListAsync`, filtered in C#; nine trigram GIN indexes unreachable | none | PLANNED |
| 7 | Frontend gating consistency | Zero `hasPermission` in `features/pipeline`; Bulk Upload ungated beside three gated siblings; `/analytics` has no `RequirePermission`; `ADR-0021` cited in two files and does not exist | none | PLANNED |
| 8 | Backfill the status docs | Docs say 226 backend / 60 frontend tests; reality is 484 / 318, plus everything above | 1–7 | PLANNED |

M8 runs last so it describes the state the other seven leave behind, and is re-verified at writing
time rather than trusting any snapshot.

## Decision the user still owns — M5

Real OCR needs an engine chosen on cost, PII residency and Burmese accuracy, and `NEXT-SESSION.md`
says that needs an ADR before code. **M5 therefore does not implement OCR.** It makes the pipeline
honest: stop manufacturing text that downstream code cannot distinguish from a real résumé. Whether
that means refusing image uploads outright or storing them with an explicit "not extracted" marker
is the Worker's design call, reviewed at the gate. Choosing an OCR vendor stays open.

## Interface contracts

### M4 — confirm parsed profile
`PUT /api/applications/{applicationId}/profile`
- Auth: goes through `IApplicationAccess` (**not** `IDepartmentAccess`), and `CanWrite` must be
  true — a panel member's grant is read-only.
- Out-of-scope rows return **404, not 403**, so existence is not leaked.
- Request: `ConfirmParsedProfileRequest` — already defined in `packages/types/src/index.ts`; the
  backend DTO must match the shape the frontend already sends.
- Response: 204.

### M3 — AI with real data
No route changes. `AiIntegrationService` gains repository access and populates
`candidateProfileData` / `jobPostingData` before calling either client. Every method applies
`IApplicationAccess` first; out-of-scope returns 404. The 402 / 502 / `X-Ai-Simulated` contract
established on 2026-08-12 is unchanged and must stay covered.

## Ground truth as of 2026-08-12 (measured, not claimed)

| Suite | Count |
|---|---|
| `dotnet test backend/RecruitOps.sln` | **484** — 51 Domain + 433 Api, 0 failing |
| `npm run test --workspace @recruitops/internal` | **318** across 39 files |
| `npm run typecheck` | 0 errors, 4 workspaces |

## Blueprints

`.agents/tw1/explorer_m1_1/analysis.md` (rate limits) · `explorer_m2_1` (migration) ·
`explorer_m3_1` (backend inventory, the four hollow features) · `explorer_m3_2` (frontend inventory,
doc structure).

⚠️ Read blueprints only from `.agents/tw1/`. The top level of `.agents/` holds ~190 directories from
earlier runs whose names collide with this run's agent ids and whose contents describe different
work entirely.
