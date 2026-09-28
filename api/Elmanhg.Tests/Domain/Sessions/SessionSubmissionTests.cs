using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionSubmissionTests
{
    private readonly SessionBuilder _builder = new();

    [Fact]
    public void Submit_AllCorrect_Scores100()
    {
        var session = _builder.Build();
        session.Items.ForEach(item => session.RecordAttempt(item, SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0));

        session.Submit();

        session.ScorePercent.Should().Be(100.00m);
        session.SubmittedAt.Should().NotBeNull();
    }

    [Fact]
    public void Submit_HalfAnswered_CountsUnansweredAsZero()
    {
        var session = _builder.Build(count: 2);
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        session.Submit();

        session.ScorePercent.Should().Be(50.00m);
    }

    [Fact]
    public void Submit_PartialCredit_RoundsToTwoDecimals()
    {
        var session = _builder.Build(count: 3);
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        session.RecordAttempt(session.Items[1], SessionBuilder.AnswerA, SessionBuilder.Grade(0m), 0);
        session.RecordAttempt(session.Items[2], SessionBuilder.AnswerA, SessionBuilder.Grade(0m), 0);

        session.Submit();

        session.ScorePercent.Should().Be(33.33m);
    }

    [Fact]
    public void Submit_AlreadySubmitted_KeepsOriginalSubmission()
    {
        var session = _builder.Build();
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        session.Submit();
        var (submittedAt, scorePercent) = (session.SubmittedAt, session.ScorePercent);

        session.Submit();

        (session.SubmittedAt, session.ScorePercent).Should().Be((submittedAt, scorePercent));
    }

    [Fact]
    public void TotalTimeTakenMilliseconds_SumsAttempts()
    {
        var session = _builder.Build();
        var single = new SessionBuilder().Build();
        session.Items.ForEach(item => session.RecordAttempt(item, SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0));
        single.RecordAttempt(single.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        (session.TotalTimeTakenMilliseconds, single.TotalTimeTakenMilliseconds).Should().Be((0L, 0L));
    }
}
