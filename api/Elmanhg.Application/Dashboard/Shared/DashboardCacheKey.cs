using System.Globalization;

namespace Elmanhg.Application.Dashboard.Shared;

public static class DashboardCacheKey
{
    public const string Profile = "dashboard";

    public static string For(string card, DateOnly? from, DateOnly? to, Guid? subjectId) => string.Create(CultureInfo.InvariantCulture, $"dashboard:{card}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{subjectId:N}");
}
