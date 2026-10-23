using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable or constant-default columns on "Offers": metadata-only, no rewrite. The ACCESS
            // EXCLUSIVE lock they take is held until this migration's transaction commits, on a table the
            // dashboard reads. Give up after 10 s rather than queue behind a long transaction; startup fails
            // loudly and is retried (as AddOffers).
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "Offers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRemindedAt",
                table: "Offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalToken",
                table: "Offers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderCount",
                table: "Offers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                table: "Offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentToEmail",
                table: "Offers",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ViewedAt",
                table: "Offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OfferPortalAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SessionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SessionExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferPortalAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferPortalAccesses_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfferSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SentToEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    LetterText = table.Column<string>(type: "text", nullable: false),
                    LetterSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferSignatures_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfferPortalAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferPortalAccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferPortalAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferPortalAttempts_OfferPortalAccesses_OfferPortalAccessId",
                        column: x => x.OfferPortalAccessId,
                        principalTable: "OfferPortalAccesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Offers_PortalToken",
                table: "Offers",
                column: "PortalToken",
                unique: true,
                filter: "\"PortalToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfferPortalAccesses_OfferId_CreatedAt",
                table: "OfferPortalAccesses",
                columns: new[] { "OfferId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferPortalAccesses_SessionHash",
                table: "OfferPortalAccesses",
                column: "SessionHash",
                unique: true,
                filter: "\"SessionHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfferPortalAttempts_OfferPortalAccessId",
                table: "OfferPortalAttempts",
                column: "OfferPortalAccessId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferSignatures_OfferId",
                table: "OfferSignatures",
                column: "OfferId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            // ⚠️ Down drops the signature records — the evidence of every acceptance. It refuses while any
            // exist: archive them first (deployment-runbook.md §3, "Rolling back past AddOfferPortal").
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "OfferSignatures") THEN
                        RAISE EXCEPTION 'OfferSignatures holds acceptance evidence. Archive the table, delete its rows, then roll back.';
                    END IF;
                END $$;
                """);

            // The older release cannot read these kinds; a pending one would stall its outbox worker.
            migrationBuilder.Sql(
                "DELETE FROM \"OutboundMessages\" WHERE \"Kind\" IN ('OfferAccessCode', 'OfferAnswered', 'OfferAnswerNotice');");

            migrationBuilder.DropTable(
                name: "OfferPortalAttempts");

            migrationBuilder.DropTable(
                name: "OfferSignatures");

            migrationBuilder.DropTable(
                name: "OfferPortalAccesses");

            migrationBuilder.DropIndex(
                name: "IX_Offers_PortalToken",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "LastRemindedAt",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "PortalToken",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "ReminderCount",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "SentToEmail",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "ViewedAt",
                table: "Offers");
        }
    }
}
