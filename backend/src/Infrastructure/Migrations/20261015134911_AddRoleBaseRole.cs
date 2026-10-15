using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <summary>ADR-0021 option A: a custom role's holders carry its base role.
    /// <para>Existing custom roles get <c>Recruiter</c> — the enum every holder already carried —
    /// so nothing anyone can reach changes until an Admin picks a base role. Holders of an
    /// <i>inactive</i> custom role move to <c>Interviewer</c> (no role policy admits it): under
    /// option A a deactivated role grants nothing on either axis.</para></summary>
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
                UPDATE "Roles" SET "BaseRole" = 'Recruiter' WHERE NOT "IsSystemRole";
                """);

            migrationBuilder.Sql("""
                UPDATE "Users" AS u SET "Role" = 'Interviewer'
                FROM "Roles" AS r
                WHERE u."RoleId" = r."Id" AND NOT r."IsSystemRole" AND NOT r."IsActive"
                  AND u."Role" <> 'SuperAdmin' AND NOT u."IsSuperAdmin";
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
                  AND u."Role" <> 'SuperAdmin' AND NOT u."IsSuperAdmin";
                """);

            migrationBuilder.DropColumn(
                name: "BaseRole",
                table: "Roles");
        }
    }
}
