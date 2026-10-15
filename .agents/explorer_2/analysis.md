# Technical Analysis: DB Migrations & RBAC Seeding (Requirement R2)

## 1. Executive Summary

Requirement R2 for RecruitOps Flow 3 mandates:
1. **Automated EF Core Database Migrations on Startup**: Ensuring pending migrations are automatically checked and applied on application startup in `Program.cs` / `DependencyInjection.cs` cleanly without data loss.
2. **Idempotent RBAC Seeding**: Ensuring system roles, canonical permissions, default tenant (`Company`), and initial SuperAdmin/Admin accounts are seeded idempotently without duplicate key errors, concurrency issues, or state corruption across multiple restarts.

This analysis evaluates the current implementation in `RecruitOps.Infrastructure` and `RecruitOps.Api`, verifies the migration pipeline and seeding logic, identifies key architectural guarantees and edge cases, and outlines recommendations for production readiness.

---

## 2. Component & Architecture Map

| Component | File Path | Key Responsibilities |
|---|---|---|
| **AppDbContext** | `backend/src/Infrastructure/Persistence/AppDbContext.cs` | Main EF Core DbContext mapping 28 domain entities, tenant query filters (`ITenantScoped`), and entity relationships. |
| **DatabaseStartup** | `backend/src/Infrastructure/Persistence/DatabaseStartup.cs` | Scope creation helper that inspects pending EF Core migrations and applies them via `MigrateAsync()`. |
| **DbInitializer** | `backend/src/Infrastructure/Persistence/DbInitializer.cs` | Idempotent seeding logic for canonical permissions, 7 system roles, default tenant company, and initial admin account. |
| **RbacSeedData** | `backend/src/Infrastructure/Persistence/RbacSeedData.cs` | Canonical definitions for 39 permissions across 10 modules and 7 system role definitions (`SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`). |
| **DependencyInjection** | `backend/src/Infrastructure/DependencyInjection.cs` | Service registration for `AppDbContext` (supporting Npgsql PostgreSQL or In-Memory provider) and infrastructure services. |
| **Program.cs** | `backend/src/Api/Program.cs` | Application entry point orchestrating WebApplication middleware, rate limiting, CORS, and startup migration/seeding execution. |

---

## 3. Database Migrations Analysis (Startup Hook & Integrity)

### 3.1 Migration File Inventory

The EF Core migration history contains 7 sequential migrations under `backend/src/Infrastructure/Migrations/`:

1. `20260727085909_InitialCreate.cs`: Initial schema for Companies, Departments, Users, UserDepartments, JobPostings, Candidates, JobApplications, ApplicationStageHistories, PortalLinks.
2. `20260727101933_Module1Requisitions.cs`: Schema for Requisitions, ApprovalChains, ApprovalChainSteps, RequisitionApprovals, JdTemplates.
3. `20260728023109_Module2Ats.cs`: JobChannelPosts and ATS stage updates.
4. `20260728061832_Module3Interviews.cs`: Interviews, InterviewParticipants, ScorecardTemplates, ScorecardCriteria, Scorecards, ScorecardResponses, Notes, NoteMentions.
5. `20260729162915_AddDynamicRbacDataModel.cs`: Dynamic RBAC data model (Roles, Permissions, RolePermissions tables + `User.RoleId` and `User.IsSuperAdmin` columns).
6. `20260807064955_AddRefreshTokenEntity.cs`: RefreshTokens table for JWT refresh token rotation.
7. `20260811000000_AddPgTrgmAndSearchIndexes.cs`: PostgreSQL `pg_trgm` extension enablement and GIN trigram indexes on `Candidates` (FullName, Email, Phone), `JobApplications` (ResumeExtractedText), `JobPostings` (Title, Description), `Requisitions` (Title, JobDescription), and `Departments` (Name).

### 3.2 Automated Startup Migration Logic

In `DatabaseStartup.cs` (`backend/src/Infrastructure/Persistence/DatabaseStartup.cs:15-55`):
```csharp
public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
{
    using var scope = services.CreateScope();
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
    var db = sp.GetRequiredService<AppDbContext>();

    // 1. In-memory safety check
    if (!db.Database.IsRelational())
    {
        logger.LogDebug("Non-relational provider in use; skipping migrations.");
        return;
    }

    // 2. Configuration toggle check
    var config = sp.GetRequiredService<IConfiguration>();
    if (string.Equals(config["Database:AutoMigrateOnStartup"], "false", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("Database:AutoMigrateOnStartup is false — skipping migrations. Apply them manually.");
        return;
    }

    // 3. Pending migration detection and execution
    var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
    if (pending.Count == 0)
    {
        logger.LogInformation("Database schema is up to date.");
        return;
    }

    logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
        pending.Count, string.Join(", ", pending));

    await db.Database.MigrateAsync(ct);
    logger.LogInformation("Database migrated successfully.");
}
```

