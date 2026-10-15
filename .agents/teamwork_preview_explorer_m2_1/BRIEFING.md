# BRIEFING — 2026-07-29T16:26:00Z

## Mission
Investigate and design domain entities for Requirement R2 (Granular Dynamic RBAC Data Model & Super-Admin) in RecruitOps, ensuring backwards compatibility for `User.Role` enum.

## 🔒 My Identity
- Archetype: Explorer
- Roles: Domain/Database Analyst & Designer
- Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1
- Original parent: c4c3e39d-ffc9-485f-87b2-94418da7d123
- Milestone: Milestone 2 (Dynamic RBAC & Super-Admin)

## 🔒 Key Constraints
- Read-only investigation — do NOT implement domain entity code directly in backend project.
- Design `Role`, `Permission`, `RolePermission`, Super-Admin representation.
- Ensure backwards compatibility so `User.Role` enum maps transparently to standard system roles.

## Current Parent
- Conversation ID: c4c3e39d-ffc9-485f-87b2-94418da7d123
- Updated: 2026-07-29T16:26:00Z

## Investigation State
- **Explored paths**: `backend/src/Domain/Entities/User.cs`, `UserRole.cs`, `AppDbContext.cs`, `DbInitializer.cs`, `RoleScope.cs`, `CurrentUser.cs`, `ICurrentUser.cs`
- **Key findings**: Designed `Role`, `Permission`, `RolePermission` entities, Super-Admin cross-tenant design (`TenantId = null`, `IsSuperAdmin = true`), and backwards-compatibility bridge preserving `User.Role` enum.
- **Unexplored areas**: None for R2 domain entity design scope.

## Key Decisions Made
1. `Role.TenantId` is `Guid?` (null for global system roles, set for tenant custom roles).
2. `Permission.Code` formatted as `permission:<module>:<feature>:<action>`.
3. `User` entity augmented with `RoleId` FK, `CustomRole` navigation, `IsSuperAdmin` flag, retaining `Role` enum property.
4. EF Core query filter for `Role`: `e.TenantId == null || e.TenantId == _tenant.TenantId`.

## Artifact Index
- ORIGINAL_REQUEST.md — Original user prompt
- BRIEFING.md — Mission & briefing state
- analysis.md — Detailed architectural & domain model analysis report
- handoff.md — 5-component handoff report
