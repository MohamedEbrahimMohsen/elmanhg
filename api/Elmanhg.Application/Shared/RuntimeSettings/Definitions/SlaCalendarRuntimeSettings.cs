using Core.DDD.Models;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class SlaCalendarRuntimeSettings(IOptions<SlaCalendarOptions> slaCalendarOptions) : IRuntimeSettingDefinitions
{
    public static readonly RuntimeSettingKey<bool> SkipWeekends = new("slaCalendar.skipWeekends");
    public static readonly RuntimeSettingKey<List<string>> WeekendDays = new("slaCalendar.weekendDays");
    public static readonly RuntimeSettingKey<string> TimeZone = new("slaCalendar.timeZone");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForBoolean(SkipWeekends, RuntimeSettingGroup.SlaCalendar, slaCalendarOptions.Value.SkipWeekends, new LocalizedText("استبعاد أيام العطلة", "Skip weekends"), new LocalizedText("عند التفعيل لا تحتسب أيام العطلة خارج فترات الامتحانات من مهلة الرد والتذكيرات.", "When on, weekend days outside exam periods do not count toward the reply time and the reminders.")),
        RuntimeSettingDefinition.ForChoiceList(WeekendDays, RuntimeSettingGroup.SlaCalendar, slaCalendarOptions.Value.WeekendDayNames(), Enum.GetNames<DayOfWeek>(), new LocalizedText("أيام العطلة", "Weekend days"), new LocalizedText("الأيام التي لا تحتسب عند استبعاد العطلة. يجب أن يحتسب يوم واحد على الأقل.", "Days that do not count when weekends are skipped. At least one day of the week must count.")),
        RuntimeSettingDefinition.ForChoice(TimeZone, RuntimeSettingGroup.SlaCalendar, slaCalendarOptions.Value.TimeZone, slaCalendarOptions.Value.AllowedTimeZoneIds(), new LocalizedText("المنطقة الزمنية لحدود الأيام", "Time zone for day boundaries"), new LocalizedText("تحدد متى يبدأ اليوم وينتهي عند احتساب العطلات وفترات الامتحانات.", "Decides when a day starts and ends for weekends and exam periods.")),
    ];

    public IReadOnlyList<RuntimeSettingConstraint> Constraints =>
    [
        new(ErrorCodes.SlaCalendarWeekendDaysInvalid, values => !values.Get(SkipWeekends) || values.Get(WeekendDays).Count < Enum.GetNames<DayOfWeek>().Length),
    ];
}
