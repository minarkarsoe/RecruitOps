using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillManualUploadSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only: Source is stored by name, so the new values need no schema change (ADR-0032 §1).
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            // Every application a bulk upload created was written as Direct; it was a recruiter's upload.
            // Since the durable queue (2026-08-21) each one has a BulkUploadFiles row.
            migrationBuilder.Sql("""
                UPDATE "JobApplications" a
                SET "Source" = 'ManualUpload'
                WHERE a."Source" = 'Direct'
                  AND EXISTS (SELECT 1 FROM "BulkUploadFiles" f WHERE f."JobApplicationId" = a."Id");
                """);

            // The first bulk upload (2026-08-09 to 08-21) wrote no file rows. Both versions mark the arrival
            // in the stage history with this note, and nothing else writes it. A separate statement, so each
            // plans as a plain semi-join.
            migrationBuilder.Sql("""
                UPDATE "JobApplications" a
                SET "Source" = 'ManualUpload'
                WHERE a."Source" = 'Direct'
                  AND EXISTS (SELECT 1 FROM "ApplicationStageHistories" h
                              WHERE h."JobApplicationId" = a."Id"
                                AND h."FromStatus" IS NULL
                                AND h."Note" = 'Created via Bulk CV Upload');
                """);

            // A candidate's channel is their first touch: only those whose first application was an upload.
            // An upload that matched someone who had applied on the career page leaves them as they were. A
            // merge moved the absorbed record's applications to the survivor, so a survivor's "first" is the
            // earliest across both people — rare, and only the candidate's label, never the analytics.
            // The EXISTS limits the per-candidate lookup to those with an upload at all.
            migrationBuilder.Sql("""
                UPDATE "Candidates" c
                SET "Source" = 'ManualUpload'
                WHERE c."Source" = 'Direct'
                  AND EXISTS (SELECT 1 FROM "JobApplications" u WHERE u."CandidateId" = c."Id" AND u."Source" = 'ManualUpload')
                  AND (SELECT a."Source" FROM "JobApplications" a
                       WHERE a."CandidateId" = c."Id"
                       ORDER BY a."AppliedAt", a."Id"
                       LIMIT 1) = 'ManualUpload';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '10s';");

            // The release before this one cannot read Viber or ManualUpload — EF fails on an enum name it does
            // not know — so rolling back maps both to Direct, as that release wrote them. Applicants' Viber
            // channel is lost. JobChannelPosts shares the enum; nothing writes it yet, and this keeps it so.
            migrationBuilder.Sql("""
                UPDATE "JobApplications" SET "Source" = 'Direct' WHERE "Source" IN ('Viber', 'ManualUpload');
                UPDATE "Candidates" SET "Source" = 'Direct' WHERE "Source" IN ('Viber', 'ManualUpload');
                UPDATE "JobChannelPosts" SET "Channel" = 'Direct' WHERE "Channel" IN ('Viber', 'ManualUpload');
                """);
        }
    }
}
