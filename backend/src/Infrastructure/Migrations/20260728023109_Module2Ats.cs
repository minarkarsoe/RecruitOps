using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Module2Ats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplyCount",
                table: "PortalLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "PortalLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRevoked",
                table: "PortalLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "PortalLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "PortalLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "RequisitionId",
                table: "JobPostings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationFormFieldsJson",
                table: "JobPostings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "JobPostings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "JobPostings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "JobPostings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Headcount",
                table: "JobPostings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "JobPostings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                table: "JobPostings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMax",
                table: "JobPostings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMin",
                table: "JobPostings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowSalary",
                table: "JobPostings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "JobPostings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppliedAt",
                table: "JobApplications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CoverNote",
                table: "JobApplications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldsJson",
                table: "JobApplications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "JobApplications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoCandidateId",
                table: "Candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Candidates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationStageHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStageHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationStageHistories_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalLinks_Token",
                table: "PortalLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_RequisitionId",
                table: "JobPostings",
                column: "RequisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_TenantId_Status",
                table: "JobPostings",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_TenantId_Email",
                table: "Candidates",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_TenantId_Phone",
                table: "Candidates",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStageHistories_JobApplicationId_ChangedAt",
                table: "ApplicationStageHistories",
                columns: new[] { "JobApplicationId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_JobPostings_Requisitions_RequisitionId",
                table: "JobPostings",
                column: "RequisitionId",
                principalTable: "Requisitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobPostings_Requisitions_RequisitionId",
                table: "JobPostings");

            migrationBuilder.DropTable(
                name: "ApplicationStageHistories");

            migrationBuilder.DropIndex(
                name: "IX_PortalLinks_Token",
                table: "PortalLinks");

            migrationBuilder.DropIndex(
                name: "IX_JobPostings_RequisitionId",
                table: "JobPostings");

            migrationBuilder.DropIndex(
                name: "IX_JobPostings_TenantId_Status",
                table: "JobPostings");

            migrationBuilder.DropIndex(
                name: "IX_Candidates_TenantId_Email",
                table: "Candidates");

            migrationBuilder.DropIndex(
                name: "IX_Candidates_TenantId_Phone",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "ApplyCount",
                table: "PortalLinks");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "PortalLinks");

            migrationBuilder.DropColumn(
                name: "IsRevoked",
                table: "PortalLinks");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "PortalLinks");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "PortalLinks");

            migrationBuilder.DropColumn(
                name: "ApplicationFormFieldsJson",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "Headcount",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "SalaryMax",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "SalaryMin",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "ShowSalary",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "CoverNote",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "CustomFieldsJson",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "MergedIntoCandidateId",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Candidates");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequisitionId",
                table: "JobPostings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
