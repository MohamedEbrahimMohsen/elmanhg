using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionAnsweringTests
{
    private const int ElapsedCeilingMilliseconds = 60_000;
    private readonly SessionBuilder _builder = new();

    [Fact]
    public void RecordAttempt_FirstAnswer_AddsGradedAttemptAtServedVersion()
    {
        var session = _builder.Build();
        var item = session.Items[0];

        session.RecordAttempt(item, SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.SessionId, attempt.StudentId, attempt.QuestionId, attempt.QuestionVersion).Should().Be((session.Id, _builder.StudentId, item.QuestionId, item.QuestionVersion));
        attempt.Answer.Should().Be(SessionBuilder.AnswerB);
        (attempt.Score, attempt.NormalisedScore).Should().Be((1m, 1m));
        attempt.GradedBy.Should().Be(AttemptGrader.Auto);
        attempt.CreatedAt.Should().BeOnOrAfter(session.StartedAt).And.BeCloseTo(session.StartedAt, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RecordAttempt_WithFeedback_StoresGradeJson()
    {
        var session = _builder.Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(0.5m, GradeFeedback.ChoiceTally(1, 1, 2)), 0);

        attempt.ReadFeedback().Should().Be(GradeFeedback.ChoiceTally(1, 1, 2));
    }

    [Fact]
    public void RecordAttempt_ReportedTimeWithinElapsed_KeepsReportedTime()
    {
        var session = _builder.Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        attempt.TimeTakenMilliseconds.Should().Be(0);
    }

    [Fact]
    public void RecordAttempt_ReportedTimeAboveElapsed_ClampsToElapsed()
    {
        var session = _builder.Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), int.MaxValue);

        attempt.TimeTakenMilliseconds.Should().BeInRange(0, ElapsedCeilingMilliseconds);
    }

    [Fact]
    public void RecordAttempt_NoReportedTime_UsesServerElapsed()
    {
        var session = _builder.Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), null);

        attempt.TimeTakenMilliseconds.Should().BeInRange(0, ElapsedCeilingMilliseconds);
    }

    [Fact]
    public void RecordAttempt_Answer_MovesLastActivityToAttemptTime()
    {
        var session = _builder.Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        session.LastActivityAt.Should().Be(attempt.CreatedAt);
    }

    [Fact]
    public void RecordAttempt_SameAnswerTwice_ReturnsExistingAttempt()
    {
        var session = _builder.Build();
        var first = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        var second = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        second.Should().BeSameAs(first);
        session.Attempts.Should().HaveCount(1);
    }

    [Fact]
    public void RecordAttempt_EquivalentJsonDifferentFormatting_ReturnsExistingAttempt()
    {
        var session = _builder.Build();
        var first = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        var second = session.RecordAttempt(session.Items[0], """{ "optionId" : "b" }""", SessionBuilder.Grade(1m), 0);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void RecordAttempt_DifferentAnswerForAnsweredItem_ThrowsSessionQuestionAlreadyAnswered()
    {
        var session = _builder.Build();
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        var act = () => session.RecordAttempt(session.Items[0], SessionBuilder.AnswerA, SessionBuilder.Grade(0m), 0);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionAlreadyAnswered);
        session.Attempts.Should().HaveCount(1);
    }

    [Fact]
    public void RecordAttempt_SubmittedSession_ThrowsSessionAlreadySubmitted()
    {
        var session = _builder.Build();
        session.Submit();

        var act = () => session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionAlreadySubmitted);
        session.Attempts.Should().BeEmpty();
    }

    [Fact]
    public void RecordAttempt_SameAnswerAfterSubmit_ReturnsExistingAttempt()
    {
        var session = _builder.Build();
        var first = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        session.Submit();

        var second = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetItem_QuestionNotServed_ReturnsNull()
    {
        var session = _builder.Build();

        session.GetItem(Guid.NewGuid()).Should().BeNull();
    }
}
