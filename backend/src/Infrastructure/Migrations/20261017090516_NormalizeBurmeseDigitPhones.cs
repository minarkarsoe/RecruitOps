using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <summary>
    /// Rewrites candidate phones stored in Burmese digits into the ASCII form ContactNormalizer
    /// now produces (Module 2.7). Until 2026-10-17 a number typed on a Myanmar keyboard was kept
    /// in Burmese digits, so it never matched the same number typed in ASCII — duplicate
    /// detection and phone search both missed it. The country-code reduction the old code skipped
    /// for Burmese input ("+၉၅ ၉…" was stored as "၉၅၉…") is applied here too.
    ///
    /// <para>Only rows that contain Myanmar digits are touched; ASCII rows were already in this
    /// form. Two candidates that were created apart for one person stay apart — merging them needs
    /// a person (NEXT-SESSION has the query that lists them).</para>
    /// </summary>
    public partial class NormalizeBurmeseDigitPhones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ၀–၉ (U+1040–1049) and the Shan digits ႐–႙ (U+1090–1099), mapped to 0–9.
            migrationBuilder.Sql("""
                UPDATE "Candidates" AS c
                SET "Phone" = CASE
                        WHEN s.t LIKE '0095%' THEN '0' || substr(s.t, 5)
                        WHEN s.t LIKE '95%' AND length(s.t) >= 10 THEN '0' || substr(s.t, 3)
                        ELSE s.t
                    END
                FROM (
                    SELECT "Id",
                           translate("Phone", '၀၁၂၃၄၅၆၇၈၉႐႑႒႓႔႕႖႗႘႙', '01234567890123456789') AS t
                    FROM "Candidates"
                    WHERE "Phone" IS NOT NULL
                ) AS s
                WHERE c."Id" = s."Id" AND s.t <> c."Phone";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the ASCII form is the same number, and which script each row was
            // typed in was not kept.
        }
    }
}
