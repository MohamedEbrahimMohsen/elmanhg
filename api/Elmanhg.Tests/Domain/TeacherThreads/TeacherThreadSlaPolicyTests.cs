using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadSlaPolicyTests
{
    private static readonly DateTimeOffset ThursdayNoon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly SlaDateRange FinalExams = new(new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15));
    private static readonly SlaDateRange SecondRound = new(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10));

    [Fact]
    public void ScheduleFrom_CairoWeekends_ComputesAllStages()
    {
        var policy = TeacherThreadSlaPolicies.CairoWeekends();

        var schedule = policy.ScheduleFrom(ThursdayNoon);

        schedule.Should().Be(new TeacherThreadSlaSchedule(ThursdayNoon, new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), policy.Fingerprint));
    }

    [Fact]
    public void DueAt_UndefinedKind_Throws()
    {
        var schedule = TeacherThreadSlaPolicies.CairoWeekends().ScheduleFrom(ThursdayNoon);

        var act = () => schedule.DueAt((TeacherThreadSlaEventKind)99);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kind");
    }

    [Fact]
    public void Fingerprint_SameInputs_IsEqualAndSixtyFourHex()
    {
        var fingerprint = TeacherThreadSlaPolicies.CairoWeekends(FinalExams).Fingerprint;

        fingerprint.Should().Be(TeacherThreadSlaPolicies.CairoWeekends(FinalExams).Fingerprint);
        fingerprint.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Fingerprint_WeekendOrderAndPeriodOrder_DoNotMatter()
    {
        var first = Policy([DayOfWeek.Friday, DayOfWeek.Saturday], [FinalExams, SecondRound]);

        var second = Policy([DayOfWeek.Saturday, DayOfWeek.Friday], [SecondRound, FinalExams]);

        second.Fingerprint.Should().Be(first.Fingerprint);
    }

    [Fact]
    public void Fingerprint_ExamPeriodAdded_Changes()
    {
        TeacherThreadSlaPolicies.CairoWeekends(FinalExams).Fingerprint.Should().NotBe(TeacherThreadSlaPolicies.CairoWeekends().Fingerprint);
    }

    [Fact]
    public void Fingerprint_ReplyHoursChanged_Changes()
    {
        TeacherThreadSlaPolicies.WallClock(replySlaHours: 48).Fingerprint.Should().NotBe(TeacherThreadSlaPolicies.WallClock().Fingerprint);
    }

    [Fact]
    public void Fingerprint_TimeZoneChanged_Changes()
    {
        var cairo = Policy([DayOfWeek.Friday, DayOfWeek.Saturday], []);

        var utc = new TeacherThreadSlaPolicy(new SlaCalendar(true, [DayOfWeek.Friday, DayOfWeek.Saturday], TimeZoneInfo.Utc, []), cairo.ReplySla, cairo.FirstReminderAfter, cairo.SecondReminderAfter);

        utc.Fingerprint.Should().NotBe(cairo.Fingerprint);
    }

    private static TeacherThreadSlaPolicy Policy(DayOfWeek[] weekendDays, SlaDateRange[] examPeriods) => new(new SlaCalendar(true, weekendDays, TeacherThreadSlaPolicies.Cairo, examPeriods), TimeSpan.FromHours(24), TimeSpan.FromHours(12), TimeSpan.FromHours(20));
}
