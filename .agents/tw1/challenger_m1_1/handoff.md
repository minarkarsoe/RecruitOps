# Challenger m1_1 — does the restored limit take effect at runtime?

**Filed by:** Orchestrator from the Challenger's reply. **VERDICT: APPROVE** · defects: 2, both
test-integrity, both pre-existing, both now closed.

**Orchestrator verification:** re-ran the suite independently — **51 + 445 = 496 passing, 0
failing**, matching the Challenger's claim. `git diff --stat` on `Program.cs` shows exactly the 3
insertions it carried at session start, so the mutation was fully reverted.

## The product code is correct

New tests drive real HTTP at 11 / 25 / 60 login permits and 120 apply permits, asserting request N
is not 429 and N+1 is. **The restored 60/120 provably takes effect in both directions.** Login and
public-apply hold independent buckets; both JSON files bind to 60/60s and 120/60s (the `"// note"`
keys do not break binding); compiled-in defaults are 60/120.

## Defect 1 — HIGH: the suite could not catch the regression this milestone fixes

The Challenger replaced `PermitLimit = limits.PermitLimit` with a hardcoded `10` — reproducing the
exact pre-restore regression — and ran the existing tests:

```
Failed! - Failed: 1, Passed: 4, Skipped: 0, Total: 5
```

**4 of the 5 existing rate-limit tests passed against a limiter that ignores configuration
entirely.** The two headline tests — `Rate_Limiting_Middleware_Blocks_Excessive_Login_Requests_With_429`
and its public-apply sibling — passed because they set the limit to exactly 10, coinciding with the
mutant constant. The single failure was incidental: a test that sets the limit to 2 and got
`Unauthorized` instead of `TooManyRequests`.

A limiter permanently stuck at the old value ships green. The new tests fail loudly on the same
mutant:

```
Request 11 of 60 was rejected with 429. The configured PermitLimit=60 is NOT reaching the
limiter — it is capped lower.
```

## Defect 2 — MEDIUM: `RateLimiting_Isolate_IP_Partitions_Correctly` tests no isolation

`Challenger1AdversarialTests.cs:57` drives **one** client and asserts only that blocking happens.
Under TestServer `Connection.RemoteIpAddress` is null, so `ClientPartitionKey` returns the constant
`"unknown"` and every caller shares one bucket — proved empirically: client A burns 4 permits and
client B's *first* request returns 429.

> **Per-IP behaviour had never been observed working in this repo — only its configuration had.**

Closed with an `IStartupFilter` that stamps a real `RemoteIpAddress` ahead of `UseRateLimiter`. The
partitioning logic itself is correct: distinct IPv4 addresses get independent buckets, IPv6
addresses in one /64 share a bucket, a different /64 does not. The Challenger did not edit the
misleading test — recommend the Worker rename or fix it.

## The lesson for every future regression test here

An `IOptions`-only assertion would **not** have caught defect 1 — the Challenger's own
`ConfiguredValue_Is_Visible_To_IOptions_Resolved_From_A_Request_Scope` passed against the mutant,
because the mutant broke *consumption*, not binding. **Any regression test on the limiter must drive
HTTP.**

## Checked and coherent, not defects

Compose runs `ASPNETCORE_ENVIRONMENT: Development`, so `appsettings.Development.json` (60/120) is
what is in force under `docker compose up`, matching the base file.
`ReverseProxy:TrustForwardedHeaders` stays false while the API port is published directly
(`5080:8080`) — correct, since trusting XFF on a directly reachable API lets anyone mint a fresh
partition key.

**Evidence file:** `backend/tests/RecruitOps.Api.Tests/ChallengerM11RateLimitTests.cs` — 12 tests,
all passing. No product code modified, no test deleted.
