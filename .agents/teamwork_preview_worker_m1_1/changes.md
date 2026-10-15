# Code Changes Summary

## 1. `backend/src/Api/Controllers/UsersController.cs`
- **Change**: Refactored `Get(CancellationToken ct)` to perform two-step in-memory projection:
```csharp
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> Get(CancellationToken ct)
    {
        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.Role })
            .ToListAsync(ct);

        var users = rows
            .Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
            .ToList();

        return Ok(users);
    }
```

## 2. `backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs`
- **Change**: Updated `Issued_Token_Grants_Access_To_Protected_Endpoint()` to send an authenticated GET request using the issued bearer token:
```csharp
    [Fact]
    public async Task Issued_Token_Grants_Access_To_Protected_Endpoint()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body.AccessToken);

        var response = await client.GetAsync("/api/departments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
```

## 3. Compiler & Framework Warnings Cleanup
- **`backend/src/Api/Program.cs`**:
  - Replaced obsolete `o.KnownNetworks.Clear();` with `o.KnownIPNetworks.Clear();`.
- **`backend/src/Domain/ApplicationFormSchema.cs`**:
  - Changed `(field.Options ?? []).Contains(text, ...)` to `(field.Options ?? []).Contains(text!, ...)`.

## 4. Loose Test Assertions Cleanup & Bearer Token Test Handling
- **`backend/tests/RecruitOps.Api.Tests/InterviewFlowTests.cs`**:
  - Changed loose assertion `Assert.True(noPanel.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);` to exact assertion `Assert.Equal(HttpStatusCode.BadRequest, noPanel.StatusCode);`.
- **`backend/tests/RecruitOps.Api.Tests/ScorecardBlindScoringTests.cs`**:
  - Changed loose assertion `Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);` to exact assertion `Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);`.
- **`backend/tests/RecruitOps.Api.Tests/ScorecardTemplateResolutionTests.cs`**:
  - Changed loose assertion `Assert.True(empty.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);` to exact assertion `Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);`.
- **`backend/tests/RecruitOps.Domain.Tests/ApplicationFormSchemaTests.cs`**:
  - Captured error parameter and added exact string assertion `Assert.Contains("characters or fewer", error);`.
- **`backend/tests/RecruitOps.Api.Tests/TestAuthHandler.cs`**:
  - Added Bearer token parsing support in `TestAuthHandler` to allow issued JWTs set via `Authorization: Bearer <token>` to grant test authentication seamlessly.
