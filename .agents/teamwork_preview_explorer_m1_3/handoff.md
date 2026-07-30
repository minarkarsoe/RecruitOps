# Handoff Report: Milestone 1 Verification & Test Strategy

**Agent:** Explorer 3 (`teamwork_preview_explorer_m1_3`)  
**Target:** Orchestrator / Worker (`teamwork_preview_worker_m1_1`)  
**Date:** 2026-07-29  

---

## 1. Observation

Direct observations from file inspections and code auditing:

1. **`backend/src/Api/Program.cs` (Line 147)**:
   ```csharp
   o.KnownNetworks.Clear();
   ```
   *Issue:* `KnownNetworks` on `ForwardedHeadersOptions` is deprecated in .NET 10 ASP.NET Core (Warning `ASPDEPR005`).

2. **`backend/src/Domain/ApplicationFormSchema.cs` (Line 231)**:
   ```csharp
   if (!(field.Options ?? []).Contains(text, StringComparer.Ordinal))
   ```
   *Issue:* `text` is of type `string?`. Passing `string?` to `Enumerable.Contains<string>(..., string value, ...)` causes warning `CS8604` (Possible null reference argument).

3. **`backend/src/Api/Controllers/UsersController.cs` (Line 50)**:
   ```csharp
   .Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
   ```
   *Issue:* Calling `u.Role.ToString()` inside `.Select(...)` prior to materialization (`ToListAsync`) fails SQL translation under Npgsql EF Core 10 PostgreSQL provider throwing `InvalidOperationException`. Compare with line 88–93 in `Selectable()` which correctly uses a two-step query.

4. **`backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs` (Lines 50–60)**:
   ```csharp
   [Fact]
   public async Task Issued_Token_Grants_Access_To_Protected_Endpoint()
   {
       var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
           new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
       var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
       Assert.NotNull(body);
       Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
   }
   ```
   *Issue:* Test never invokes a protected endpoint with the retrieved bearer token.

5. **Loose Status Assertions in Integration Tests**:
   - `backend/tests/RecruitOps.Api.Tests/InterviewFlowTests.cs:144`: `Assert.True(noPanel.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);`
   - `backend/tests/RecruitOps.Api.Tests/ScorecardBlindScoringTests.cs:238`: `Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);`
   - `backend/tests/RecruitOps.Api.Tests/ScorecardTemplateResolutionTests.cs:108`: `Assert.True(empty.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);`
   *Issue:* Loose disjunction hides expected validation semantics.

6. **`backend/tests/RecruitOps.Domain.Tests/ApplicationFormSchemaTests.cs` (Line 217)**:
   ```csharp
   Assert.False(ApplicationFormSchema.TryValidateAnswers(schema, answers, out _, out _));
   ```
   *Issue:* `out var error` is ignored; does not assert the validation error message.

---

## 2. Logic Chain

1. **Observed Warning ASPDEPR005** in `Program.cs:147`: .NET 10 ASP.NET Core deprecated `KnownNetworks` on `ForwardedHeadersOptions` in favor of `KnownIPNetworks`. Calling `o.KnownIPNetworks.Clear();` fixes compiler warning `ASPDEPR005`.
2. **Observed Warning CS8604** in `ApplicationFormSchema.cs:231`: `text` is `string?`. Since flow control guarantees non-null `text` inside the switch block, using `text!` satisfies compiler nullability checks.
3. **Observed PostgreSQL Translation Bug** in `UsersController.cs:50`: `u.Role.ToString()` in LINQ projection cannot be translated to SQL by `Npgsql`. Refactoring `Get()` to query `{u.Id, u.Email, u.DisplayName, u.Role}` into an anonymous type first and then projecting `.ToString()` in memory resolves the runtime SQL translation failure.
4. **Observed Trivial Assertion** in `AuthLoginTests.cs:50-60`: `Issued_Token_Grants_Access_To_Protected_Endpoint()` checks string non-emptiness instead of sending a request with `Authorization: Bearer <token>`. Modifying the test to send a request to `GET /api/departments` validates real JWT middleware behavior.
5. **Observed Loose Assertions**: DTO model validation returns `400 BadRequest`. Replacing loose disjunctions with `Assert.Equal(HttpStatusCode.BadRequest, ...)` ensures rigorous HTTP API contract testing.

---

## 3. Caveats

1. **Test Environment vs Production DB**: Tests currently run against EF Core `InMemory` provider (`CustomWebAppFactory`), which masks LINQ translation errors. Real PostgreSQL translation behavior must be verified by following EF Core translation rules.
2. **Terminal Execution Constraints**: Terminal execution (`run_command`) timed out waiting for user confirmation during investigation, so code analysis was conducted via static file inspection (`view_file`, `grep_search`, `find_by_name`).

---

## 4. Conclusion

All Milestone 1 backend R1 tasks have been fully analyzed and scoped. The Worker agent can proceed to implement the fixes in `Program.cs`, `ApplicationFormSchema.cs`, `UsersController.cs`, `AuthLoginTests.cs`, `InterviewFlowTests.cs`, `ScorecardBlindScoringTests.cs`, `ScorecardTemplateResolutionTests.cs`, and `ApplicationFormSchemaTests.cs` using the step-by-step execution plan detailed in `analysis.md`.

---

## 5. Verification Method

To verify the implementation independently, execute the following:

1. **Compiler Build (Check 0 warnings)**:
   ```powershell
   dotnet build backend/src/Api/RecruitOps.Api.csproj --configuration Release
   ```
   *Expected output:* Build succeeded with 0 Warnings and 0 Errors.

2. **Domain Unit Test Execution**:
   ```powershell
   dotnet test backend/tests/RecruitOps.Domain.Tests/RecruitOps.Domain.Tests.csproj
   ```
   *Expected output:* All domain unit tests pass.

3. **API Integration Test Execution**:
   ```powershell
   dotnet test backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj
   ```
   *Expected output:* All API integration tests pass, including updated `Issued_Token_Grants_Access_To_Protected_Endpoint` and exact status assertions.
