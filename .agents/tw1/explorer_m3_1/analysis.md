# Blueprint — Milestone 3, part 1: backend inventory

**Explorer:** `explorer_m3_1` · **Filed by:** Orchestrator (subagents cannot write files here)

## Verified test numbers (explorer ran `dotnet test backend/RecruitOps.sln` itself)

```
RecruitOps.Domain.Tests: 51 passed, 0 failed
RecruitOps.Api.Tests:   433 passed, 0 failed
```

**484 backend tests.** The docs say 226. Stale by 258.

---

# ⚠️ Four features are substantially hollow

Each of the following was **verified directly by the Orchestrator**, not relayed. They are not doc
staleness — they are shipped features that do not do what their name says.

## 1. The AI endpoints send the provider no candidate or job data

`Infrastructure/Services/AiIntegrationService.cs` lines 37, 43, 49:

```csharp
return _claudeService.MatchCandidateAsync(request, null, null, ct);
return _geminiService.GenerateExecutiveSummaryAsync(request, null, null, ct);
return _geminiService.PrepareDocumentAsync(request, null, null, ct);
```

The two `null`s are `candidateProfileData` and `jobPostingData`. **Even with a valid API key**,
`match-candidate` sends Claude nothing but two GUIDs — the composed prompt literally reads
`"Candidate profile: \nJob posting: "`.

This is the same wound as the fabrication fixed on 2026-08-12, one layer down. That fix stopped the
endpoints inventing an answer when no key was configured. It did not — and could not — make the
configured path meaningful, because the facade sitting above the clients never fetches the data the
clients are built to accept. The hardcoded 88% stub was concealing that the real path is equally
empty.

**There is also no authorization anywhere in the AI call path.** No `IApplicationAccess`, no
`IDepartmentAccess`, in `AiController`, `AiIntegrationService`, or either client. Harmless only
because no real data flows today. The moment someone wires the DB fetches in — the obvious next
step — ADR-0003 and ADR-0018 must be built from scratch with no precedent in that code path, on a
whole new endpoint family rather than one sibling of three.

## 2. "Confirm & Apply to Profile" calls a route that does not exist

`frontend/internal/src/lib/api.ts:186-191` issues `PUT /applications/{id}/profile`, and
`CandidateSlideOver.tsx` calls it from the real UI, not just a test.

- `ApplicationsController` has four actions: `stage`, `history`, resume upload, resume download.
  **No `profile` action.** Grep for `profile` across the controllers returns nothing.
- `Application/Interfaces/ICandidateService.cs` is an **empty interface** carrying
  `// TODO: define Candidate use-case operations`. No implementation, no DI registration.
- `CandidatesController` returns `Array.Empty<object>()`.

Clicking Confirm in the UI today 404s. ADR-0008 makes human confirmation mandatory before AI output
reaches a candidate record — **the write side of that gate is unbuilt.**

## 3. OCR does not exist; image uploads return a placeholder string as "CV text"

`.png`, `.jpg`, `.jpeg` are in the upload allowlist. `DocumentTextExtractor.ExtractFromImageOrScannedAsync`
reads the PNG header for its dimensions and returns:

```csharp
return $"Image Document: {fileName} | Format: {extension} | Dimensions: {width}x{height} | Size: {ms.Length} bytes";
```

That string is the "extracted text". It then flows onward into search indexing and AI parsing as if
it were a résumé. The same fallback fires for a scanned PDF with no text layer.

The gap itself is known — `NEXT-SESSION.md` lists OCR as an unbuilt prerequisite. What is **not**
recorded is that the pipeline accepts the file and manufactures plausible-looking text rather than
refusing it.

## 4. Search cannot use the nine trigram indexes the migration creates

`SearchService.cs` does `candQuery.ToListAsync(ct)` (line 236, and the same shape at 212, 335) —
pulling **entire tables** into memory — then filters in C# with
`string.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)` (262-293).

No `EF.Functions.ILike`, no similarity operator, no SQL that a GIN trigram index could serve. The
migration builds nine indexes the application cannot reach. Fine on dev data; linear degradation in
production. **Do not describe search as "pg_trgm-backed"** — the indexes exist, the code does not
use them.

---

# What genuinely shipped and works

## Refresh tokens (`be7b1ff`)

`RefreshToken` entity (`ITenantScoped`), migration paired with its `.Designer.cs`.
`POST /api/auth/{login,refresh,revoke}`. Access token 8h, refresh 14 days.
**Rotation is real** — refresh revokes the presented token and issues a new one via a
`ReplacedByToken` chain. **Reuse detection is implemented** — presenting an already-revoked token
revokes every active token for that user. Revoke returns 204 even for unknown tokens, so there is no
existence oracle. 6 tests in `AuthRefreshTokenTests.cs`.

