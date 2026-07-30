# Handoff Report — Requirement R1 Analysis & Proposals

## 1. Observation
- **`UsersController.cs` (lines 46-51)**:
  `UsersController.Get` attempts `Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))` inside `IQueryable` before materialization.
  Conversely, `UsersController.Selectable` (lines 84-93) explicitly uses a two-step approach (`Select(u => new { u.Id, u.DisplayName, u.Role }).ToListAsync(ct)` followed by in-memory `.Select(u => new SelectableUserDto(u.Id, u.DisplayName, u.Role.ToString()))`), noting in lines 80-84 that EF Core cannot translate `enum.ToString()` into SQL against PostgreSQL.
- **`AuthLoginTests.cs` (lines 50-60)**:
  `Issued_Token_Grants_Access_To_Protected_Endpoint` only calls `/api/auth/login` and asserts `body.AccessToken` is not empty. It never performs an HTTP GET request with `Authorization: Bearer <AccessToken>` to any protected route (such as `/api/departments`) nor does it assert `200 OK`.

## 2. Logic Chain
1. **`UsersController.cs`**:
   - Observation: `u.Role.ToString()` is executed inside `_db.Users.Select(...)`.
   - Logic: EF Core Npgsql SQL generator attempts to convert the LINQ projection into database SQL. PostgreSQL integer/enum columns cannot be converted via `.ToString()` inside Npgsql SQL expressions, throwing `InvalidOperationException` at runtime against PostgreSQL.
   - Deduction: Rewriting `Get` into a two-step query (SQL query selecting anonymous type with raw `Role`, followed by in-memory `UserListItemDto` projection after `ToListAsync(ct)`) matches `Selectable` and eliminates SQL translation failure.

2. **`AuthLoginTests.cs`**:
   - Observation: `Issued_Token_Grants_Access_To_Protected_Endpoint()` stops after retrieving `AccessToken` from login response.
   - Logic: The test claims to assert that an issued token grants access to a protected endpoint, but never attempts to access a protected endpoint with the bearer token.
   - Deduction: Adding an HTTP GET request to `/api/departments` with `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken)` and asserting `HttpStatusCode.OK` makes the test perform its actual stated job.

## 3. Caveats
- EF Core In-Memory database provider evaluates `enum.ToString()` in memory during unit tests, which is why unit tests did not catch `UsersController.Get` failing on PostgreSQL prior to this analysis.
- No other caveats.

## 4. Conclusion
Exact changes required:
- `backend/src/Api/Controllers/UsersController.cs`: Refactor `Get` method (lines 46-51) into two-step in-memory projection.
- `backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs`: Update `Issued_Token_Grants_Access_To_Protected_Endpoint()` (lines 50-60) to send `GET /api/departments` with bearer token header and assert 200 OK.

Full details and exact code diffs are in `analysis.md`.

## 5. Verification Method
- Execute build: `dotnet build backend/RecruitOps.sln`
- Run test suite: `dotnet test backend/RecruitOps.sln`