### 3.3 Design Principles & Safeguards in Migration Pipeline

1. **Fail-Fast Strategy**: `MigrateAsync()` intentionally omits a catch-all `try/catch` block. If a database migration fails (e.g. invalid connection string, permission error, DB unavailable), the exception bubbles up and halts container startup. This prevents the application from starting in an inconsistent state or serving HTTP requests against a half-migrated schema.
2. **In-Memory Test Isolation**: `db.Database.IsRelational()` checks if the provider is relational. In unit/integration tests using EF Core `UseInMemoryDatabase()`, `IsRelational()` returns `false`, causing migration checks to log a debug message and exit cleanly without attempting SQL execution.
3. **Configuration Gating**: `Database:AutoMigrateOnStartup` allows ops engineers to disable automatic migrations if migrations are executed out-of-band via CI/CD deployment jobs.
4. **Idempotency & Data Safety**: EF Core tracks applied migrations in the `__EFMigrationsHistory` table. `MigrateAsync()` queries this table via `GetPendingMigrationsAsync()` and applies only unapplied migrations in transactional batches without modifying existing data.

---

## 4. RBAC Seeding & Idempotency Analysis

### 4.1 Structure of RbacSeedData.cs

`RbacSeedData.cs` (`backend/src/Infrastructure/Persistence/RbacSeedData.cs`) defines:
1. **Canonical Permissions** (`GetCanonicalPermissions()`): 39 permissions across 10 modules:
   - `requisitions` (5: read, create, update, delete, approve)
   - `postings` (5: read, create, update, delete, publish)
   - `applications` (5: read, create, update, delete, move_stage)
   - `interviews` (4: read, create, update, cancel)
   - `scorecards` (3: read, submit, manage_templates)
   - `users` (4: read, create, update, delete)
   - `roles` (4: read, create, update, delete)
   - `settings` (2: read, update)
   - `system` (2: manage, audit)
   - `ai` (5: parse, analyze, generate, prepare, translate)
2. **System Roles** (`GetSystemRoles()`): 7 default system roles:
   - `SuperAdmin`: `IsSuperAdmin = true`, assigned all 39 permissions.
   - `Admin`: `IsSuperAdmin = false`, assigned 38 permissions (all except `permission:system:system:manage`).
   - `HrDirector`: 31 permissions.
   - `Recruiter`: 23 permissions.
   - `HiringManager`: 11 permissions.
   - `Approver`: 2 permissions (`requisitions:read`, `requisitions:approve`).
   - `Interviewer`: 3 permissions (`interviews:read`, `scorecards:read`, `scorecards:submit`).

### 4.2 Pipeline in DbInitializer.cs

`DbInitializer.cs` (`backend/src/Infrastructure/Persistence/DbInitializer.cs`) implements a 4-step idempotent seeding pipeline:

