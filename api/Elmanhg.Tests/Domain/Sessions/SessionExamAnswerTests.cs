using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionExamAnswerTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly ExamSessionBuilder _builder = new();

    [Fact]
    public void SaveExamAnswer_OpenExam_StoresAnswerAndTouches()
    {
        var session = _builder.Build();
        var at = ExamSessionBuilder.Now.AddMinutes(5);

        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, at);

        session.Items[0].SavedAnswer.Should().Be(SessionBuilder.AnswerB);
        session.Items[0].AnswerSavedAt.Should().Be(at);
        session.LastActivityAt.Should().Be(at);
        session.UpdationDate.Should().Be(at);
        session.Attempts.Should().BeEmpty();
    }

    [Fact]
    public void SaveExamAnswer_SecondAnswer_Overwrites()
    {
        var session = _builder.Build();
        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));

        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerA, Grace, ExamSessionBuilder.Now.AddMinutes(2));

        session.Items[0].SavedAnswer.Should().Be(SessionBuilder.AnswerA);
        session.Items[0].AnswerSavedAt.Should().Be(ExamSessionBuilder.Now.AddMinutes(2));
    }

    [Fact]
    public void SaveExamAnswer_WithinGrace_Saves()
    {
        var session = _builder.Build();

        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, session.Deadline.GetValueOrDefault().AddSeconds(10));

        session.Items[0].SavedAnswer.Should().Be(SessionBuilder.AnswerB);
    }

    [Fact]
    public void SaveExamAnswer_PastDeadline_ThrowsExamTimeExpired()
    {
        var session = _builder.Build();

        var act = () => session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, session.Deadline.GetValueOrDefault().AddSeconds(31));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ExamTimeExpired);
        session.Items[0].SavedAnswer.Should().BeNull();
    }

    [Fact]
    public void SaveExamAnswer_Submitted_ThrowsAlreadySubmitted()
    {
        var session = _builder.Build();
        session.SubmitExam(new Dictionary<Guid, QuestionGrade>(), ExamSessionBuilder.Now.AddMinutes(1));

        var act = () => session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(2));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionAlreadySubmitted);
    }

    [Fact]
    public void SaveExamAnswer_QuizSession_ThrowsInvalidOperation()
    {
        var quiz = new SessionBuilder().Build();

        var act = () => quiz.SaveExamAnswer(quiz.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SaveExamAnswer_ForeignItem_ThrowsInvalidOperation()
    {
        var session = _builder.Build();
        var other = _builder.Build();

        var act = () => session.SaveExamAnswer(other.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now);

        act.Should().Throw<InvalidOperationException>();
        other.Items[0].SavedAnswer.Should().BeNull();
    }

    [Fact]
    public void RecordAttempt_ExamSession_ThrowsInvalidOperation()
    {
        var session = _builder.Build();

        var act = () => session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        act.Should().Throw<InvalidOperationException>();
        session.Attempts.Should().BeEmpty();
    }
}
