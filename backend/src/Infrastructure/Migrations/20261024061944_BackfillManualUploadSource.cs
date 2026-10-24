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
            migrationBuilder.Sql("""
                UPDATE "JobApplications" a
                SET "Source" = 'ManualUpload'
                WHERE a."Source" = 'Direct'
                  AND EXISTS (SELECT 1 FROM "BulkUploadFiles" f WHERE f."JobApplicationId" = a."Id");
                """);

            // A candidate's channel is their first touch: only those whose first application was an upload.
            // An upload that matched someone who had applied on the career page leaves them as they were.
            migrationBuilder.Sql("""
                UPDATE "Candidates" c
                SET "Source" = 'ManualUpload'
                WHERE c."Source" = 'Direct'
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
            // channel is lost; nothing else is.
            migrationBuilder.Sql("""
                UPDATE "JobApplications" SET "Source" = 'Direct' WHERE "Source" IN ('Viber', 'ManualUpload');
                UPDATE "Candidates" SET "Source" = 'Direct' WHERE "Source" IN ('Viber', 'ManualUpload');
                """);
        }
    }
}
