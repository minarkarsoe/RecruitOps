# Analysis & Fix Proposal Report: Requirement R1 (Milestone 1)

## Executive Summary
This report provides a detailed analysis and concrete refactoring proposal for Requirement R1 of Milestone 1 in the RecruitOps project.
The investigation covers two items:
1. The EF Core PostgreSQL SQL translation bug in `backend/src/Api/Controllers/UsersController.cs` (`GET /api/users`).
2. The deceptive assertion in `backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs` (`Issued_Token_Grants_Access_To_Protected_Endpoint`).

---

## 1. SQL Translation Bug in `GET /api/users` (`UsersController.cs`)

### File Path & Line Numbers
- **File**: `backend/src/Api/Controllers/UsersController.cs`
- **Affected Method**: `Get(CancellationToken ct)` (Lines 35–54, specifically lines 46–51)

### Code Inspection
```csharp
35:     /// <summary>All active users in the tenant, ordered by display name.
...
42:     [HttpGet]
43:     [Authorize(Policy = Policies.AdminOnly)]
44:     public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> Get(CancellationToken ct)
45:     {
46:         var users = await _db.Users
47:             .AsNoTracking()
48:             .Where(u => u.IsActive)
49:             .OrderBy(u => u.DisplayName)
50:             .Select(u => new UserListItemDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
51:             .ToListAsync(ct);
52: 
53:         return Ok(users);
54:     }
```

### Root Cause Explanation
When EF Core builds a SQL query from an `IQueryable` LINQ expression, it attempts to translate all operations inside `.Select(...)` into database server SQL expressions.
In `UsersController.cs` line 50, `u.Role` is an enum (`UserRole`), which is stored as an integer in PostgreSQL. Calling `u.Role.ToString()` inside the `.Select(...)` projection forces EF Core and the Npgsql PostgreSQL provider to try to translate `enum.ToString()` into a SQL string conversion statement.

EF Core and Npgsql do **not** support SQL translation for `enum.ToString()` expressions inside `IQueryable` projections. When executed against a PostgreSQL database runtime, EF Core throws an `InvalidOperationException` (e.g. *"The LINQ expression '...' could not be translated. Either rewrite the query in a form that can be translated, or switch to client evaluation explicitly..."*).

*(Note: EF Core's In-Memory provider client-evaluates expressions, which hid this bug in unit tests. However, lines 80-84 in `UsersController.cs` explicitly note: "`Get` above projects the enum inside the query and has never been run against Postgres; do not copy that shape.")*

### Proposed Two-Step In-Memory Projection Refactor
To resolve the issue, `Get` must follow the exact two-step projection pattern already implemented in `Selectable` (lines 84–93 of `UsersController.cs`):
1. **Query in SQL**: Query the database using `Select` into an anonymous object carrying raw types (`u.Id`, `u.Email`, `u.DisplayName`, `u.Role`). Materialize the results into memory using `ToListAsync(ct)`.
2. **Project in Memory**: Perform `.Select(...)` in memory on the materialized list to invoke `.Role.ToString()` and construct `UserListItemDto`.

#### Replacement Code Snippet (`backend/src/Api/Controllers/UsersController.cs`)
Replace lines 46–51 with:
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

---

## 2. Deceptive Assertion in `AuthLoginTests.cs`

### File Path & Line Numbers
- **File**: `backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs`
- **Affected Test**: `Issued_Token_Grants_Access_To_Protected_Endpoint()` (Lines 50–60)

### Code Inspection
```csharp
50:     [Fact]
51:     public async Task Issued_Token_Grants_Access_To_Protected_Endpoint()
52:     {
53:         // End-to-end: log in, then call a protected endpoint with the real bearer token.
54:         // (Uses the Test scheme's bearer passthrough is NOT active here — this asserts the
55:         //  token is well-formed; full JWT-scheme verification is covered by the API config.)
56:         var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
57:             new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
58:         var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
59:         Assert.NotNull(body);
60:         Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
61:     }
```

### Why the Assertion is Deceptive
The test method name is `Issued_Token_Grants_Access_To_Protected_Endpoint()`, and its comment claims to test logging in and calling a protected endpoint with a bearer token. However:
- The current implementation only logs in via `/api/auth/login` and asserts that `body.AccessToken` is non-null and non-empty.
- It **never sends** an HTTP request to any protected endpoint (e.g. `/api/departments` or `/api/users`), nor does it attach `Authorization: Bearer <AccessToken>` to a request header, nor does it assert `200 OK` from a protected resource.
- Therefore, if authorization on protected endpoints fails or token verification breaks, this test will falsely pass.

### Proposed Refactor
Update `Issued_Token_Grants_Access_To_Protected_Endpoint()` to take the retrieved `body.AccessToken`, configure an `HttpClient` (or `HttpRequestMessage`) with `Authorization: Bearer <AccessToken>`, issue an HTTP `GET` request to `/api/departments`, and assert `HttpStatusCode.OK` (200 OK).

#### Replacement Code Snippet (`backend/tests/RecruitOps.Api.Tests/AuthLoginTests.cs`)
Replace lines 50–60 with:
```csharp
    [Fact]
    public async Task Issued_Token_Grants_Access_To_Protected_Endpoint()
    {
        // End-to-end: log in, then call a protected endpoint with the real bearer token.
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));

        // Attach bearer token and call protected endpoint /api/departments
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body.AccessToken);

        var response = await client.GetAsync("/api/departments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
```

---

## 3. Verification Instructions

1. **Verify Backend Build**:
   ```powershell
   dotnet build backend/RecruitOps.sln
   ```
2. **Verify Test Suite**:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
3. **Verify Specific Tests**:
   ```powershell
   dotnet test backend/tests/RecruitOps.Api.Tests --filter "FullyQualifiedName~AuthLoginTests|FullyQualifiedName~UserDirectoryTests"
   ```
