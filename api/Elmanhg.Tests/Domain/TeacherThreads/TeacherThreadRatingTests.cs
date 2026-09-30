using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TeacherThreads;

public sealed class TeacherThreadRatingTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void Rate_AnsweredThread_StoresRatingAndCloses()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();
        var ratedAt = SubmittedAt.AddHours(5);

        thread.Rate(4, ratedAt.AddTicks(3));

        (thread.Rating, thread.Status, thread.ClosedAt, thread.UpdationDate).Should().Be(((int?)4, TeacherThreadStatus.Closed, (DateTimeOffset?)ratedAt, ratedAt));
    }

    [Fact]
    public void Rate_ClosedAfterFinalReply_StoresRatingAndKeepsClosedAt()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).FinalReplied().Build();
        var closedAt = thread.ClosedAt;

        thread.Rate(2, SubmittedAt.AddHours(9));

        (thread.Rating, thread.Status, thread.ClosedAt).Should().Be(((int?)2, TeacherThreadStatus.Closed, closedAt));
        closedAt.Should().Be(SubmittedAt.AddHours(3));
    }

    [Fact]
    public void Rate_OpenThread_ThrowsNotAnswered()
    {
        var thread = new TeacherThreadBuilder().ClaimedBy(_teacherId).Build();

        var act = () => thread.Rate(5, SubmittedAt.AddHours(1));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotAnswered);
        thread.Rating.Should().BeNull();
    }

    [Fact]
    public void Rate_AlreadyRated_ThrowsAlreadyRated()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Rated(3).Build();

        var act = () => thread.Rate(5, SubmittedAt.AddHours(9));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadAlreadyRated);
        thread.Rating.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rate_OutOfRange_Throws(int rating)
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        var act = () => thread.Rate(rating, SubmittedAt.AddHours(5));

        act.Should().Throw<ArgumentOutOfRangeException>();
        (thread.Rating, thread.Status).Should().Be(((int?)null, TeacherThreadStatus.Answered));
    }

    [Theory]
    [InlineData("Open", false, false)]
    [InlineData("Answered", true, true)]
    [InlineData("FinalReplied", false, true)]
    [InlineData("Rated", false, false)]
    public void CanFollowUpAndCanBeRated_ByState_MatchRules(string state, bool canFollowUp, bool canBeRated)
    {
        var builder = state switch
        {
            "Open" => new TeacherThreadBuilder().ClaimedBy(_teacherId),
            "Answered" => new TeacherThreadBuilder().AnsweredBy(_teacherId),
            "FinalReplied" => new TeacherThreadBuilder().AnsweredBy(_teacherId).FinalReplied(),
            _ => new TeacherThreadBuilder().AnsweredBy(_teacherId).Rated(5),
        };
        var thread = builder.Build();

        (thread.CanFollowUp(), thread.CanBeRated()).Should().Be((canFollowUp, canBeRated));
    }
}
