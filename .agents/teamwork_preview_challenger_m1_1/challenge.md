# Challenge Report — Milestone 1 Verification

## Challenge Summary

**Overall risk assessment**: LOW

Empirical testing and code inspection confirm that all Milestone 1 deliverables pass verification:
- `dotnet test backend/tests/RecruitOps.Api.Tests`: 133 Passed, 0 Failed.
- `dotnet test backend/tests/RecruitOps.Domain.Tests`: 39 Passed, 0 Failed.
- `UsersController.Get` endpoint executes safely using two-step LINQ projection, avoiding EF Core SQL translation exceptions.
- `AuthLoginTests.Issued_Token_Grants_Access_To_Protected_Endpoint` successfully validates JWT bearer token authentication and policy enforcement against `/api/departments`.

---

## 1. Empirical Execution Results

### Test Suite Execution
| Test Assembly | Total | Passed | Failed | Skipped | Duration | Result |
|---|---|---|---|---|---|---|
| `RecruitOps.Api.Tests` | 133 | 133 | 0 | 0 | 5.0 s | **PASS** |
| `RecruitOps.Domain.Tests` | 39 | 39 | 0 | 0 | 0.14 s | **PASS** |

### Key Endpoint Verification

#### A. `UsersController.Get` LINQ Translation Safety
- **Endpoint**: `GET /api/users` (`UsersController.Get`)
- **Policy**: `AdminOnly`
- **Implementation**:
  ```csharp
  var rows = await _db.Users
      .AsNoTracking()
      .Where(u => u.IsActive)
      .OrderBy(u => u.DisplayName)
      .Select(u => new { u.Id, u.Email, u.DisplayName, u.Role })
      .ToListAsync(ct);

  var users = rows
      .Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
      .ToList();
  ```
- **Verification**: EF Core LINQ query materializes `rows` into memory first (`ToListAsync`), and `u.Role.ToString()` is evaluated in-memory via LINQ to Objects. This prevents EF Core database provider translation failures (e.g. Postgres Npgsql translation errors on enum `ToString()`).
- **Test Coverage**: Executed via integration test `An_Admin_Still_Reads_Both` in `UserDirectoryTests.cs` — returned `200 OK`.

#### B. `AuthLoginTests.Issued_Token_Grants_Access_To_Protected_Endpoint`
- **Endpoint**: `GET /api/departments`
- **Test File**: `backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs`
- **Implementation**:
  1. Issues `POST /api/auth/login` with `AdminEmail` & `AdminPassword`.
  2. Deserializes `LoginResponse` containing JWT `AccessToken`.
  3. Attaches `Authorization: Bearer <AccessToken>` header to `HttpClient`.
  4. Invokes `GET /api/departments` protected by `[Authorize(Policy = Policies.InternalUser)]`.
  5. Asserts `200 OK`.
- **Verification**: Test executed and passed cleanly.

---

## 2. Challenges & Stress-Test Analysis

### [Low] Challenge 1: Outdated Inline Comment in `UsersController.cs`
- **Assumption challenged**: Comments in code accurately reflect current LINQ query structure.
- **Observation**: Comment on line 86-87 states "`Get` above projects the enum inside the query and has never been run against Postgres".
- **Findings**: `Get` on lines 46-56 actually *does* perform two-step materialization (`.Select(u => new { u.Id, u.Email, u.DisplayName, u.Role }).ToListAsync(ct)` followed by in-memory `.Select(u => new UserListItemDto(..., u.Role.ToString()))`). The query pattern is already fixed and safe, but the code comment in `Selectable` was not updated to reflect that `Get` was also updated.
- **Blast radius**: Low (code comments only, zero impact on runtime behavior or correctness).
- **Mitigation**: Update inline comment in a future documentation pass if desired.

### [Low] Challenge 2: Security Warnings on Dependency `System.Security.Cryptography.Xml`
- **Assumption challenged**: All NuGet packages are free of known vulnerabilities.
- **Observation**: `dotnet test` raised NU1903 package vulnerability warnings for `System.Security.Cryptography.Xml` 10.0.6 (GHSA-23rf-6693-g89p, GHSA-8q5v-6pqq-x66h, GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v).
- **Blast radius**: Low for current dev/test stage, potential risk if unpatched before production deployment.
- **Mitigation**: Upgrade underlying package dependencies when updating .NET packages.

---

## 3. Unchallenged / Verified Areas

- **Policy separation on `UsersController`**: Confirmed per-action authorization policy separation (`AdminOnly` on `Get`, `RecruitmentStaff` on `Selectable`), preserving ADR-0019 requirement that recruiters get a narrower payload without email addresses.
- **Domain logic unit tests**: All 39 tests in `RecruitOps.Domain.Tests` passed without failure.
