using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Application.Auth.Shared;

public static class SecurityStampClaim
{
    public const string ClaimType = "security_stamp_hash";

    // A digest, never the stamp: Identity's token providers derive codes from the raw stamp, and a JWT is readable by its holder.
    public static string Fingerprint(string? securityStamp) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? string.Empty)));

    public static bool Matches(string presentedFingerprint, string? securityStamp) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presentedFingerprint), Encoding.UTF8.GetBytes(Fingerprint(securityStamp)));
}
