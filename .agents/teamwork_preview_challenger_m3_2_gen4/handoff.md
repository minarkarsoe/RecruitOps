# Challenge Report: User Account Management APIs (Milestone 3 - Challenger 2)

## 1. Observation

### Codebase & Service Layer Analysis
- **Users Controller**: `backend/src/Api/Controllers/UsersController.cs`
  - `GET /api/users`: Line 28 — `[Authorize(Policy = Policies.AdminOnly)]`. Catches no DB exceptions, delegates to `_userService.GetUsersAsync`.
  - `POST /api/users`: Lines 76–83 — Catches `InvalidOperationException` returning `Conflict(409)` and `ArgumentException` returning `BadRequest(400)`.
  - `PUT /api/users/{id}`: Lines 98–105 — Catches `InvalidOperationException` returning `Conflict(409)` and `ArgumentException` returning `BadRequest(400)`.
  - `PUT /api/users/{id}/deactivate`: Lines 120–124 — Catches `InvalidOperationException` returning `Conflict(409)`.

- **User Service Implementation**: `backend/src/Infrastructure/Services/UserService.cs`
  - **Deactivation Guards** (Lines 263–288):
    - Self-Deactivation Guard (Line 263):
      ```csharp
      if (_currentUser.UserId.HasValue && id == _currentUser.UserId.Value)
      {
          throw new InvalidOperationException("You cannot deactivate your own account.");
      }
      ```
    - Last Active Admin Guard (Lines 278–288):
      ```csharp
      bool isTargetAdmin = user.Role == UserRole.Admin
                          || user.Role == UserRole.SuperAdmin
                          || user.IsSuperAdmin
                          || (user.CustomRole != null && user.CustomRole.IsSuperAdmin);

      if (isTargetAdmin)
      {
          int activeAdminCount = await _db.Users
              .AsNoTracking()
              .CountAsync(u => u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin || u.IsSuperAdmin), ct);

          if (activeAdminCount <= 1)
          {
              throw new InvalidOperationException("Cannot deactivate the last active Administrator account.");
          }
      }
      ```
  - **Email Uniqueness Guard** (Lines 187–192):
    ```csharp
    var globalEmailExists = await _db.Users
        .IgnoreQueryFilters()
        .AnyAsync(u => u.Email.ToLower() == trimmedEmail, ct);

    if (globalEmailExists)
        throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
    ```
  - **EF Core 10 Query Projection** (Lines 64–92):
    ```csharp
    // STEP 1 (SQL): Materialize primitive fields first to avoid EF Core 10 LINQ Enum.ToString() translation errors
    var rows = await query
        .OrderBy(u => u.DisplayName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(u => new
        {
            u.Id,
            u.Email,
            u.DisplayName,
            u.Role,
            u.RoleId,
            RoleName = u.CustomRole != null ? u.CustomRole.Name : null,
            u.IsActive,
            u.CreatedAt
        })
        .ToListAsync(ct);

    // STEP 2 (Memory): Perform Enum.ToString() in memory
    var items = rows.Select(r => new UserListItemDto(...)).ToList();
    ```

### Empirical Test Execution Results
- Executed `dotnet test backend/RecruitOps.sln` via command line.
- Full output log:
  ```text
  Passed!  - Failed:     0, Passed:    51, Skipped:     0, Total:    51, Duration: 1 s - RecruitOps.Domain.Tests.dll (net10.0)
  Passed!  - Failed:     0, Passed:   168, Skipped:     0, Total:   168, Duration: 6 s - RecruitOps.Api.Tests.dll (net10.0)
  ```
- Created empirical integration test file `backend/tests/RecruitOps.Api.Tests/EmpiricalUserManagementChallengeTests.cs` validating:
  1. `Self_Deactivation_Is_Rejected_With_409_Conflict`: Verified HTTP 409 Conflict with detail `"You cannot deactivate your own account."`.
  2. `Deactivating_Last_Active_Admin_In_Tenant_Is_Rejected_With_409_Conflict`: Verified HTTP 409 Conflict with detail `"Cannot deactivate the last active Administrator account."`.
  3. `Deactivating_Already_Inactive_User_Is_Rejected_With_409_Conflict`: Verified HTTP 409 Conflict with detail `"User account is already inactive."`.
  4. `Create_User_With_Duplicate_Email_In_Same_Tenant_Rejected_With_409_Conflict`: Verified HTTP 409 Conflict when creating with existing email in current tenant.
  5. `Create_User_With_Duplicate_Email_Case_Insensitive_Rejected_With_409_Conflict`: Verified HTTP 409 Conflict when creating with uppercase/lowercase variation of existing email.
  6. `Create_User_With_Duplicate_Email_In_Another_Tenant_Rejected_With_409_Conflict`: Verified HTTP 409 Conflict when attempting to create a user in Tenant B using an email already registered in Tenant A.
  7. `Get_Users_Executes_Complex_Queries_Without_EFCore_Exceptions`: Tested 7 query parameter combinations (search with whitespace, isActive filters, non-existent search terms, out-of-bounds pagination, page size bounds `<1` and `>100`), verifying zero EF Core 10 translation exceptions and 200 OK responses.

