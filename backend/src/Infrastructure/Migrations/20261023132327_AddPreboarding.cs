using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adds columns to Companies: wait at most 10 s for the lock rather than queueing every request
            // behind a long transaction. If startup fails with 55P03, restart and it retries.
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            migrationBuilder.AddColumn<string>(
                name: "AdminHandoffEmail",
                table: "Companies",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItHandoffEmail",
                table: "Companies",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreboardingRequired",
                table: "Companies",
                type: "integer",
                nullable: false,
                // Existing companies get the default (Preboarding.DefaultRequired: NRC, bank, education).
                // Deliberately not in the model: see AppDbContext.
                defaultValue: 7);

            migrationBuilder.CreateTable(
                name: "PreboardingDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreboardingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreboardingDocuments_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreboardingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Required = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemindedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReminderCount = table.Column<int>(type: "integer", nullable: false),
                    HandoffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreboardingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreboardingRequests_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreboardingFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreboardingDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreboardingFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreboardingFiles_PreboardingDocuments_PreboardingDocumentId",
                        column: x => x.PreboardingDocumentId,
                        principalTable: "PreboardingDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreboardingDocuments_OfferId_Kind",
                table: "PreboardingDocuments",
                columns: new[] { "OfferId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreboardingFiles_PreboardingDocumentId",
                table: "PreboardingFiles",
                column: "PreboardingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PreboardingRequests_OfferId",
                table: "PreboardingRequests",
                column: "OfferId",
                unique: true);

            // Offers accepted before this release are asked for their company's default set, as a newly
            // accepted one is (ADR-0030 §5b).
            migrationBuilder.Sql("""
                INSERT INTO "PreboardingRequests" ("Id", "TenantId", "OfferId", "Required", "ReminderCount", "CreatedAt")
                SELECT gen_random_uuid(), o."TenantId", o."Id", c."PreboardingRequired", 0, now()
                FROM "Offers" o
                JOIN "Companies" c ON c."Id" = o."TenantId"
                WHERE o."Status" = 'Accepted'
                  AND NOT EXISTS (SELECT 1 FROM "PreboardingRequests" r WHERE r."OfferId" = o."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            // ⚠️ Down drops candidates' joining documents — the rows that name every NRC scan and bank book in
            // object storage, and typed bank details. It refuses while any exist: delete the objects under
            // preboarding/ and these rows first (deployment-runbook.md §3, "Rolling back past AddPreboarding").
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "PreboardingDocuments") THEN
                        RAISE EXCEPTION 'PreboardingDocuments holds candidates'' joining documents. Delete their objects under preboarding/ and the rows, then roll back.';
                    END IF;
                END $$;
                """);

            // The older release has no handler for these kinds; a pending one would sit in its outbox retrying.
            migrationBuilder.Sql(
                "DELETE FROM \"OutboundMessages\" WHERE \"Kind\" IN ('PreboardingHandoff', 'PreboardingSubmitted', 'PreboardingReturned', 'PreboardingReminder');");

            migrationBuilder.DropTable(
                name: "PreboardingFiles");

            migrationBuilder.DropTable(
                name: "PreboardingRequests");

            migrationBuilder.DropTable(
                name: "PreboardingDocuments");

            migrationBuilder.DropColumn(
                name: "AdminHandoffEmail",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ItHandoffEmail",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PreboardingRequired",
                table: "Companies");
        }
    }
}
