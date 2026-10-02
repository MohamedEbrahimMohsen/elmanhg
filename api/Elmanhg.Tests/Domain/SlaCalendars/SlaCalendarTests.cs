using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.SlaCalendars;

public sealed class SlaCalendarTests
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);
    private static readonly DateTimeOffset ThursdayNoon = At(2026, 10, 1, 12);

    [Fact]
    public void AddCountedTime_SkipOff_AddsRealHours()
    {
        Calendar(skipWeekends: false).AddCountedTime(ThursdayNoon, Day).Should().Be(At(2026, 10, 2, 12));
    }

    [Fact]
    public void AddCountedTime_ThursdayAfternoonOverWeekend_ResumesSunday()
    {
        Calendar().AddCountedTime(ThursdayNoon, Day).Should().Be(At(2026, 10, 4, 12));
    }

    [Fact]
    public void AddCountedTime_StartsOnFriday_ClockStartsSundayMidnight()
    {
        Calendar().AddCountedTime(At(2026, 10, 2, 9), TimeSpan.FromHours(12)).Should().Be(At(2026, 10, 4, 9));
        Calendar().AddCountedTime(At(2026, 10, 1, 22), TimeSpan.FromHours(12)).Should().Be(At(2026, 10, 4, 9));
    }

    [Fact]
    public void AddCountedTime_EndsExactlyAtMidnightBeforeWeekend_ReturnsThatMidnight()
    {
        Calendar().AddCountedTime(At(2026, 10, 1, 20), TimeSpan.FromHours(1)).Should().Be(At(2026, 10, 1, 21));
    }

    [Fact]
    public void AddCountedTime_CairoDayBoundaryNotUtc_UsesCalendarTimeZone()
    {
        Calendar().AddCountedTime(At(2026, 10, 1, 20, 30), TimeSpan.FromHours(1)).Should().Be(At(2026, 10, 3, 21, 30));
    }

    [Fact]
    public void AddCountedTime_SameInstantInUtcCalendar_UsesUtcDays()
    {
        var calendar = new SlaCalendar(true, [DayOfWeek.Friday, DayOfWeek.Saturday], TimeZoneInfo.Utc, []);

        calendar.AddCountedTime(At(2026, 10, 1, 20, 30), TimeSpan.FromHours(1)).Should().Be(At(2026, 10, 1, 21, 30));
    }

    [Fact]
    public void AddCountedTime_ExamPeriodCoversWeekend_CountsEveryDay()
    {
        Calendar(examPeriods: [Range(2026, 10, 2, 2026, 10, 3)]).AddCountedTime(ThursdayNoon, Day).Should().Be(At(2026, 10, 2, 12));
    }

    [Fact]
    public void AddCountedTime_ExamPeriodOnFridayOnly_SkipsSaturday()
    {
        Calendar(examPeriods: [Range(2026, 10, 2, 2026, 10, 2)]).AddCountedTime(ThursdayNoon, TimeSpan.FromHours(40)).Should().Be(At(2026, 10, 4, 4));
    }

    [Fact]
    public void AddCountedTime_ExamPeriodStartsSaturday_CountsSaturday()
    {
        Calendar(examPeriods: [Range(2026, 10, 3, 2026, 10, 10)]).AddCountedTime(ThursdayNoon, Day).Should().Be(At(2026, 10, 3, 12));
    }

    [Fact]
    public void AddCountedTime_SpringForwardWeekend_CountsRealHours()
    {
        Calendar().AddCountedTime(At(2026, 4, 23, 18), Day).Should().Be(At(2026, 4, 26, 17));
    }

    [Fact]
    public void AddCountedTime_FallBackThursday_CountsTwentyFiveHourDay()
    {
        Calendar().AddCountedTime(At(2026, 10, 29, 9), Day).Should().Be(At(2026, 11, 1, 9));
    }

    [Fact]
    public void AddCountedTime_SkipOffAcrossDst_AddsExactlyTwentyFourHours()
    {
        Calendar(skipWeekends: false).AddCountedTime(At(2026, 10, 29, 9), Day).Should().Be(At(2026, 10, 30, 9));
    }

    [Fact]
    public void AddCountedTime_EmptyWeekend_AddsRealHours()
    {
        new SlaCalendar(true, [], TeacherThreadSlaPolicies.Cairo, []).AddCountedTime(ThursdayNoon, Day).Should().Be(At(2026, 10, 2, 12));
    }

    [Fact]
    public void AddCountedTime_NegativeDuration_Throws()
    {
        var act = () => Calendar().AddCountedTime(ThursdayNoon, TimeSpan.FromHours(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddCountedTime_NoCountedDay_ThrowsInvalidOperation()
    {
        var calendar = new SlaCalendar(true, Enum.GetValues<DayOfWeek>(), TeacherThreadSlaPolicies.Cairo, []);

        var act = () => calendar.AddCountedTime(ThursdayNoon, Day);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StartOfDay_SpringForwardFriday_ReturnsTransitionInstant()
    {
        Calendar().StartOfDay(new DateOnly(2026, 4, 24)).Should().Be(At(2026, 4, 23, 22));
    }

    [Fact]
    public void StartOfDay_AfterFallBack_ReturnsStandardMidnight()
    {
        Calendar().StartOfDay(new DateOnly(2026, 10, 30)).Should().Be(At(2026, 10, 29, 22));
    }

    [Fact]
    public void AddCountedTime_ResultIsUtc()
    {
        var result = Calendar().AddCountedTime(new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.FromHours(3)), Day);

        (result.Offset, result).Should().Be((TimeSpan.Zero, At(2026, 10, 4, 12)));
    }

    private static SlaCalendar Calendar(bool skipWeekends = true, SlaDateRange[]? examPeriods = null) => new(skipWeekends, [DayOfWeek.Friday, DayOfWeek.Saturday], TeacherThreadSlaPolicies.Cairo, examPeriods ?? []);

    private static SlaDateRange Range(int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay) => new(new DateOnly(startYear, startMonth, startDay), new DateOnly(endYear, endMonth, endDay));

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
