using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionReplayTests
{
    private const string MathAnswer = """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""";
    private static readonly QuestionGrade FullGrade = new(1m, 1m, GradeOutcome.Correct, GradeFeedback.MathStepTally(2, 2));
    private static readonly DateTimeOffset AnsweredAt = new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);
    private readonly SessionBuilder _builder = new();

    [Fact]
    public void IsReplay_Unanswered_ReturnsFalse()
    {
        var session = _builder.Build();

        session.IsReplay(session.Items[0], SessionBuilder.AnswerB).Should().BeFalse();
    }

    [Fact]
    public void IsReplay_SameAnswerAsAttempt_ReturnsTrue()
    {
        var session = _builder.Build();
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        session.IsReplay(session.Items[0], SessionBuilder.AnswerB).Should().BeTrue();
    }

    [Fact]
    public void IsReplay_SameAnswerAsPendingAnswer_ReturnsTrue()
    {
        var session = _builder.Build();
        session.SubmitForAiGrading(session.Items[1], MathAnswer, 0);

        session.IsReplay(session.Items[1], MathAnswer).Should().BeTrue();
    }

    [Fact]
    public void IsReplay_DifferentAnswer_ThrowsSessionQuestionAlreadyAnswered()
    {
        var session = _builder.Build();
        session.SubmitForAiGrading(session.Items[1], MathAnswer, 0);

        var act = () => session.IsReplay(session.Items[1], """{"steps":[],"finalAnswer":"x = 3"}""");

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionAlreadyAnswered);
    }

    [Fact]
    public void IsReplay_SubmittedSessionUnanswered_ThrowsSessionAlreadySubmitted()
    {
        var session = _builder.Build();
        session.Submit();

        var act = () => session.IsReplay(session.Items[0], SessionBuilder.AnswerB);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionAlreadySubmitted);
    }

    [Fact]
    public void SubmitForAiGrading_New_SavesAnswerOnItem()
    {
        var session = _builder.Build();
        var item = session.Items[1];

        var submission = session.SubmitForAiGrading(item, MathAnswer, 0);

        (submission.IsNew, item.SavedAnswer, item.AnswerSavedAt).Should().Be((true, MathAnswer, (DateTimeOffset?)submission.SubmittedAt));
        session.Attempts.Should().BeEmpty();
        session.FindPendingEssayAnswer(item).Should().Be(MathAnswer);
    }

    [Fact]
    public void RecordAiGradedAttempt_Finished_RecomputesScorePercentAndRaisesEvent()
    {
        var session = _builder.Build();
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(0m), 0);
        session.SubmitForAiGrading(session.Items[1], MathAnswer, 0);
        session.Submit();
        session.ClearDomainEvents();

        var attempt = session.RecordAiGradedAttempt(session.Items[1], MathAnswer, FullGrade, AttemptGrader.AI, 0, AnsweredAt, AnsweredAt.AddMinutes(1));

        (attempt!.GradedBy, attempt.CreatedAt, session.ScorePercent).Should().Be((AttemptGrader.AI, AnsweredAt, (decimal?)50m));
        session.GetDomainEvents().OfType<AttemptsRecorded>().Should().ContainSingle().Which.Attempts.Should().ContainSingle().Which.Should().BeSameAs(attempt);
    }
}
