using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    [Required]
    public string TimeZone { get; set; } = "Africa/Cairo";

    [Range(0, 3600)]
    public int CacheSeconds { get; set; } = 60;

    [Range(1, 3650)]
    public int DefaultRangeDays { get; set; } = 30;

    [Range(1, 3650)]
    public int MaxRangeDays { get; set; } = 366;

    [Range(1, 31)]
    public int RecentWeekDays { get; set; } = 7;

    [Range(1, 366)]
    public int RecentMonthDays { get; set; } = 30;
}
