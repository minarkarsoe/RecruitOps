using System.Security.Cryptography;
using System.Text;

namespace RecruitOps.Infrastructure.Services;

/// <summary>How a refresh token is stored (ADR-0028): its SHA-256, lowercase hex — the same value
/// the migration computed for existing rows with Postgres's <c>sha256()</c>.
/// <para>No salt and no slow hash on purpose: the token is 512 random bits, so there is nothing to
/// guess. The hash only has to make a copy of the table useless as a set of sessions.</para></summary>
public static class RefreshTokenHash
{
    public static string Of(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
