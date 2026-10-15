using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <summary>ADR-0021 option A: a custom role's holders carry its base role.
    /// <para>Each existing custom role takes the enum its holders already carry, when they all
    /// carry the same assignable one — so nothing anyone can reach changes until an Admin picks a
    /// base role. That is <c>Recruiter</c> for almost every role; before 2026-10-15 a role coded
    /// like an enum (<c>"4"</c>, <c>"APPROVER"</c>) gave its holders that enum instead, and those
    /// keep it (security review of option A, finding 3). Roles with no holders, or holders that
    /// disagree, get <c>Recruiter</c>. Holders of an <i>inactive</i> custom role move to
    /// <c>Interviewer</c>: under option A a deactivated role grants nothing on either axis.</para>
    /// <para>Holders whose enum is Admin or SuperAdmin are not touched here or by later role edits;
    /// NEXT-SESSION has the audit query to run before deploying.</para></summary>
    public partial class AddRoleBaseRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseRole",
                table: "Roles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Roles" AS r SET "BaseRole" = COALESCE((
                    SELECT MIN(u."Role") FROM "Users" AS u
                    WHERE u."RoleId" = r."Id"
                    HAVING COUNT(DISTINCT u."Role") = 1
                       AND MIN(u."Role") IN ('HrDirector', 'Recruiter', 'HiringManager', 'Approver', 'Interviewer')
                ), 'Recruiter')
                WHERE NOT r."IsSystemRole";
                """);

            migrationBuilder.Sql("""
                UPDATE "Users" AS u SET "Role" = 'Interviewer'
                FROM "Roles" AS r
                WHERE u."RoleId" = r."Id" AND NOT r."IsSystemRole" AND NOT r."IsActive"
                  AND u."Role" NOT IN ('SuperAdmin', 'Admin') AND NOT u."IsSuperAdmin";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to what every custom-role holder carried before option A.
            migrationBuilder.Sql("""
                UPDATE "Users" AS u SET "Role" = 'Recruiter'
                FROM "Roles" AS r
                WHERE u."RoleId" = r."Id" AND NOT r."IsSystemRole"
                  AND u."Role" NOT IN ('SuperAdmin', 'Admin') AND NOT u."IsSuperAdmin";
                """);

            migrationBuilder.DropColumn(
                name: "BaseRole",
                table: "Roles");
        }
    }
}
