using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace RecruitOps.Infrastructure.Services;

/// <summary>The key the offer link's codes are hashed under (ADR-0030 §5a). Derived from the install's
/// JWT signing key — already a required, per-install secret kept out of the database — under a label of
/// its own, so the two keys are never the same bytes. Rotating the JWT key retires codes in flight, which
/// live ten minutes.</summary>
public sealed class OfferPortalSecret
{
    public OfferPortalSecret(IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
            throw new InvalidOperationException("Jwt:Key is not configured, so offer codes cannot be hashed.");
        Key = SHA256.HashData(Encoding.UTF8.GetBytes("RecruitOps offer-portal code v1|" + jwtKey));
    }

    public byte[] Key { get; }
}
