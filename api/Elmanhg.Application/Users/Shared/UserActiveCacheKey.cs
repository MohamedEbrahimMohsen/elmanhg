using System.Globalization;

namespace Elmanhg.Application.Users.Shared;

public static class UserActiveCacheKey
{
    public static string For(Guid userId) => string.Create(CultureInfo.InvariantCulture, $"user-active:{userId:N}");
}
