using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <summary>ADR-0027: the audit log, append-only at the database too.
    /// <para>The triggers refuse UPDATE, DELETE and TRUNCATE on <c>AuditLogEntries</c>, so a bug, a
    /// cascade or a hand-written query cannot rewrite history. A database owner can still drop them —
    /// that is the line between append-only and tamper-evident (ADR-0027, Consequences).</para></summary>
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TargetType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_ActorUserId",
                table: "AuditLogEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_TargetId",
                table: "AuditLogEntries",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_TenantId_OccurredAt",
                table: "AuditLogEntries",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.Sql("""
                CREATE FUNCTION audit_log_entries_append_only() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'AuditLogEntries is append-only (ADR-0027): % refused', TG_OP;
                END;
                $$;

                CREATE TRIGGER audit_log_entries_no_update_or_delete
                    BEFORE UPDATE OR DELETE ON "AuditLogEntries"
                    FOR EACH ROW EXECUTE FUNCTION audit_log_entries_append_only();

                CREATE TRIGGER audit_log_entries_no_truncate
                    BEFORE TRUNCATE ON "AuditLogEntries"
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_log_entries_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_log_entries_no_truncate ON "AuditLogEntries";
                DROP TRIGGER IF EXISTS audit_log_entries_no_update_or_delete ON "AuditLogEntries";
                DROP FUNCTION IF EXISTS audit_log_entries_append_only();
                """);

            migrationBuilder.DropTable(
                name: "AuditLogEntries");
        }
    }
}
