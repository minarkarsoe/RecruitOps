# Handoff — worker_m1: restore the rate limits

**Filed by:** Orchestrator from the Worker's reply (subagents cannot write report files).

## What changed — four files, all hand-edited, no git-based reverts

1. `backend/src/Api/Auth/LoginRateLimitOptions.cs` — `PermitLimit` 10 → **60**, rationale doc
   comment restored (NAT/office sizing, successful logins count too, the per-account throttle is the
   actual brute-force control).
2. `backend/src/Api/Auth/PublicApplyRateLimitOptions.cs` — `PermitLimit` 10 → **120**, rationale
   restored (deliberately higher than login; a shared job link produces a genuine burst).
3. `backend/src/Api/appsettings.json` — Login → **60**, PublicApply → **120**. Existing `// note`
   fields and the `AI:` block untouched.
4. `backend/src/Api/appsettings.Development.json` — same two numbers, plus one `// note` per block
   recording that this is the file `docker compose` actually uses. `AI:` block untouched.

`WindowSeconds` untouched everywhere. `Program.cs`, `RateLimitPolicies.cs`, the controllers and
`LoginThrottle` untouched.

## Verification — the Worker's own output

```
dotnet build backend/src/Api   → Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test backend/RecruitOps.sln
  Passed! - Failed: 0, Passed:  51, Total:  51 - RecruitOps.Domain.Tests.dll
  Passed! - Failed: 0, Passed: 433, Total: 433 - RecruitOps.Api.Tests.dll
```

**484 passing, 0 failing** — matches the pre-change baseline exactly, which is the expected result:
no test reads the shipped value.

## Deviation the Worker declared

It first wrote the Login doc comment with `<see cref="ILoginThrottle"/>`, then reverted to plain
prose because `ILoginThrottle` lives in `RecruitOps.Application.Interfaces` and the file has no
`using` for it — an unresolvable `cref` risks a compiler warning. Build confirms 0 warnings.
Declared rather than hidden; the substance of the comment is unchanged.

## What the Worker asked reviewers to check hardest

- That the `appsettings.Development.json` values are genuinely what reaches `docker compose up` —
  it did not run compose, relying on the blueprint's verified claim about `docker-compose.yml:68`.
- That the `AI:` blocks in both JSON files are unchanged from before its edits.

Both are legitimate self-flagged limits rather than claims of completeness.
