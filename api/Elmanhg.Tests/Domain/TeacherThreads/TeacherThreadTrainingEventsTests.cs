using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadTrainingEventsTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void Reply_FirstReply_RaisesNoClosedEvent()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        thread.Reply(_teacherId, "Because F = ma.", SubmittedAt.AddHours(1));

        thread.GetDomainEvents().OfType<TeacherThreadClosed>().Should().BeEmpty();
    }

    [Fact]
    public void Reply_FinalReply_RaisesTeacherThreadClosed()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FollowedUp().Build();
        thread.ClearDomainEvents();

        thread.Reply(_teacherId, "Newtons.", SubmittedAt.AddHours(3));

        thread.GetDomainEvents().OfType<TeacherThreadClosed>().Should().ContainSingle().Which.Thread.Should().BeSameAs(thread);
    }

    [Fact]
    public void Rate_AnsweredThread_RaisesTeacherThreadClosed()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        thread.Rate(5, SubmittedAt.AddHours(4));

        thread.GetDomainEvents().OfType<TeacherThreadClosed>().Should().ContainSingle().Which.Thread.Should().BeSameAs(thread);
        thread.GetDomainEvents().OfType<TeacherThreadRatedAfterClose>().Should().BeEmpty();
    }

    [Fact]
    public void Rate_ClosedThread_RaisesRatedAfterCloseWithRatingTime()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FinalReplied().Build();
        thread.ClearDomainEvents();
        var ratedAt = SubmittedAt.AddHours(9);

        thread.Rate(4, ratedAt.AddTicks(3));

        thread.GetDomainEvents().OfType<TeacherThreadRatedAfterClose>().Should().ContainSingle().Which.RatedAt.Should().Be(ratedAt);
        thread.GetDomainEvents().OfType<TeacherThreadClosed>().Should().BeEmpty();
    }
}
