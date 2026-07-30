# Challenge Report: Milestone 1 Verification (Challenger 2)

**Target Scope**: Package security dependencies (`System.Security.Cryptography.Xml` & NU1903 warnings) and API status code assertion integrity (`InterviewFlowTests`, `ScorecardBlindScoringTests`, `ScorecardTemplateResolutionTests`).

## Challenge Summary

**Overall risk assessment**: **MEDIUM** (Build succeeds and all 32 API tests pass with explicit HTTP status code assertions, but NU1903 high-severity security warnings remain active during `dotnet build`).

---

## Findings & Verification Results

### 1. Package Security Dependencies & NU1903 Warnings
- **Claim**: `.csproj` files reference `System.Security.Cryptography.Xml` and no NU1903 warnings remain during `dotnet build backend/RecruitOps.sln`.
- **Status**: **FAILED** (NU1903 Warnings Persist)
- **Empirical Evidence**:
  - Direct `.csproj` inspection confirms references to `System.Security.Cryptography.Xml` version `10.0.6` in:
    - `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj` (Line 22)
    - `backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj` (Line 18)
  - Execution of `dotnet build backend/RecruitOps.sln` produced **20 NU1903 warnings** for `System.Security.Cryptography.Xml` version `10.0.6`:
    ```
    RecruitOps.Infrastructure.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.6 has a known high severity vulnerability, https://github.com/advisories/GHSA-23rf-6693-g89p
    RecruitOps.Infrastructure.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.6 has a known high severity vulnerability, https://github.com/advisories/GHSA-8q5v-6pqq-x66h
    RecruitOps.Infrastructure.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.6 has a known high severity vulnerability, https://github.com/advisories/GHSA-cvvh-rhrc-wg4q
    RecruitOps.Infrastructure.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.6 has a known high severity vulnerability, https://github.com/advisories/GHSA-g8r8-53c2-pm3f
    RecruitOps.Infrastructure.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.6 has a known high severity vulnerability, https://github.com/advisories/GHSA-mmjf-rqrv-855v
    ```
  - **Root Cause**: The `.csproj` comments note pinning to `10.0.6` to avoid CVE-2026-33116, but NuGet security audits recognize `10.0.6` as having known high severity security advisories (GHSA-23rf-6693-g89p, GHSA-8q5v-6pqq-x66h, GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v).

---

### 2. Status Code Assertion Integrity & Filtered Test Suite
- **Claim**: Execute filtered `dotnet test` and verify tightened assertions.
- **Status**: **PASSED**
- **Empirical Evidence**:
  - Test command executed:
    ```bash
    dotnet test backend/tests/RecruitOps.Api.Tests --filter "FullyQualifiedName~InterviewFlowTests|FullyQualifiedName~ScorecardBlindScoringTests|FullyQualifiedName~ScorecardTemplateResolutionTests"
    ```
  - Execution Result: **32 Passed, 0 Failed, 0 Skipped** (Duration: ~5s).
    - `InterviewFlowTests`: 13 passed tests.
    - `ScorecardBlindScoringTests`: 12 passed tests.
    - `ScorecardTemplateResolutionTests`: 7 passed tests.
  - Verification of Status Code Assertions:
    - Inspected code across all 3 test classes. Status code checks use explicit enum assertions (`HttpStatusCode.NotFound`, `HttpStatusCode.Forbidden`, `HttpStatusCode.Conflict`, `HttpStatusCode.BadRequest`, `HttpStatusCode.Unauthorized`, `HttpStatusCode.OK`).
    - Specific status code assertions verified:
      - **404 NotFound**: Scoped access checks for interview details across departments (`InterviewFlowTests.cs:75`, `InterviewFlowTests.cs:105`, `InterviewFlowTests.cs:258`, `ScorecardBlindScoringTests.cs:197`).
      - **403 Forbidden**: Role restriction enforcement (e.g. non-recruiter rescheduling `InterviewFlowTests.cs:122`, non-admin scorecard template creation `ScorecardTemplateResolutionTests.cs:160`).
      - **409 Conflict**: Domain boundary & conflict prevention (e.g. stray lead `InterviewFlowTests.cs:155`, unknown interviewer `InterviewFlowTests.cs:173`, rejected application scheduling `InterviewFlowTests.cs:194`, cancelled round rescheduling `InterviewFlowTests.cs:217`, dropping active panel interviewer `InterviewFlowTests.cs:247`, revising submitted scorecard `ScorecardBlindScoringTests.cs:135`, incomplete submission `ScorecardBlindScoringTests.cs:152`, scoring cancelled round `ScorecardBlindScoringTests.cs:272`, dual scope template `ScorecardTemplateResolutionTests.cs:81`, duplicate active scope template `ScorecardTemplateResolutionTests.cs:96`, unknown criterion type `ScorecardTemplateResolutionTests.cs:119`).
      - **400 BadRequest**: Model validation (e.g. empty panel list `InterviewFlowTests.cs:144`, rating outside 1-5 `ScorecardBlindScoringTests.cs:238`, empty template criteria `ScorecardTemplateResolutionTests.cs:108`).
      - **401 Unauthorized**: Unauthenticated request handling (`InterviewFlowTests.cs:269`).
      - **200 OK**: Authorized read validation (`InterviewFlowTests.cs:98`, `ScorecardTemplateResolutionTests.cs:169`).

---

## Challenges

### [Medium] Security Vulnerability Warnings (NU1903) in `System.Security.Cryptography.Xml`
- **Assumption challenged**: Pinning `System.Security.Cryptography.Xml` to `10.0.6` eliminates NU1903 security audit warnings during dotnet build.
- **Attack scenario**: Version `10.0.6` contains known vulnerabilities (GHSA-23rf-6693-g89p, GHSA-8q5v-6pqq-x66h, GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v). Downstream CI/CD pipelines enforcing strict security audits or treating warnings as errors (`<WarningsAsErrors>NU1903</WarningsAsErrors>`) will fail or deploy packages with active security advisories.
- **Blast radius**: `RecruitOps.Infrastructure` and `RecruitOps.Api.Tests` projects.
- **Mitigation**: Update `System.Security.Cryptography.Xml` package reference in both `.csproj` files to a patched release without advisory flags, or audit transitive dependencies pulling the package.

---

## Stress Test Results

- `dotnet build backend/RecruitOps.sln` → Build succeeded with 20 NU1903 warnings → **FAIL** (NU1903 warnings present).
- `dotnet test` with filter `FullyQualifiedName~InterviewFlowTests|FullyQualifiedName~ScorecardBlindScoringTests|FullyQualifiedName~ScorecardTemplateResolutionTests` → 32/32 tests pass with explicit `HttpStatusCode` assertions → **PASS**.

---

## Unchallenged Areas

- **Domain logic inside RecruitOps.Application**: Verified via API test suite execution; full unit-level domain coverage was outside this challenger's explicit verification filter.
