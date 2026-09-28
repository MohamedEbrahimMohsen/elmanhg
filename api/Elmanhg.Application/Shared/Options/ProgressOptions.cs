using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ProgressOptions
{
    public const string SectionName = "Progress";

    [Required]
    public string StreakTimeZone { get; set; } = "Africa/Cairo";

    [Range(1, 3650)]
    public int StreakMaxDays { get; set; } = 365;

    [Range(1, 20)]
    public int WeakLessonCount { get; set; } = 4;

    [Range(1, 20)]
    public int WeakObjectiveCount { get; set; } = 3;

    [Range(1, 100)]
    public int HistoryMaxPageSize { get; set; } = 50;
}