```
[Start SeedAsync]
       │
       ▼
┌─────────────────────────────────────────────────────────────┐
│ Step 1: Seed Canonical Permissions                          │
│ - IgnoreQueryFilters() on db.Permissions                    │
│ - Diff against GetCanonicalPermissions() by Code            │
│ - Add missing permissions to db & SaveChangesAsync()        │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ Step 2: Seed System Roles & RolePermissions                 │
│ - IgnoreQueryFilters() on db.Roles                          │
│ - Add missing system roles / update existing system roles   │
│ - Sync RolePermission join records for each role            │
│ - SaveChangesAsync()                                        │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ Step 3: Default Tenant & Admin/SuperAdmin Seeding           │
│ - Read Seed:AdminEmail and Seed:AdminPassword from config   │
│ - If empty or email already exists -> skip                  │
│ - Create Default Company ("default" slug)                   │
│ - Create User with hashed password & Admin/SuperAdmin role  │
│ - SaveChangesAsync()                                        │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ Step 4: Link Legacy Users                                   │
│ - IgnoreQueryFilters() on db.Users where RoleId == null     │
│ - Match user.Role.ToString() to system role Code            │
│ - Set user.RoleId & user.IsSuperAdmin = matched.IsSuperAdmin│
│ - SaveChangesAsync()                                        │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 Idempotency Guarantees & Edge Cases

1. **Multi-Tenancy Query Filter Bypass (`IgnoreQueryFilters()`)**:
   - Query filters in `AppDbContext` filter entities by `TenantId == _tenant.TenantId`. During startup seeding, no tenant HTTP context exists (`_tenant.TenantId` is `Guid.Empty`).
   - `DbInitializer` explicitly invokes `.IgnoreQueryFilters()` on `Permissions`, `Roles`, and `Users`.
   - Without `.IgnoreQueryFilters()`, system roles (`TenantId == null`) would be invisible, causing duplicate insertion attempts on every startup.
2. **Re-Execution Safety**:
   - Permissions are keyed by `Code` (`permission:module:feature:action`). Existing permissions are loaded into a dictionary and skipped if present.
   - System roles are matched by `Code` (case-insensitive). Existing system roles are updated in-place; new permissions are added to `RolePermissions` without duplicating existing links.
   - Default tenant and Admin account creation checks `db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email)`. Re-running `SeedAsync` on an populated database is a strict no-op.
3. **Concurrency & Multi-Replica Edge Cases**:
   - In multi-container deployments where multiple API replicas boot concurrently against a fresh database, race conditions could occur if two instances attempt step 1 or step 2 simultaneously.
   - Database unique constraints (`IX_Permissions_Code`, `IX_Roles_TenantId_Code`, `IX_Users_Email`) protect schema integrity at the database layer.
   - Recommendation: Ensure seeding runs inside a database transaction or wrap `SeedAsync()` invocation in `DatabaseStartup` so migration and seeding occur sequentially within startup initialization.

---

## 5. Startup Integration in Program.cs

In `backend/src/Api/Program.cs` (lines 204-210):

```csharp
// Apply pending migrations before serving traffic (ADR-0004: unattended installs).
// No-ops on the in-memory provider used by tests.
await DatabaseStartup.MigrateAsync(app.Services);

if (app.Environment.IsDevelopment())
    await DbInitializer.SeedAsync(app.Services);
```

### Key Observation & Recommendation for Production Seeding:
Currently `DbInitializer.SeedAsync` is wrapped in `if (app.Environment.IsDevelopment())`.
However, canonical permissions and system roles seeded by `SeedPermissionsAndRolesAsync` are mandatory for the dynamic RBAC authorization engine to function in **all environments** (including `Production`).
- If a fresh production environment starts with `ASPNETCORE_ENVIRONMENT=Production`, `DbInitializer.SeedAsync` will be skipped, leaving `Roles` and `Permissions` tables empty and breaking login / authorization.
- **Recommendation**:
  - Unify `DatabaseStartup` or separate RBAC system seeding from sample development data seeding.
  - `SeedPermissionsAndRolesAsync` (and default tenant/admin creation if `Seed:AdminEmail` is configured) should execute on startup regardless of environment, while sample candidate/posting data (if any) remains dev-only.

---

## 6. Verification Results

### Baseline Test Suite Execution

The backend test suite was verified using the official .NET test runner:

1. `dotnet test backend/tests/RecruitOps.Domain.Tests/RecruitOps.Domain.Tests.csproj`
   - **Passed**: 51 tests (0 failed, 0 skipped).
   - Covered: `RbacDomainTests` (51 tests covering `RbacSeedData`, `DbInitializer.SeedPermissionsAndRolesAsync`, canonical permissions count = 39, system roles count = 7, idempotency over 3 consecutive executions).

2. `dotnet test backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj`
   - **Passed**: 403 tests (0 failed, 0 skipped).
   - Covered: API controllers, dynamic authorization handlers, rate limiting policies, and authentication endpoints.

3. **Total Backend Baseline**: **454 tests passed** cleanly across `RecruitOps.sln`.

---

## 7. Actionable Implementation Guidelines for Implementer

When implementing Requirement R2, the following exact changes should be made:

1. **Update `Program.cs` / `DatabaseStartup.cs`**:
   - Ensure `DatabaseStartup.MigrateAsync(app.Services)` runs before `app.Run()`.
   - Ensure `DbInitializer.SeedPermissionsAndRolesAsync` (or `DbInitializer.SeedAsync`) runs on startup in all environments so system roles and canonical permissions exist in production databases.
2. **Configuration Options**:
   - Ensure `Seed:AdminEmail` and `Seed:AdminPassword` are supported via environment variables (`Seed__AdminEmail`, `Seed__AdminPassword`).
3. **Idempotency Guard**:
   - Retain `.IgnoreQueryFilters()` in all seeding queries to ensure multi-tenant query filters do not interfere with system-level role and permission seeding.
