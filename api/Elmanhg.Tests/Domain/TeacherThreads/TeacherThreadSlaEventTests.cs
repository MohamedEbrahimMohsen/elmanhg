using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadSlaEventTests
{
    [Fact]
    public void Record_SetsFieldsAndTruncatesOccurredAtToMicroseconds()
    {
        var threadId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var slaDueAt = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(24);
        var occurredAt = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(12);

        var slaEvent = TeacherThreadSlaEvent.Record(threadId, TeacherThreadSlaEventKind.FirstReminder, slaDueAt, teacherId, occurredAt.AddTicks(9));

        (slaEvent.ThreadId, slaEvent.Kind, slaEvent.SlaDueAt, slaEvent.TeacherId, slaEvent.OccurredAt).Should().Be((threadId, TeacherThreadSlaEventKind.FirstReminder, slaDueAt, (Guid?)teacherId, occurredAt));
        slaEvent.Id.Should().NotBeEmpty();
        slaEvent.OccurredAt.Ticks.Should().Be(occurredAt.Ticks);
    }
}
