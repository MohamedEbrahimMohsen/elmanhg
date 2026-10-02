using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.Shared;

public static class TeacherThreadSlaPolicyLoader
{
    public static async Task<TeacherThreadSlaPolicy> LoadAsync(IRuntimeSettings runtimeSettings, IExamPeriodRepository examPeriodRepository, CancellationToken cancellationToken)
    {
        var values = await runtimeSettings.GetValuesAsync(cancellationToken).ConfigureAwait(false);
        var periods = await examPeriodRepository.GetAllAsync(cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? [];
        var weekendDays = values.Get(SlaCalendarRuntimeSettings.WeekendDays)
            .Select(Enum.Parse<DayOfWeek>)
            .ToList();
        var examPeriods = periods
            .Select(x => x.ToDateRange())
            .ToList();
        var calendar = new SlaCalendar(values.Get(SlaCalendarRuntimeSettings.SkipWeekends), weekendDays, TimeZoneInfo.FindSystemTimeZoneById(values.Get(SlaCalendarRuntimeSettings.TimeZone)), examPeriods);
        return new TeacherThreadSlaPolicy(calendar, TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.ReplySlaHours)), TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.FirstReminderAfterHours)), TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.SecondReminderAfterHours)));
    }
}
