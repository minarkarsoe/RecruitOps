using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-0023 (revise-and-resubmit in rounds) and ADR-0024 (senior skip-ahead).
    ///
    /// Hand-adjusted after a schema review. The generated version ran all four operations in
    /// EF's single implicit transaction, with <c>DropIndex</c> first — and Postgres holds DDL
    /// locks until the transaction commits, not until the statement finishes. So the old
    /// index's ACCESS EXCLUSIVE lock covered the whole <c>CREATE INDEX</c> build, blocking
    /// reads *and* writes on RequisitionApprovals for its full duration rather than just
    /// writes. Reordered and made concurrent below.
    /// </summary>
    public partial class AddApprovalRoundAndActualDecider : Migration
    {
        // Kept in one constant so Up() and the invalid-index recovery note cannot drift apart.
        private const string NewIndex = "IX_RequisitionApprovals_RequisitionId_Round_Sequence";
        private const string OldIndex = "IX_RequisitionApprovals_RequisitionId_Sequence";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Both columns first. On Postgres 11+ adding a NOT NULL column with a *constant*
            //    default is a catalog-only change — no table rewrite, no backfill step needed.
            //    Round defaults to 1, so every pre-existing step correctly becomes round 1;
            //    DecidedByUserId is null, meaning "the assigned approver decided it", which is
            //    true of every row written before ADR-0024 existed.
            migrationBuilder.AddColumn<Guid>(
                name: "DecidedByUserId",
                table: "RequisitionApprovals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "RequisitionApprovals",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // 2. Build the new unique index CONCURRENTLY, outside a transaction. Slower, but it
            //    takes only a SHARE UPDATE EXCLUSIVE lock, so approvals keep being read and
            //    written while it builds. suppressTransaction is required — Postgres refuses
            //    CREATE INDEX CONCURRENTLY inside a transaction block.
            //
            //    Building this BEFORE dropping the old one also means the table is never
            //    without uniqueness enforcement, even for an instant.
            //
            //    ⚠️ If this statement fails or is interrupted, Postgres leaves behind an
            //    INVALID index that enforces nothing. Recovery is manual and is not automatic
            //    on re-run:
            //        SELECT indexrelid::regclass FROM pg_index WHERE NOT indisvalid;
            //        DROP INDEX CONCURRENTLY "IX_RequisitionApprovals_RequisitionId_Round_Sequence";
            //    then re-run this migration.
            migrationBuilder.Sql(
                $@"CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ""{NewIndex}""
                   ON ""RequisitionApprovals"" (""RequisitionId"", ""Round"", ""Sequence"");",
                suppressTransaction: true);

            // 3. Only now drop the old one. It is not merely redundant, it is actively wrong
            //    under ADR-0023: a resubmission legitimately reuses Sequence 1..n for the same
            //    requisition, which the old (RequisitionId, Sequence) constraint forbids.
            //    Cheap on its own — the exclusive lock is held for a catalog update, not a build.
            migrationBuilder.Sql(
                $@"DROP INDEX CONCURRENTLY IF EXISTS ""{OldIndex}"";",
                suppressTransaction: true);
        }

        /// <summary>
        /// ⚠️ NOT a safe blind rollback once this feature has seen real traffic. Read this
        /// before running it under incident pressure.
        ///
        /// 1. It is unconditionally destructive. Dropping DecidedByUserId erases the ADR-0024
        ///    record of who really approved a step on someone else's behalf, and dropping
        ///    Round erases which attempt each step belonged to. Neither is reconstructible
        ///    afterwards, and there is no export step here.
        ///
        /// 2. It can fail outright. If any requisition has been through a second round, the
        ///    rows are no longer unique on (RequisitionId, Sequence) alone, so recreating the
        ///    old unique index throws 23505.
        ///
        /// The operation order below is deliberate and is the one mitigation available here:
        /// the old index is rebuilt FIRST, while Round still exists. So on a database that has
        /// seen a second round this fails at step 1 and stops — before either column is
        /// dropped, with nothing destroyed. Dropping the columns first, as the generated
        /// version did, would destroy the data and *then* fail to restore the constraint,
        /// leaving the database in a state neither migration describes.
        ///
        /// Before running it, check:  SELECT count(*) FROM "RequisitionApprovals" WHERE "Round" > 1;
        /// If that is not 0, prefer rolling *forward* with a fix over rolling back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $@"CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ""{OldIndex}""
                   ON ""RequisitionApprovals"" (""RequisitionId"", ""Sequence"");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                $@"DROP INDEX CONCURRENTLY IF EXISTS ""{NewIndex}"";",
                suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "DecidedByUserId",
                table: "RequisitionApprovals");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "RequisitionApprovals");
        }
    }
}
