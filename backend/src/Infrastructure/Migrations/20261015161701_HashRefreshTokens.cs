using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Migrations
{
    /// <summary>ADR-0028: refresh tokens are stored as SHA-256 hashes, never as tokens.
    /// <para>Up hashes existing rows in place with Postgres's built-in <c>sha256()</c> (PostgreSQL 11+) —
    /// the same lowercase hex <c>RefreshTokenHash.Of</c> computes — so signed-in users stay signed in.
    /// A session started before this migration counts from the token's creation.</para>
    /// <para>Down cannot recover a token from its hash: it revokes every refresh token, and everyone
    /// signs in again.</para></summary>
    public partial class HashRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SessionStartedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" SET
                    "TokenHash" = encode(sha256(convert_to("Token", 'UTF8')), 'hex'),
                    "ReplacedByTokenHash" = CASE WHEN "ReplacedByToken" IS NULL THEN NULL
                                                 ELSE encode(sha256(convert_to("ReplacedByToken", 'UTF8')), 'hex') END,
                    "SessionStartedAt" = "CreatedAt";

                -- A deactivated user's tokens were only refused while they stayed inactive; end
                -- them now, so reactivation cannot bring them back (ADR-0028 §2).
                UPDATE "RefreshTokens" SET "IsRevoked" = TRUE, "RevokedAt" = now()
                WHERE NOT "IsRevoked" AND "UserId" IN (SELECT "Id" FROM "Users" WHERE NOT "IsActive");
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SessionStartedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByToken",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "RefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByToken",
                table: "RefreshTokens",
                type: "text",
                nullable: true);

            // A hash cannot be turned back into a token: every session ends. The placeholder keeps
            // the column unique and non-null, and can never match a token the API issues.
            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" SET
                    "Token" = 'revoked-by-rollback:' || "TokenHash",
                    "IsRevoked" = TRUE,
                    "RevokedAt" = COALESCE("RevokedAt", now());
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionStartedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "RefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);
        }
    }
}
