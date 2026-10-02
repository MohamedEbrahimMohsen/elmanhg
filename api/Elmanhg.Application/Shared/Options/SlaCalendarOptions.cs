using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class SlaCalendarOptions
{
    public const string SectionName = "SlaCalendar";

    public bool SkipWeekends { get; set; } = true;

    [Required]
    public string WeekendDays { get; set; } = "Friday,Saturday";

    [Required]
    public string TimeZone { get; set; } = "Africa/Cairo";

    [Required]
    public string AllowedTimeZones { get; set; } = "Africa/Cairo,Asia/Riyadh,Asia/Dubai,Asia/Kuwait,UTC";

    [Range(1, 500)]
    public int ExamPeriodNameMaxLength { get; set; } = 100;

    [Range(1, 366)]
    public int ExamPeriodMaxDays { get; set; } = 120;

    public List<string> WeekendDayNames() => Split(WeekendDays);

    public List<string> AllowedTimeZoneIds() => Split(AllowedTimeZones);

    private static List<string> Split(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
