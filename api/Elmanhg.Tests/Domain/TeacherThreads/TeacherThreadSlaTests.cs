using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadSlaTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private static readonly DateTimeOffset SundayMidnightCairo = new(2026, 10, 3, 21, 0, 0, TimeSpan.Zero);
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void DueSlaStages_BeforeFirstReminder_ReturnsNone()
    {
        var thread = new TeacherThreadBuilder().Build();

        Stages(thread, SubmittedAt.AddHours(11).AddMinutes(59)).Should().BeEmpty();
    }

    [Fact]
    public void DueSlaStages_AtFirstReminder_ReturnsFirst()
    {
        var thread = new TeacherThreadBuilder().Build();

        Stages(thread, SubmittedAt.AddHours(12)).Should().Equal(TeacherThreadSlaEventKind.FirstReminder);
    }

    [Fact]
    public void DueSlaStages_AtSecondReminder_ReturnsFirstAndSecond()
    {
        var thread = new TeacherThreadBuilder().Build();

        Stages(thread, SubmittedAt.AddHours(20)).Should().Equal(TeacherThreadSlaEventKind.FirstReminder, TeacherThreadSlaEventKind.SecondReminder);
    }

    [Fact]
    public void DueSlaStages_AtDueTime_ReturnsAllStages()
    {
        var thread = new TeacherThreadBuilder().Build();

        Stages(thread, SubmittedAt.AddHours(24)).Should().Equal(TeacherThreadSlaEventKind.FirstReminder, TeacherThreadSlaEventKind.SecondReminder, TeacherThreadSlaEventKind.Breach);
    }

    [Fact]
    public void DueSlaStages_AnsweredThread_ReturnsNone()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        Stages(thread, SubmittedAt.AddHours(30)).Should().BeEmpty();
    }

    [Fact]
    public void DueSlaStages_VoiceReply_ClosesWindow()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        thread.ReplyWithVoice(_teacherId, "Because F = ma.", "https://files.example/voice.webm", 42, SubmittedAt.AddHours(13));

        (thread.Status, Stages(thread, SubmittedAt.AddHours(30)).Count).Should().Be((TeacherThreadStatus.Answered, 0));
    }

    [Fact]
    public void DueSlaStages_VoiceReplyToFollowUp_ClosesThreadAndWindow()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FollowedUp().Build();

        thread.ReplyWithVoice(_teacherId, "Yes.", "https://files.example/voice.webm", 10, SubmittedAt.AddHours(15));

        (thread.Status, Stages(thread, SubmittedAt.AddHours(40)).Count).Should().Be((TeacherThreadStatus.Closed, 0));
    }

    [Fact]
    public void DueSlaStages_AfterFollowUp_MeasuresFromNewWindow()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FollowedUp().Build();

        Stages(thread, SubmittedAt.AddHours(13)).Should().BeEmpty();
        Stages(thread, SubmittedAt.AddHours(14)).Should().Equal(TeacherThreadSlaEventKind.FirstReminder);
    }

    [Fact]
    public void Submit_CairoWeekendPolicy_StoresCalendarStageTimes()
    {
        var policy = TeacherThreadSlaPolicies.CairoWeekends();

        var thread = new TeacherThreadBuilder().WithSlaPolicy(policy).Build();

        (thread.SlaWindowStartedAt, thread.FirstReminderDueAt, thread.SecondReminderDueAt, thread.SlaDueAt).Should().Be((SubmittedAt, SundayMidnightCairo.AddHours(3), SundayMidnightCairo.AddHours(11), SundayMidnightCairo.AddHours(15)));
        thread.SlaScheduleFingerprint.Should().Be(policy.Fingerprint);
    }

    [Fact]
    public void FollowUp_StartsNewWindowFromFollowUpTime()
    {
        var thread = new TeacherThreadBuilder().WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()).AnsweredBy(_teacherId).FollowedUp().Build();

        (thread.SlaWindowStartedAt, thread.SlaDueAt).Should().Be((SubmittedAt.AddHours(2), SundayMidnightCairo.AddHours(17)));
    }

    [Fact]
    public void DueSlaStages_BeforeFirstReminder_ReturnsEmpty()
    {
        var thread = new TeacherThreadBuilder().Build();

        Stages(thread, thread.FirstReminderDueAt.AddTicks(-10)).Should().BeEmpty();
    }

    [Fact]
    public void DueSlaStages_AtDeadline_ReturnsAllThree()
    {
        var thread = new TeacherThreadBuilder().WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()).Build();

        Stages(thread, thread.SlaDueAt).Should().Equal(TeacherThreadSlaEventKind.FirstReminder, TeacherThreadSlaEventKind.SecondReminder, TeacherThreadSlaEventKind.Breach);
    }

    [Fact]
    public void DueSlaStages_OverWeekend_UsesStoredCalendarTimes()
    {
        var thread = new TeacherThreadBuilder().WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()).Build();

        Stages(thread, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)).Should().BeEmpty();
    }

    [Fact]
    public void DueSlaStages_AnsweredThread_ReturnsEmpty()
    {
        var thread = new TeacherThreadBuilder().WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()).AnsweredBy(_teacherId).Build();

        Stages(thread, thread.SlaDueAt.AddDays(3)).Should().BeEmpty();
    }

    [Fact]
    public void RescheduleSla_NewFingerprint_RecomputesFromWindowStartAndStamps()
    {
        var thread = new TeacherThreadBuilder().Build();
        var policy = TeacherThreadSlaPolicies.CairoWeekends();
        var rescheduledAt = SubmittedAt.AddHours(1).AddTicks(3);

        var rescheduled = thread.RescheduleSla(policy, rescheduledAt);

        rescheduled.Should().BeTrue();
        thread.SlaSchedule().Should().Be(policy.ScheduleFrom(SubmittedAt));
        (thread.SlaWindowStartedAt, thread.UpdationDate).Should().Be((SubmittedAt, SubmittedAt.AddHours(1)));
    }

    [Fact]
    public void RescheduleSla_SameFingerprint_ReturnsFalseAndKeepsTimes()
    {
        var thread = new TeacherThreadBuilder().Build();
        var before = thread.SlaSchedule();
        var updatedAt = thread.UpdationDate;

        var rescheduled = thread.RescheduleSla(TeacherThreadSlaPolicies.WallClock(), SubmittedAt.AddHours(1));

        rescheduled.Should().BeFalse();
        (thread.SlaSchedule(), thread.UpdationDate).Should().Be((before, updatedAt));
    }

    [Fact]
    public void RescheduleSla_AnsweredThread_ReturnsFalse()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();
        var before = thread.SlaSchedule();

        var rescheduled = thread.RescheduleSla(TeacherThreadSlaPolicies.CairoWeekends(), SubmittedAt.AddHours(2));

        rescheduled.Should().BeFalse();
        thread.SlaSchedule().Should().Be(before);
    }

    [Fact]
    public void SlaSchedule_ReturnsStoredTimes()
    {
        var thread = new TeacherThreadBuilder().WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()).Build();

        var schedule = thread.SlaSchedule();

        schedule.Should().Be(new TeacherThreadSlaSchedule(thread.SlaWindowStartedAt, thread.FirstReminderDueAt, thread.SecondReminderDueAt, thread.SlaDueAt, thread.SlaScheduleFingerprint));
        (schedule.DueAt(TeacherThreadSlaEventKind.FirstReminder), schedule.DueAt(TeacherThreadSlaEventKind.SecondReminder), schedule.DueAt(TeacherThreadSlaEventKind.Breach)).Should().Be((thread.FirstReminderDueAt, thread.SecondReminderDueAt, thread.SlaDueAt));
    }

    private static List<TeacherThreadSlaEventKind> Stages(TeacherThread thread, DateTimeOffset now) => thread.DueSlaStages(now);
}
