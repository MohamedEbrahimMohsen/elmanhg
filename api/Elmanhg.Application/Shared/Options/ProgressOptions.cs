using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ProgressOptions
{
    public const string SectionName = "Progress";

    [Required]
    public string StreakTimeZone { get; set; } = "Africa/Cairo";

    [Range(1, 3650)]
    public int StreakMaxDays { get; set; } = 365;
}
