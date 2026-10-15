using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvIngestionAndAiProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsZawgyiNormalized",
                table: "JobApplications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResumeExtractedText",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeFileKey",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeFileName",
                table: "JobApplications",
                type: "text",
                nullable: true);

            // Moved here from 20260811000000_AddPgTrgmAndSearchIndexes, which sorts earlier and so
            // tried to index this column before it existed — "42703: column ResumeExtractedText does
            // not exist" on every fresh database. An index belongs in the migration that creates the
            // column it covers.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResumeUploadedAt",
                table: "JobApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_JobApplications_ResumeExtractedText_Trgm\" ON \"JobApplications\" USING gin (\"ResumeExtractedText\" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_JobApplications_ResumeExtractedText_Trgm\";");

            migrationBuilder.DropColumn(
                name: "IsZawgyiNormalized",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ResumeExtractedText",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ResumeFileKey",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ResumeFileName",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ResumeUploadedAt",
                table: "JobApplications");
        }
    }
}
