# Blueprint — Milestone 3, part 2: frontend inventory + doc structure

**Explorer:** `explorer_m3_2` · **Filed by:** Orchestrator (subagents cannot write files here)

## Verified ground truth (explorer ran these itself, 2026-08-12)

```
npm run typecheck                              → 0 errors, all 4 workspaces
npm run test --workspace @recruitops/internal  → 39 files passed, 318 tests passed
```

**39 files / 318 tests is the number to write into both docs.** `FEATURE-STATUS.md` currently says
189 tests / 22 files; `NEXT-SESSION.md` says 60. The explorer did **not** run `dotnet test` — the
backend number comes from `explorer_m3_1`, not from here.

## Half A — what shipped on the frontend

### Candidate 360 / pipeline

- `CandidateSlideOver.tsx` (675 lines) — six tabs: overview, ai, cv, history, scorecards, notes
  (489-499). Clickable Smart Match badge in the header (456-478) jumps to the `ai` tab.
- `SmartMatchBreakdown.tsx` (307) — score, strengths, gaps, per-criterion breakdown, suggested
  questions. Exports `getMatchBadgeConfig` (14-43), reused by `CandidateSlideOver.tsx:26,436`.
- `ExecutiveSummaryPanel.tsx` (331) — EN/MY/Bilingual toggle (141-174), Internal/Client audience
  toggle (180-204), copy and `.md` export (57-106).
- **The CV viewer is not its own file.** It is `CvAndDocumentsTab`, defined inside
  `CandidateSlideOver.tsx:80-411`. Do not look for `CvViewer.tsx`. Covers drag/drop with a
  10MB/format guard (104-114), raw-extracted-text beside a human-review form, a Zawgyi→Unicode badge
  when `extractionResult.isZawgyiNormalized` (271-273), the ADR-0008 human-confirmation gate via
  `resumeApi.confirmParsedProfile` (145-167), and original-document download (169-183, 401-408).
- `BulkCvUploadModal.tsx` (299) — up to 50 files, polls `getBulkResumeStatus` every 1.5s (106-125).
  Mounted from `pages/JobPostingDetailPage.tsx:411-418`.

**Error handling** — identical in both AI panels; copy this shape for any future AI panel. A 402
sets `isApiKeyMissing` and renders an amber "AI Features Unconfigured" banner
(`data-testid="smart-match-402-banner"` / `"executive-summary-402-banner"`) with the action button
disabled; any other `ApiError` renders a red banner with a working Retry.

### `features/ai/` is absent — but the row is not "not started"

Confirmed by directory listing and a repo-wide glob: `AiDocumentPrepModal.tsx`,
`InlineTranslator.tsx` and `TranslatedTextField.tsx` do not exist anywhere.

**However** `aiApi.prepareDocument` and `aiApi.translateBurmese` already exist and are typed at
`frontend/internal/src/lib/api.ts:230-242`, with `lib/ai.test.ts` covering the client (7 tests). The
backend endpoints exist too. So the accurate FEATURE-STATUS wording is **"backend + API client done,
UI components unbuilt"** — not "not started".

⚠️ Root `PROJECT.md` lists all three files in its Code Layout section as though they were built.
That file is wrong on this point. It is another instance of why `.agents/*` and generated plan files
are leads, not evidence.

### Search

- `features/search/useSearch.ts` (203) — **300ms debounce** (`debounceMs = 300`, 49; effect 101-110).
  Clearing the query bypasses the debounce (67-81). One `AbortController` per request, aborting the
  previous on every param change (113-166) — the only cancellable-fetch pattern in the repo.
- `packages/ui/src/CommandPalette.tsx` (392) — presentational and controlled. Merges static items
  with `searchResults`, dedupes by id (116-127), sorts by a fixed `CATEGORY_ORDER` (78). Does not
  call `useSearch` itself.
- `components/AppLayout.tsx` owns the Ctrl+K listener (40-55) and the wiring (15-27, 178-191). The
  static nav items are filtered by `hasPermission` (57-156); dynamic search results are only as safe
  as `/api/search` makes them server-side.
- Types at `packages/types/src/index.ts:905-940` (appended inline, not a dedicated file).
- No dedicated `/search` results page — palette only.

⚠️ `M2_Empirical_Verification.test.tsx` contains a test named "EMPIRICAL BUG TEST: verifies whether
keyboard selection matches visual category ordering". The explorer did not read its assertions.
**Check what that test actually concludes before describing search as done without caveat.**

### Analytics

- `pages/AnalyticsPage.tsx` (111) composes `useAnalytics()` with `KpiCardSection`,
  `TimeToHireChart`, `FunnelChart`, `SourceDistributionChart`, `CustomReportBuilder`.
- `features/analytics/useAnalytics.ts` — `fetchDashboard` (parallel `Promise.all` of 4 GETs),
  `runReportQuery`, `downloadReportCsv`.
- Types in `packages/types/src/analytics.ts` (69 lines), re-exported at `index.ts:901`.

⚠️ The explorer read `useAnalytics.ts` but **not** `analyticsApi.ts`, so whether CSV export triggers
a real file download or merely calls the endpoint is **unverified**. Do not write "CSV export works"
as fact without checking.

## Half B — where things go in the docs

### `docs/status/FEATURE-STATUS.md` (474 lines)

