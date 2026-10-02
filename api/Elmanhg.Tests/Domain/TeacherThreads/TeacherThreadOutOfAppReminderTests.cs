using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadOutOfAppReminderTests
{
    [Fact]
    public void Record_ReminderStage_SetsFieldsAndTruncatesToMicroseconds()
    {
        var threadId = Guid.NewGuid();
        var slaDueAt = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(24);
        var occurredAt = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(20);

        var reminder = TeacherThreadOutOfAppReminder.Record(threadId, TeacherThreadSlaEventKind.SecondReminder, slaDueAt, occurredAt.AddTicks(9));

        (reminder.ThreadId, reminder.Stage, reminder.SlaDueAt, reminder.OccurredAt).Should().Be((threadId, TeacherThreadSlaEventKind.SecondReminder, slaDueAt, occurredAt));
        reminder.Id.Should().NotBeEmpty();
        reminder.OccurredAt.Ticks.Should().Be(occurredAt.Ticks);
    }

    [Fact]
    public void Record_BreachStage_ThrowsArgumentOutOfRange()
    {
        var act = () => TeacherThreadOutOfAppReminder.Record(Guid.NewGuid(), TeacherThreadSlaEventKind.Breach, TeacherThreadBuilder.DefaultSubmittedAt, TeacherThreadBuilder.DefaultSubmittedAt);

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("stage");
    }
}
