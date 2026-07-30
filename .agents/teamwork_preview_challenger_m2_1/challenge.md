# Milestone 2 RBAC Seeding Challenge Report

## Challenge Summary

**Overall risk assessment**: LOW

## Empirical Test Results

- **Test Command**: `dotnet test backend/tests/RecruitOps.Domain.Tests --filter "FullyQualifiedName~RbacDomainTests"`
- **Result**: PASSED (9 Passed, 0 Failed, 0 Skipped)
- **Full Solution Test Command**: `dotnet test backend/RecruitOps.sln`
- **Result**: PASSED (181 total tests: 48 domain tests + 133 API tests passed)

## Findings & Empirical Verification

1. **Idempotency**:
   - `DbInitializer.SeedPermissionsAndRolesAsync` was tested with 1, 2, and 3 consecutive executions against the database.
   - Total Permission entity count remained identical (**34**).
   - Total System Role entity count remained identical (**7**).
   - Total `RolePermission` join table entity count remained identical (127 total permission assignments across all 7 system roles).
   - Multi-tenant User `RoleId` linking was confirmed idempotent.

2. **Canonical Permissions Count**:
   - Target requirement threshold: >= 29 canonical permissions.
   - Actual implemented canonical permissions: **34 permissions** across 9 functional modules:
     1. `requisitions` (5 permissions: read, create, update, delete, approve)
     2. `postings` (5 permissions: read, create, update, delete, publish)
     3. `applications` (5 permissions: read, create, update, delete, move_stage)
     4. `interviews` (4 permissions: read, create, update, cancel)
     5. `scorecards` (3 permissions: read, submit, manage_templates)
     6. `users` (4 permissions: read, create, update, delete)
     7. `roles` (4 permissions: read, create, update, delete)
     8. `settings` (2 permissions: read, update)
     9. `system` (2 permissions: manage, audit)
   - All 34 permissions adhere to the standard `permission:{module}:{feature}:{action}` naming convention.

3. **Default System Roles & Mappings**:
   - Created cleanly: **7 default system roles** (`IsSystemRole = true`).
   - Role breakdown & permission mapping count:
     - `SuperAdmin` (`IsSuperAdmin = true`): 34 permissions (full system permissions)
     - `Admin` (`IsSuperAdmin = false`): 33 permissions (all except `permission:system:system:manage`)
     - `HrDirector`: 26 permissions
     - `Recruiter`: 18 permissions
     - `HiringManager`: 11 permissions
     - `Approver`: 2 permissions (`permission:requisitions:requisitions:read`, `permission:requisitions:requisitions:approve`)
     - `Interviewer`: 3 permissions (`permission:interviews:interviews:read`, `permission:scorecards:scorecards:read`, `permission:scorecards:scorecards:submit`)

## Stress Test Results

- **Idempotency Harness (3x Sequential Seed Runs)** -> Expected: No duplicates in Permissions, Roles, or RolePermissions -> **PASS**
- **Canonical Permission Count (>= 29 threshold)** -> Expected: Exact 34 permissions -> **PASS**
- **System Role Count** -> Expected: Exact 7 system roles -> **PASS**
- **SuperAdmin & Admin Permission Exclusion Check** -> Expected: Admin lacks system manage permission -> **PASS**
- **Unassigned User Role Linking across Tenants** -> Expected: Links user.RoleId without errors -> **PASS**

## Challenges & Failure Modes Analyzed

### [Low] Concurrent Multi-Replica Initialization
- **Assumption challenged**: DbInitializer runs in a single process during application startup.
- **Attack scenario**: Multiple app replicas starting simultaneously against an unseeded database could execute `SeedPermissionsAndRolesAsync` concurrently.
- **Blast radius**: Database unique key constraint violation on `Permission.Code` or `Role.Code` during initial boot.
- **Mitigation**: Standard EF Core migration/seeding pattern (single-instance startup task or db migration lock) handles multi-container deployments cleanly.

## Unchallenged Areas
- Runtime dynamic permission evaluation policies (covered by API tests).
