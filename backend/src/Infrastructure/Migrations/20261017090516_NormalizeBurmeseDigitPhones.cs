using System.Globalization;
using System.Text;
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
            // The rewrite below depends on character semantics; on a non-UTF8 database translate()
            // would work per byte and garble rows rather than fail. Refuse instead.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF getdatabaseencoding() <> 'UTF8' THEN
                        RAISE EXCEPTION 'NormalizeBurmeseDigitPhones needs a UTF8 database (this one is %)', getdatabaseencoding();
                    END IF;
                END $$;
                """);

            // Every non-ASCII decimal digit, from the same Unicode data ContactNormalizer reads
            // (CharUnicodeInfo), so the two cannot disagree: Myanmar ၀–၉ and Shan ႐–႙ are the ones
            // this market types, but CV import can bring full-width, Arabic-Indic, Thai and others.
            // Written as U& escapes so the statement is ASCII whatever editor touches this file.
            var from = new StringBuilder();
            var to = new StringBuilder();
            for (var c = (char)0x80; c < char.MaxValue; c++)
            {
                var value = CharUnicodeInfo.GetDecimalDigitValue(c);
                if (value < 0) continue;
                from.Append('\\').Append(((int)c).ToString("X4"));
                to.Append((char)('0' + value));
            }

            // The +95 / 0095 reduction the old code skipped for non-ASCII input ("+၉၅ ၉…" was stored as
            // "၉၅၉…"). Not where the stored value already starts with an ASCII 0: the old code had
            // reduced that one already, and reducing "0၀၉၅…" again would delete three digits.
            migrationBuilder.Sql($$"""
                UPDATE "Candidates" AS c
                SET "Phone" = CASE
                        WHEN s.t LIKE '0095%' AND c."Phone" NOT LIKE '0%' THEN '0' || substr(s.t, 5)
                        WHEN s.t LIKE '95%' AND length(s.t) >= 10 THEN '0' || substr(s.t, 3)
                        ELSE s.t
                    END
                FROM (
                    SELECT "Id", translate("Phone", U&'{{from}}', '{{to}}') AS t
                    FROM "Candidates"
                    WHERE "Phone" ~ '[^0-9]'
                ) AS s
                WHERE c."Id" = s."Id" AND s.t <> c."Phone";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the ASCII form is the same number, and which script each row was
            // typed in was not kept. This migration is not reversible — the rollback is the
            // pre-upgrade database dump the upgrade runbook takes (deployment-runbook.md §3).
        }
    }
}
