using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Applied at startup (ADR-0004). The three new columns are metadata-only on PostgreSQL
            // 11+ (constant defaults), but each takes a brief ACCESS EXCLUSIVE lock, and the offer's
            // foreign key a SHARE ROW EXCLUSIVE one on "JobApplications" — which public applications
            // write to. Give up after 10 s rather than queue every writer behind a long transaction;
            // startup fails loudly and is retried (as AddDistinctCandidatePairs).
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            migrationBuilder.AddColumn<bool>(
                name: "RequireOfferApproval",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOfferSalaryToHiringManagers",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "ApprovalChains",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Requisition");

            migrationBuilder.CreateTable(
                name: "OfferTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Allowances = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    JoiningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProbationMonths = table.Column<int>(type: "integer", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    OfferTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    LetterBody = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offers_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_OfferTemplates_OfferTemplateId",
                        column: x => x.OfferTemplateId,
                        principalTable: "OfferTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OfferApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferApprovals_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfferApprovals_ApproverUserId_Decision",
                table: "OfferApprovals",
                columns: new[] { "ApproverUserId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferApprovals_OfferId_Round_Sequence",
                table: "OfferApprovals",
                columns: new[] { "OfferId", "Round", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Offers_JobApplicationId",
                table: "Offers",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_OfferTemplateId",
                table: "Offers",
                column: "OfferTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_OneOpenPerApplication",
                table: "Offers",
                column: "JobApplicationId",
                unique: true,
                filter: "\"Status\" NOT IN ('Rejected', 'Expired')");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_TenantId_Status",
                table: "Offers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Offers_TenantId_Year_Number",
                table: "Offers",
                columns: new[] { "TenantId", "Year", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfferTemplates_TenantId_Name",
                table: "OfferTemplates",
                columns: new[] { "TenantId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfferApprovals");

            migrationBuilder.DropTable(
                name: "Offers");

            migrationBuilder.DropTable(
                name: "OfferTemplates");

            migrationBuilder.DropColumn(
                name: "RequireOfferApproval",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ShowOfferSalaryToHiringManagers",
                table: "Companies");

            // Without the column every chain reads as a requisition chain again, so an offer chain would
            // start approving requisitions. Remove them first (their steps cascade). The same risk
            // exists for an app-only rollback that keeps this schema: deactivate offer chains then
            // (deployment runbook, upgrades).
            migrationBuilder.Sql("DELETE FROM \"ApprovalChains\" WHERE \"Purpose\" = 'Offer';");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "ApprovalChains");
        }
    }
}
