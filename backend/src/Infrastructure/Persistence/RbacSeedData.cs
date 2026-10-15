using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Persistence;

public static class RbacSeedData
{
    public class SystemRoleSeedDefinition
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSuperAdmin { get; set; }
        public string[] PermissionCodes { get; set; } = Array.Empty<string>();
    }

    public static List<Permission> GetCanonicalPermissions() => new()
    {
        // 1. Requisitions
        new Permission { Module = "requisitions", Feature = "requisitions", Action = "read", Name = "Read Requisitions", Description = "View job requisitions", Code = "permission:requisitions:requisitions:read" },
        new Permission { Module = "requisitions", Feature = "requisitions", Action = "create", Name = "Create Requisitions", Description = "Create new job requisitions", Code = "permission:requisitions:requisitions:create" },
        new Permission { Module = "requisitions", Feature = "requisitions", Action = "update", Name = "Update Requisitions", Description = "Update job requisitions", Code = "permission:requisitions:requisitions:update" },
        new Permission { Module = "requisitions", Feature = "requisitions", Action = "delete", Name = "Delete Requisitions", Description = "Delete job requisitions", Code = "permission:requisitions:requisitions:delete" },
        new Permission { Module = "requisitions", Feature = "requisitions", Action = "approve", Name = "Approve Requisitions", Description = "Approve or reject job requisitions", Code = "permission:requisitions:requisitions:approve" },

        // 2. Postings
        new Permission { Module = "postings", Feature = "postings", Action = "read", Name = "Read Postings", Description = "View job postings", Code = "permission:postings:postings:read" },
        new Permission { Module = "postings", Feature = "postings", Action = "create", Name = "Create Postings", Description = "Create job postings", Code = "permission:postings:postings:create" },
        new Permission { Module = "postings", Feature = "postings", Action = "update", Name = "Update Postings", Description = "Update job postings", Code = "permission:postings:postings:update" },
        new Permission { Module = "postings", Feature = "postings", Action = "delete", Name = "Delete Postings", Description = "Delete job postings", Code = "permission:postings:postings:delete" },
        new Permission { Module = "postings", Feature = "postings", Action = "publish", Name = "Publish Postings", Description = "Publish job postings to channels", Code = "permission:postings:postings:publish" },

        // 3. Applications
        new Permission { Module = "applications", Feature = "applications", Action = "read", Name = "Read Applications", Description = "View candidate applications", Code = "permission:applications:applications:read" },
        new Permission { Module = "applications", Feature = "applications", Action = "create", Name = "Create Applications", Description = "Create candidate applications", Code = "permission:applications:applications:create" },
        new Permission { Module = "applications", Feature = "applications", Action = "update", Name = "Update Applications", Description = "Update candidate applications", Code = "permission:applications:applications:update" },
        new Permission { Module = "applications", Feature = "applications", Action = "delete", Name = "Delete Applications", Description = "Delete candidate applications", Code = "permission:applications:applications:delete" },
        new Permission { Module = "applications", Feature = "applications", Action = "move_stage", Name = "Move Application Stage", Description = "Advance candidate application stages", Code = "permission:applications:applications:move_stage" },

        // 4. Interviews
        new Permission { Module = "interviews", Feature = "interviews", Action = "read", Name = "Read Interviews", Description = "View scheduled interviews", Code = "permission:interviews:interviews:read" },
        new Permission { Module = "interviews", Feature = "interviews", Action = "create", Name = "Schedule Interviews", Description = "Schedule candidate interviews", Code = "permission:interviews:interviews:create" },
        new Permission { Module = "interviews", Feature = "interviews", Action = "update", Name = "Update Interviews", Description = "Update scheduled interviews", Code = "permission:interviews:interviews:update" },
        new Permission { Module = "interviews", Feature = "interviews", Action = "cancel", Name = "Cancel Interviews", Description = "Cancel scheduled interviews", Code = "permission:interviews:interviews:cancel" },

        // 5. Scorecards
        new Permission { Module = "scorecards", Feature = "scorecards", Action = "read", Name = "Read Scorecards", Description = "View interview scorecards", Code = "permission:scorecards:scorecards:read" },
        new Permission { Module = "scorecards", Feature = "scorecards", Action = "submit", Name = "Submit Scorecards", Description = "Submit candidate evaluation scorecards", Code = "permission:scorecards:scorecards:submit" },
        new Permission { Module = "scorecards", Feature = "scorecards", Action = "manage_templates", Name = "Manage Scorecard Templates", Description = "Create and edit scorecard templates", Code = "permission:scorecards:scorecards:manage_templates" },

        // 6. Users
        new Permission { Module = "users", Feature = "users", Action = "read", Name = "Read Users", Description = "View user profiles", Code = "permission:users:users:read" },
        new Permission { Module = "users", Feature = "users", Action = "create", Name = "Create Users", Description = "Create new user accounts", Code = "permission:users:users:create" },
        new Permission { Module = "users", Feature = "users", Action = "update", Name = "Update Users", Description = "Update user accounts", Code = "permission:users:users:update" },
        new Permission { Module = "users", Feature = "users", Action = "delete", Name = "Delete Users", Description = "Deactivate or delete user accounts", Code = "permission:users:users:delete" },

        // 7. Roles
        new Permission { Module = "roles", Feature = "roles", Action = "read", Name = "Read Roles", Description = "View RBAC roles and permissions", Code = "permission:roles:roles:read" },
        new Permission { Module = "roles", Feature = "roles", Action = "create", Name = "Create Roles", Description = "Create custom RBAC roles", Code = "permission:roles:roles:create" },
        new Permission { Module = "roles", Feature = "roles", Action = "update", Name = "Update Roles", Description = "Update RBAC roles and permission assignments", Code = "permission:roles:roles:update" },
        new Permission { Module = "roles", Feature = "roles", Action = "delete", Name = "Delete Roles", Description = "Delete custom RBAC roles", Code = "permission:roles:roles:delete" },

        // 8. Settings
        new Permission { Module = "settings", Feature = "settings", Action = "read", Name = "Read Settings", Description = "View organization settings", Code = "permission:settings:settings:read" },
        new Permission { Module = "settings", Feature = "settings", Action = "update", Name = "Update Settings", Description = "Update organization settings", Code = "permission:settings:settings:update" },

        // 9. System
        new Permission { Module = "system", Feature = "system", Action = "manage", Name = "Manage System", Description = "Full administrative control over system infrastructure", Code = "permission:system:system:manage" },
        new Permission { Module = "system", Feature = "system", Action = "audit", Name = "Audit System", Description = "View system audit logs and metrics", Code = "permission:system:system:audit" },

        // 10. AI Services
        new Permission { Module = "ai", Feature = "resume", Action = "parse", Name = "Parse Resume", Description = "Parse and structure resume documents using Claude AI", Code = "permission:ai:resume:parse" },
        new Permission { Module = "ai", Feature = "matching", Action = "analyze", Name = "Analyze Candidate Matching", Description = "Perform detailed candidate-job matching analysis using Claude AI", Code = "permission:ai:matching:analyze" },
        new Permission { Module = "ai", Feature = "summary", Action = "generate", Name = "Generate Executive Summary", Description = "Generate executive summary and interview questions using Gemini AI", Code = "permission:ai:summary:generate" },
        new Permission { Module = "ai", Feature = "document", Action = "prepare", Name = "Prepare Dossier & Interview Kit", Description = "Prepare client dossiers and interview kits using Gemini AI", Code = "permission:ai:document:prepare" },
        new Permission { Module = "ai", Feature = "localization", Action = "translate", Name = "Burmese Localization", Description = "Translate content between English and Burmese using Gemini AI", Code = "permission:ai:localization:translate" }
    };

    public static List<SystemRoleSeedDefinition> GetSystemRoles() => new()
    {
        new SystemRoleSeedDefinition
        {
            Code = "SuperAdmin",
            Name = "SuperAdmin",
            Description = "Super Administrator with unrestricted access across all tenants and features",
            IsSuperAdmin = true,
            PermissionCodes = GetCanonicalPermissions().Select(p => p.Code).ToArray()
        },
        new SystemRoleSeedDefinition
        {
            Code = "Admin",
            Name = "Admin",
            Description = "Tenant Administrator with full access to tenant management, users, roles, and settings",
            IsSuperAdmin = false,
            PermissionCodes = GetCanonicalPermissions().Where(p => p.Code != "permission:system:system:manage").Select(p => p.Code).ToArray()
        },
        new SystemRoleSeedDefinition
        {
            Code = "HrDirector",
            Name = "HR Director",
            Description = "HR Director managing talent acquisition, requisitions, postings, applications, scorecards, and reports",
            IsSuperAdmin = false,
            PermissionCodes = new[]
            {
                "permission:requisitions:requisitions:read", "permission:requisitions:requisitions:create", "permission:requisitions:requisitions:update", "permission:requisitions:requisitions:delete", "permission:requisitions:requisitions:approve",
                "permission:postings:postings:read", "permission:postings:postings:create", "permission:postings:postings:update", "permission:postings:postings:delete", "permission:postings:postings:publish",
                "permission:applications:applications:read", "permission:applications:applications:create", "permission:applications:applications:update", "permission:applications:applications:delete", "permission:applications:applications:move_stage",
                "permission:interviews:interviews:read", "permission:interviews:interviews:create", "permission:interviews:interviews:update", "permission:interviews:interviews:cancel",
                "permission:scorecards:scorecards:read", "permission:scorecards:scorecards:submit", "permission:scorecards:scorecards:manage_templates",
                "permission:users:users:read", "permission:roles:roles:read", "permission:settings:settings:read", "permission:system:system:audit",
                "permission:ai:resume:parse", "permission:ai:matching:analyze", "permission:ai:summary:generate", "permission:ai:document:prepare", "permission:ai:localization:translate"
            }
        },
        new SystemRoleSeedDefinition
        {
            Code = "Recruiter",
            Name = "Recruiter",
            Description = "Recruiter managing job postings, candidate applications, interview scheduling, and evaluations",
            IsSuperAdmin = false,
            PermissionCodes = new[]
            {
                // Create and update are granted together on purpose. Under ADR-0022 the
                // requisition flow is create -> edit the draft -> submit, and both the edit
                // and the submit endpoints are gated on `update`. Granting `create` alone
                // would let a recruiter raise a requisition they could then neither correct
                // nor submit, which is worse than not being able to raise one at all.
                // Approve is deliberately NOT granted: raising headcount and approving it
                // are different authorities, and the chain decides the second one.
                "permission:requisitions:requisitions:read", "permission:requisitions:requisitions:create", "permission:requisitions:requisitions:update",
                "permission:postings:postings:read", "permission:postings:postings:create", "permission:postings:postings:update", "permission:postings:postings:delete", "permission:postings:postings:publish",
                "permission:applications:applications:read", "permission:applications:applications:create", "permission:applications:applications:update", "permission:applications:applications:delete", "permission:applications:applications:move_stage",
                "permission:interviews:interviews:read", "permission:interviews:interviews:create", "permission:interviews:interviews:update", "permission:interviews:interviews:cancel",
                "permission:scorecards:scorecards:read", "permission:scorecards:scorecards:submit",
                "permission:users:users:read",
                "permission:ai:resume:parse", "permission:ai:matching:analyze", "permission:ai:summary:generate", "permission:ai:document:prepare", "permission:ai:localization:translate"
            }
        },
        new SystemRoleSeedDefinition
        {
            Code = "HiringManager",
            Name = "Hiring Manager",
            Description = "Department manager submitting requisitions, reviewing department applications, and conducting interviews",
            IsSuperAdmin = false,
            PermissionCodes = new[]
            {
                "permission:requisitions:requisitions:read", "permission:requisitions:requisitions:create", "permission:requisitions:requisitions:update",
                "permission:postings:postings:read",
                "permission:applications:applications:read", "permission:applications:applications:move_stage",
                "permission:interviews:interviews:read", "permission:interviews:interviews:create", "permission:interviews:interviews:update",
                "permission:scorecards:scorecards:read", "permission:scorecards:scorecards:submit"
            }
        },
        new SystemRoleSeedDefinition
        {
            Code = "Approver",
            Name = "Approver",
            Description = "Approver responsible for reviewing and approving job requisitions",
            IsSuperAdmin = false,
            PermissionCodes = new[]
            {
                "permission:requisitions:requisitions:read", "permission:requisitions:requisitions:approve"
            }
        },
        new SystemRoleSeedDefinition
        {
            Code = "Interviewer",
            Name = "Interviewer",
            Description = "Interviewer evaluating candidates on interview panels",
            IsSuperAdmin = false,
            PermissionCodes = new[]
            {
                "permission:interviews:interviews:read", "permission:scorecards:scorecards:read", "permission:scorecards:scorecards:submit"
            }
        }
    };
}
