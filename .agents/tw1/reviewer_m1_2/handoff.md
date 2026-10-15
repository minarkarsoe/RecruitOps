# Reviewer m1_2 — configuration consistency, collateral damage, comment accuracy

**Filed by:** Orchestrator from the Reviewer's reply. **VERDICT: APPROVE** · 🔴 0 · 🟡 2 · 🟢 2

## Collateral damage — clean, verified against the tree

- **Both `AI:` blocks survived.** `git diff` on `appsettings.json` shows exactly one hunk versus
  HEAD — the `AI:` block. The `RateLimit` block is **byte-identical to HEAD**, meaning the Worker
  restored 60/120 *and* the original `// note` text exactly: no drift, no paraphrase.
- Semantically intact too, which git cannot check since the `AI:` block is uncommitted:
  `appsettings.json` has `EnableFallback: false`, Development has `true`, matching the options
  classes. **The 2026-08-12 fabricated-candidate fix is not re-enabled.**
- **mtime sweep confirms exactly four files** were touched in the Worker's window. Nothing else in
  `backend/src`, `backend/tests`, `docs/`, or the repo root.
- `Program.cs` untouched by this milestone; the per-request `IOptions<T>` resolution at `:94-96` and
  `:112-114` is intact — the previously-shipped "read it into a local" bug was not reintroduced.
- `dotnet build backend/src/Api` → 0 warnings, 0 errors, re-run by the Reviewer.

## All four sites agree, and there is no fifth

A repo-wide sweep for `PermitLimit` / `RateLimit__` / `"RateLimit"` across `*.json`, `*.yml`,
`*.env*`, `Dockerfile*`, `*.cs` finds **no other configuration surface**: no
`appsettings.Production.json`, no `infra/`, no `RateLimit__*` in compose, nothing in `.env.example`.
Remaining hits are test-local overrides only.

## Comment accuracy — every claim checked against code

The compose claim, the ADR-0016 citation, the `ReverseProxy:TrustForwardedHeaders` key, "the fixed
window counts successful logins", and "brute force is blocked by the per-account throttle" all
verified true. The restored prose is content-for-content equal to the pre-regression text. No stale
"10 requests/min" claim survives anywhere in `docs/` or the frontend.

## 🟡 1 — the Development duplicate is regression residue, now made permanent

**This is a challenge to an Orchestrator ruling, not a Worker defect.** The Worker did as directed.

`appsettings.Development.json:17-28` did not exist before the hardening run created it at 10/10. The
Worker set it to 60/120 rather than deleting it — per Open Question 2, which the Orchestrator
resolved as "yes, add notes". So the value now lives in **four** places instead of three, and the
Development layer wins under compose.

> Concrete failure: an operator with a 400-seat office follows ADR-0016, raises
> `RateLimit:Login:PermitLimit` to 200 in `appsettings.json`, restarts `docker compose up`, and
> still gets 60 — silently, with a config file in front of them that says 200.

Deleting the Development block would make the base file authoritative again and remove the surface
entirely. **Orchestrator accepted this — see the remediation note below.**

## 🟡 2 — no CHANGELOG entry for a shipped config change

`CLAUDE.md` rule 2. `CHANGELOG.md:731` says 120/60s, so the doc is now correct again — but nothing
records that the value regressed to 10 and was restored. The next person seeing 120 in the CHANGELOG
and 10 in a deployed config has no trail, which is how this survived an audit the first time
(`.agents/auditor_flow3/audit.md:22` marked the 10-req/min behaviour PASS). → folded into M8.

## 🟢 3 — the Worker's `cref` reasoning was wrong; the outcome was right

`RecruitOps.Api.csproj` does not set `GenerateDocumentationFile`, so CS1574 is never emitted for this
project — the warning the Worker avoided could not have occurred. A fully-qualified `cref` would
also have resolved without a `using`. The call still lands correctly for a better reason: an Api
options class advertising an Application interface is the cross-layer reference `CLAUDE.md` asks to
avoid. Noted so the reasoning is not reused as precedent.

## 🟢 4 — nothing pins the shipped values

Superseded during the gate by `ChallengerM11RateLimitTests.cs`. Flagged only so it is not scheduled
twice.

## Process observation for the Orchestrator

The Reviewer noticed `Program.cs` and a new test file changing **during its review** — a concurrent
Challenger. It re-diffed and confirmed no harm. Real observation: within a gate, Challengers may add
tests while Reviewers read, so the tree moves under them.