| Lines | Section | What to do |
|---|---|---|
| 3 | "Last updated" header | Stale: says 2026-08-03, 228 backend / 189 frontend. Rewrite with real counts. |
| 5-22 | Blockquote summary | Bullets are `> <emoji> **bolded claim**`. Add AI / Search / Analytics / Deployment bullets in that voice. |
| 24-37 | "Summary by module" table | **Highest-value edit in the file.** Row 31 still says 2.3 OCR / 2.4 Smart Match / 2.6 search "not started" — all three are built. Row 34 still has Module 5 Reporting & Analytics as ⬜ — it is built. |
| 39-52 | "Delivery readiness (ADR-0004)" | Belongs to the backend/deployment survey — cross-check with `explorer_m3_1`, do not fill in blind. |
| 54-244 | "Built in detail" | One `### <icon> <name>` subsection per feature, prose with inline code refs. Match the specificity of the existing entries (e.g. 173-193), not a generic paragraph. |
| 245-264 | "Test inventory" | 3-row table. Replace the frontend `27` with `318`. The existing row breaks its count down (14 + 7 + 6); **do not fabricate false precision** if you cannot tally the 39 files that way — a defensible bucket beats an invented breakdown. |
| 266-428 | Narrative sections | Reserve for genuinely investigative findings (the ADR-0019 story). Routine "feature shipped" notes do not belong here. |
| 430-473 | "Known gaps & risks" table | `\| Issue \| Severity \| Where \|`. ⚠️ **Stray blank lines split this table into fragments around 436-437 and 463-464** — insert at the end of a contiguous fragment, not mid-table. |

### `docs/status/NEXT-SESSION.md` (244 lines)

| Lines | Section | What to do |
|---|---|---|
| 3 | Header | Stale: "Milestones 1–5 Complete · 226 Backend + 60 Frontend Tests". |
| 12-29 | "Where the product is" | Prose plus a fenced flow diagram. Append arrow-steps; do not convert to a table. |
| 45-64 | "'The stack came up' is not 'the screens are correct'" | Reusable template — **nothing in this run was verified in a real browser**, only via `npm test` and source reading. That gap is real and this section exists to record it. |
| 66-78 | "What's built" table | Column 1's header is deliberately empty (`\| \| State \|`) — not a mistake, do not "fix" it. Row 71 mirrors the stale module row; row 76's test counts are the most out-of-date line in either doc. |
| 80-131 | Backlog | Item 3 "Module 2.3 — CV upload + OCR" (107-117) is now largely **done**. Confirm the OCR/Zawgyi claims with `explorer_m3_1` before closing it fully. |
| 132-244 | "Things will bite you" | Flat bullets, bold lead. None of the ~20 existing items concern AI, Search, Analytics or CV upload. |

### `docs/status/CHANGELOG.md` (1291 lines)

Confirmed newest entry is `## 2026-08-12 (latest)` — the AI-fallback fix. Its "Still open" paragraph
(51-54) is near-verbatim reusable for the `X-Ai-Simulated` gap.

### ADRs to cite (20 files exist — cite, do not invent)

ADR-0008 (AI optional, key-gated, human confirmation) · ADR-0009 (Myanmar script) · ADR-0013
(storage) · ADR-0003 (department scoping) · ADR-0018 (candidate data).

## Code-level findings — NOT docs issues

These are outside M3's scope. Recorded here, reported to the user, **not fixed by this run.**

1. **`ADR-0021` is cited in two source comments and does not exist** —
   `packages/types/src/index.ts:712` and `frontend/internal/src/lib/api.ts:207` both say
   "(ADR-0021)". A repo-wide grep for `ADR-002[1-9]` finds only these two references and no file.
   **Do not create an ADR-0021 to make the comment true.**
2. **The AI panels have no client-side permission gating.** `features/pipeline` contains zero
   `hasPermission` calls, while every sibling action button on `JobPostingDetailPage.tsx` (257, 277,
   294, 363) is wrapped in one. The backend does gate all five AI endpoints with
   `[HasPermission("permission:ai:...")]`, so this is a UX inconsistency, not an authorization hole
   — a user without the permission sees a button that will 403.
3. **The "Bulk Upload CVs" button has no `hasPermission` guard** — the one button on that page
   without one, next to three that have it.
4. **`/analytics` has no `RequirePermission` wrapper** (`App.tsx:75`), unlike `/users` (58-65) and
   `/roles` (66-73); the sidebar gates it on `permission:requisitions:requisitions:read`
   (`Sidebar.tsx:64-73`). No `permission:analytics:*` permission appears to exist.

Items 2–4 are the same shape as the recurring defect `NEXT-SESSION.md` already documents: **a rule
applied to some siblings and not others.** That is why they are worth recording even though this
milestone will not fix them.

## Open Questions — Orchestrator resolutions

1. **Backend test count** — comes from `explorer_m3_1`. Do not write a backend number sourced from
   here.
2. **Is the Analytics permission reuse intentional?** Undetermined. → Record as a Known gap phrased
   as an open question; do not assert it is a bug.
3. **Fix the dangling ADR-0021 reference?** → **Out of scope.** Record it; do not write the ADR and
   do not edit the comments in a docs milestone.
4. **What milestone numbering should `NEXT-SESSION.md` use?** The AI / Search / Analytics /
   Deployment work does not map onto the original Module 1–8 axis. → Keep the **module** axis, which
   is the product's real structure, and describe the recent flows by name rather than renumbering.
   Renumbering would invalidate every existing cross-reference.
5. **Delivery-readiness rows** — owned by `explorer_m3_1`; silence here is not evidence.
