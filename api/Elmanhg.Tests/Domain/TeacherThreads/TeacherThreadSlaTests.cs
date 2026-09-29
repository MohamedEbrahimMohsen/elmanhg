using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadSlaTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private static readonly TimeSpan ReplySla = TimeSpan.FromHours(24);
    private static readonly TimeSpan FirstReminderAfter = TimeSpan.FromHours(12);
    private static readonly TimeSpan SecondReminderAfter = TimeSpan.FromHours(20);
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

    private static List<TeacherThreadSlaEventKind> Stages(TeacherThread thread, DateTimeOffset now) => thread.DueSlaStages(now, ReplySla, FirstReminderAfter, SecondReminderAfter);
}
