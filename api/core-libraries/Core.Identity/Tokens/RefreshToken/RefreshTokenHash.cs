using System.Security.Cryptography;
using System.Text;

namespace Core.Identity.Tokens.RefreshToken;

public static class RefreshTokenHash
{
    public static string Compute(string refreshToken) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