Tokens travel in the JSON body, **not** an httpOnly cookie — so `NEXT-SESSION.md`'s backlog item
"Refresh token + httpOnly cookie option" is half done: issuance shipped, cookie transport did not.

## Analytics — the best-shaped code found in this survey

All five public methods on `AnalyticsService` funnel through one private
`GetAllowedDepartmentIdsAsync` (25-42) applying ADR-0018 then ADR-0003. That is exactly the
"one place, every sibling calls it" shape `NEXT-SESSION.md` asks for — **point future Workers here
as the reference implementation.** The conversion funnel is computed from `ApplicationStageHistory`,
redeeming the promise that the append-only trail is Module 5's raw material.

No `[HasPermission]` gating — class-level `InternalUser` policy only. No ADR is specific to
analytics or reporting; it is governed only by cross-cutting ADR-0003/ADR-0018.
Tests: `AnalyticsApiTests.cs` (14), `AnalyticsAdversarialTests.cs` (4).

## AI provider gating (fixed 2026-08-12) — matches the CHANGELOG

`AiController`'s five actions funnel through one `RunAsync<T>` helper (34-69). `EnableFallback`
defaults `false`; `RequireApiKey`/`X-Require-Api-Key` confirmed gone. `ShouldServeDevelopmentStub()`
is the single gate; with a key configured, any fault raises `AiProviderUnavailableException` → 502
and never a stub. Cancellation is correctly distinguished from timeout. Verified independently here.

## Myanmar script normalization

`MyanmarScriptNormalizer.cs` (205 lines) is a **hand-rolled heuristic detector and character
mapper — not a wrapper around Google's `myanmar-tools`.** FEATURE-STATUS's open gap "No .NET client
for myanmar-tools — integration undecided" should be recorded as **a decision made by another
route**, not left looking open. Accuracy against a real Burmese corpus is unverified from code
alone. Applied consistently in `DocumentTextExtractor`, `SearchService`, and both AI clients.

## Operational readiness

`/healthz` (`[AllowAnonymous]`, no `/api` prefix) checks DB, storage, memory, uptime.
`SecurityHeadersMiddleware` sets all four headers, wired first. Startup runs `MigrateAsync` then
`SeedPermissionsAndRolesAsync` unconditionally; tenant/admin seed only in Development.

**Gap found here:** `GetHealthz` always returns `Ok(response)` — **HTTP 200 even when the body says
`"status": "Unhealthy"`.** A monitor checking only the status code (k8s liveness probes do this by
default) reports the service healthy while the database is down.

## Other warts

- **Two identical `IBulkResumeService` interfaces**, `Application/Common/Interfaces/` and
  `Application/Interfaces/`, byte-identical but for namespace. `BulkResumeService` implements both;
  `DependencyInjection.cs:109-110` registers both. Only the `Application.Interfaces` copy is
  consumed. Both arrived in the same commit — two parallel agents each creating it, neither
  deleting.
- **Bulk upload runs in-process**, not on a background job runner. No queue or hosted service exists
  anywhere in `Infrastructure/`. The prerequisite `NEXT-SESSION.md` said was missing is still
  missing; the endpoint just runs synchronously and hands back a batch id.
- **`LoginResponse` TS/C# contract drift** — `packages/types` marks `refreshToken` optional though
  C# always sends it, and declares `isSuperAdmin`, `tenantId`, `activeTenantId`, `activeTenantName`,
  none of which exist on the C# record.

---

## Open Questions — Orchestrator resolutions

1. **Should M3 describe post-M1/M2 state or current state?** → Post-fix state, re-verified at
   writing time. M3 runs last precisely so it can.
2. **Are the confirm-profile 404 and the OCR placeholder for M3 to implement?** → **No.** M3 records
   them as known gaps. Implementing them is a separate decision that belongs to the user.
3. **How should the docs frame the AI flow's missing data-fetch and missing ADR-0003/0018?** →
   Escalated to the user; it changes what "Module 2.4 Smart Match ✅" can honestly claim.
4. **Duplicate `IBulkResumeService`** → record as a known wart; do not clean up inside a docs
   milestone.
5. **Is `/healthz` returning 200-when-unhealthy intentional?** → Undetermined from code or ADR-0004.
   Record as an open question, do not assert it is a bug.
6. **Exact per-file test counts** → the explorer counted `[Fact]`/`[Theory]` attributes, which
   undercounts `[InlineData]` rows. Use `dotnet test --list-tests` if the docs need precision, or
   write defensible buckets rather than invented breakdowns.
