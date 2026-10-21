using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDistinctCandidatePairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Applied at startup (ADR-0004). Adding the column takes a brief ACCESS EXCLUSIVE lock on
            // "Candidates" (metadata only on PostgreSQL 11+: a constant default rewrites nothing), and
            // the two foreign keys a SHARE ROW EXCLUSIVE one. Queued behind a long transaction, that
            // lock would hold every later read and write of candidates behind it; give up after 10 s
            // instead, so startup fails loudly and is retried while the old instance keeps serving.
            // SET LOCAL lasts only for this migration's transaction.
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            migrationBuilder.AddColumn<Guid>(
                name: "MergeStamp",
                table: "Candidates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "DistinctCandidatePairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistinctCandidatePairs", x => x.Id);
                    table.CheckConstraint("ck_distinct_candidate_pair_ordered", "\"FirstCandidateId\" < \"SecondCandidateId\"");
                    table.ForeignKey(
                        name: "FK_DistinctCandidatePairs_Candidates_FirstCandidateId",
                        column: x => x.FirstCandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistinctCandidatePairs_Candidates_SecondCandidateId",
                        column: x => x.SecondCandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistinctCandidatePairs_FirstCandidateId",
                table: "DistinctCandidatePairs",
                column: "FirstCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DistinctCandidatePairs_SecondCandidateId",
                table: "DistinctCandidatePairs",
                column: "SecondCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DistinctCandidatePairs_Tenant_First_Second",
                table: "DistinctCandidatePairs",
                columns: new[] { "TenantId", "FirstCandidateId", "SecondCandidateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistinctCandidatePairs");

            migrationBuilder.DropColumn(
                name: "MergeStamp",
                table: "Candidates");
        }
    }
}
