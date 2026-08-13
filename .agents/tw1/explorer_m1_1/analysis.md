# Blueprint — Milestone 1: restore the rate limits

**Explorer:** `explorer_m1_1` · **Filed by:** Orchestrator (subagents cannot write files here)
**Orchestrator verification:** the three load-bearing claims below were re-checked directly —
`docker-compose.yml:68` sets `ASPNETCORE_ENVIRONMENT: Development`, `ADR-0016` line 33 states
"60 requests / 60s", and both options classes currently default to `10` with their rationale
comments deleted. Confirmed.

## Bottom line

The finding as originally written was **incomplete**. It named `appsettings.json` only. Two more
things are wrong, and one of them means the original fix would not have reached the deployed system.

1. **The C# option-class defaults were changed too** — `LoginRateLimitOptions.PermitLimit` 60 → 10,
   `PublicApplyRateLimitOptions.PermitLimit` 120 → 10 — and the XML doc comments explaining *why*
   they were 60 and 120 were **deleted**, replaced with "Fixed to 10 reqs/min per IP (Requirement
   R1/Flow 3)."
2. **`appsettings.Development.json` gained a brand-new `RateLimit` block at 10/10**, and
   `docker-compose.yml:68` runs the backend with `ASPNETCORE_ENVIRONMENT: Development`. Development
   config layers over the base file and wins. **So `appsettings.Development.json` — not
   `appsettings.json` — is the value actually in force under `docker compose up`.** Fixing only the
   base file leaves the compose deployment exactly as broken as it is today.

## Files to change — complete list

| # | File | Change |
|---|---|---|
| 1 | `backend/src/Api/Auth/LoginRateLimitOptions.cs:14` | `PermitLimit` 10 → **60**, restore the deleted rationale comment |
| 2 | `backend/src/Api/Auth/PublicApplyRateLimitOptions.cs:8` | `PermitLimit` 10 → **120**, restore the deleted rationale comment |
| 3 | `backend/src/Api/appsettings.json:20,25` | Login → **60**, PublicApply → **120**. Leave the `// note` fields; they already say the right thing |
| 4 | `backend/src/Api/appsettings.Development.json:19,23` | same two numbers, plus a short note (see Decision 2) |

Nothing else. Not `Program.cs`, not `RateLimitPolicies.cs`, not either controller, not
`ILoginThrottle`/`LoginThrottle`, not any test file.

## The note's claim is true — verified, not assumed

The config comment says brute force is stopped by the per-account throttle, not by this number. It
is:

- `ILoginThrottle` / `LoginThrottle.cs` — `MaxFailures = 5`, 15-minute lockout, keyed by SHA-256 of
  the normalised email so failures against non-existent accounts are tracked too (ADR-0016, so the
  429 cannot become an account-enumeration oracle).
- Registered singleton at `Infrastructure/DependencyInjection.cs:50`.
- `AuthController.cs:32-43` checks `RetryAfter(email)` **before** attempting authentication, ahead
  of the ASP.NET limiter entirely.
- `ADR-0016` states the per-IP default as 60 requests / 60s in its own decision table.

So restoring 60/120 does not weaken the login path — the mechanism that actually stops brute force
is untouched by this milestone.

## Tests: restoring 60/120 breaks nothing

Every test class uses `CustomWebAppFactory` (or `NoAiFallbackWebAppFactory`, which extends it), and
that factory overrides both limits to `10000` at `CustomWebAppFactory.cs:76-77` — because TestServer
has no remote address, so every request in the run shares one `"unknown"` partition.

The five tests that actually exercise rate limiting each set their own value via
`WithWebHostBuilder`, which is applied after the factory's own config and therefore wins:

- `Challenger1AdversarialTests.cs:65` (`10`), `:93` (`2`)
- `OperationalHealthAndSecurityTests.cs:142` (`10`), `:168` (`10`), `:194` (`10`)

**None of them read the shipped value.** No other file under `backend/tests` references
`RateLimit`, `PermitLimit`, or either options type.

## Traps

1. **Fixing `appsettings.json` alone does not fix the deployed system.** See Bottom line §2. This is
   the trap that would have made a "successful" milestone change nothing users experience.
2. **The limit is resolved per-request from `IOptions<T>`, never read into a local at startup**
   (`Program.cs:94,112`). This is deliberate: a previous security-review pass found that reading it
   into a local silently defeated test overrides, because `WebApplicationFactory` adds configuration
   during `Build()`, after top-level statements have run. Do not "simplify" it back — that is a
   previously-shipped, previously-fixed bug.
3. **`PublicApplyRateLimitOptions.cs` is untracked.** `git checkout -- backend/src/Api/Auth/` will
   not touch it, so a git-based revert would restore the Login default and silently leave
   PublicApply at 10. Edit it by hand.
4. **Do not revert either JSON file wholesale.** Both interleave this regression with the legitimate
   `AI:` configuration added by the AI-fallback fix earlier today. `git checkout HEAD -- <file>`
   would delete that too. Hand-edit the two `PermitLimit` values.
5. **`WindowSeconds` never regressed** — it is 60 everywhere already. Do not touch it.

## Where the regression came from

`ORIGINAL_REQUEST.md:117` asked that the limiter "blocks excessive requests (>10 reqs/min)". The
deployment-hardening run read that as "exactly 10" and hardened to it, without cross-checking
ADR-0016 or the comment it was overwriting. `.agents/auditor_flow3/audit.md:22` then marked the
10-req/min requirement **PASS** — the audit confirmed the literal number and never noticed it had
inverted a documented decision.

Note that 60 already satisfies "blocks excessive requests (>10 reqs/min)": a limit of 60 does block
requests beyond 60/min. The stricter reading was a choice, not a requirement.

`docs/status/CHANGELOG.md:731,762` still documents 120/60s and 60/60s as current — more evidence
this was undocumented drift rather than an intended change.

## Open Questions — resolved by the Orchestrator

1. **Is 60/120 the intended fix, given Flow 3's "10 reqs/min" acceptance criterion?**
   **Yes — restore 60/120.** ADR-0016 is the governing decision record and was never amended; the
   hardening run overwrote a documented decision on a literal reading of ambiguous acceptance text
   that 60 already satisfies. Where an ADR and an acceptance checkbox disagree, the ADR wins until
   someone writes an ADR superseding it.
2. **Should `appsettings.Development.json` get `// note` fields?**
   **Yes, one short note per block**, precisely because it is the file in force under compose — the
   next reader needs to know the numbers are deliberate there too.
3. **Should the C# defaults be restored even though nothing falls back to them?**
   **Yes, in scope.** A code default of 10 under a comment citing a requirement that no longer holds
   is exactly what reintroduces this bug the next time someone reads the class instead of the
   config.