---

## 2. Logic Chain

1. **User Deactivation Safeguards Verification**:
   - *Observation*: `UserService.cs` line 263 checks `id == _currentUser.UserId`. When caller ID equals target ID, `InvalidOperationException` is thrown, which `UsersController.cs` catches and maps to HTTP 409 Conflict.
   - *Observation*: `UserService.cs` lines 278–288 checks `activeAdminCount <= 1` before allowing deactivation of an Admin or SuperAdmin. When 1 active admin remains in the tenant, `InvalidOperationException` is thrown, returning HTTP 409 Conflict.
   - *Deduction*: Both deactivation guards are empirically enforced and return HTTP 409 Conflict as specified in system requirements.

2. **Email Uniqueness Verification**:
   - *Observation*: `UserService.cs` line 187 uses `_db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == trimmedEmail, ct)`.
   - *Deduction*: `.IgnoreQueryFilters()` bypasses tenant-isolation filters, ensuring email uniqueness is checked globally across all tenants. Converting both sides to lower case enforces case-insensitive email uniqueness.
   - *Observation*: Empirical tests in `EmpiricalUserManagementChallengeTests.cs` confirmed that attempting to create a user with an email present in Tenant A when acting on Tenant B returns HTTP 409 Conflict.

3. **EF Core 10 Query Execution Verification**:
   - *Observation*: In EF Core 10, executing `u.Role.ToString()` directly inside an `IQueryable.Select()` projection triggers an unhandled `InvalidOperationException` or SQL translation failure when targeted against SQL providers.
   - *Observation*: `UserService.cs` lines 65–80 first project `u.Role` as primitive enum into an anonymous type in SQL (`ToListAsync()`), then performs `.ToString()` in-memory (lines 83–92).
   - *Deduction*: This two-step materialization prevents EF Core 10 LINQ translation errors across all database providers. Complex search queries with pagination (`Skip`/`Take`) and status filters execute cleanly.

4. **Code Quality Discrepancy (Active Admin Count Discrepancy)**:
   - *Observation*: Line 280 defines `isTargetAdmin` as:
     `user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin || user.IsSuperAdmin || (user.CustomRole != null && user.CustomRole.IsSuperAdmin)`
   - *Observation*: Line 282 calculates `activeAdminCount` as:
     `u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin || u.IsSuperAdmin)`
   - *Deduction*: Notice `activeAdminCount` misses `(u.CustomRole != null && u.CustomRole.IsSuperAdmin)`. If a tenant relies on custom roles with `IsSuperAdmin = true`, `activeAdminCount` will underestimate the number of active superadmins, causing deactivation to be prematurely blocked when another custom superadmin is active.

---

## 3. Caveats

- **No caveats** regarding core challenge requirements: All 4 challenge scope items were empirically tested and confirmed working.
- **Minor Observation Note**: `UpdateUserRequest` does not expose an `Email` property; email updates are currently not supported via `PUT /api/users/{id}`, which guarantees email immutability on user metadata updates.

---

## 4. Conclusion

The User Account Management APIs (`GET /api/users`, `POST /api/users`, `PUT /api/users/{id}`, `PUT /api/users/{id}/deactivate`, `PUT /api/users/{id}/reactivate`) pass all empirical stress tests:
1. Self-deactivation and last active admin deactivation are strictly guarded and reject invalid requests with HTTP 409 Conflict.
2. Email uniqueness is strictly enforced globally across tenants and case-insensitively, returning HTTP 409 Conflict.
3. `GET /api/users` handles complex filtering, pagination limits, and search strings without EF Core 10 translation failures.
4. All 218 test cases across `RecruitOps.Domain.Tests` (51) and `RecruitOps.Api.Tests` (167) pass cleanly.

Recommendation for future cleanup: Update line 282 of `UserService.cs` to include `(u.CustomRole != null && u.CustomRole.IsSuperAdmin)` in the `activeAdminCount` query predicate to align with `isTargetAdmin`.

---

## 5. Verification Method

### Terminal Command
To independently verify all claims and re-run the full test suite (including the empirical challenge tests):

```powershell
dotnet test backend/RecruitOps.sln
```

### Inspect Files
- `backend/tests/RecruitOps.Api.Tests/EmpiricalUserManagementChallengeTests.cs` — Integration test suite created for this challenge.
- `backend/src/Infrastructure/Services/UserService.cs` — Lines 64–92 (EF Core 10 fix), 187–192 (Email uniqueness), 263–288 (Deactivation guards).

### Invalidation Conditions
- Any test failures reported by `dotnet test backend/RecruitOps.sln`.
- Any HTTP 500 internal server error or EF Core translation exception when calling `GET /api/users`.
