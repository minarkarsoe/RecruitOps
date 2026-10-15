using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPgTrgmAndSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Enable pg_trgm extension for PostgreSQL trigram indexing
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // 2. Candidates table trigram indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Candidates_FullName_Trgm\" ON \"Candidates\" USING gin (\"FullName\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Candidates_Email_Trgm\" ON \"Candidates\" USING gin (\"Email\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Candidates_Phone_Trgm\" ON \"Candidates\" USING gin (\"Phone\" gin_trgm_ops);");

            // 3. JobApplications table trigram index for CV extracted text
            // The JobApplications.ResumeExtractedText trigram index is NOT created here. That column
            // is added by 20260812141548_AddCvIngestionAndAiProfileFields, which sorts *after* this
            // migration, so indexing it here fails on any fresh database with
            // "42703: column ResumeExtractedText does not exist". The index is created in that later
            // migration instead, beside the column it covers.

            // 4. JobPostings table trigram indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_JobPostings_Title_Trgm\" ON \"JobPostings\" USING gin (\"Title\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_JobPostings_Description_Trgm\" ON \"JobPostings\" USING gin (\"Description\" gin_trgm_ops);");

            // 5. Requisitions table trigram indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Requisitions_Title_Trgm\" ON \"Requisitions\" USING gin (\"Title\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Requisitions_JobDescription_Trgm\" ON \"Requisitions\" USING gin (\"JobDescription\" gin_trgm_ops);");

            // 6. Departments table trigram index
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Departments_Name_Trgm\" ON \"Departments\" USING gin (\"Name\" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Departments_Name_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Requisitions_JobDescription_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Requisitions_Title_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_JobPostings_Description_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_JobPostings_Title_Trgm\";");
            // Paired with the note in Up(): this index is created and dropped by
            // 20260812141548_AddCvIngestionAndAiProfileFields, not here.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Candidates_Phone_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Candidates_Email_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Candidates_FullName_Trgm\";");

            // Drop extension
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS pg_trgm;");
        }
    }
}
