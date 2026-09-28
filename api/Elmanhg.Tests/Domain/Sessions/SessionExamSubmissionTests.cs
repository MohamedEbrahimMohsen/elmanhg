using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionExamSubmissionTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset SubmittedAt = ExamSessionBuilder.Now.AddMinutes(10);
    private readonly ExamSessionBuilder _builder = new();

    [Fact]
    public void SubmitExam_SavedAnswers_CreatesAttemptsOnlyForAnsweredItems()
    {
        var session = SessionWithFirstAnswerSaved();

        var attempts = session.SubmitExam(Grades(session, 1m), SubmittedAt);

        var attempt = attempts.Should().ContainSingle().Subject;
        attempt.QuestionId.Should().Be(session.Items[0].QuestionId);
        attempt.Answer.Should().Be(SessionBuilder.AnswerB);
        attempt.QuestionVersion.Should().Be(session.Items[0].QuestionVersion);
        attempt.TimeTakenMilliseconds.Should().Be(0);
        attempt.CreatedAt.Should().Be(SubmittedAt);
        session.Attempts.Should().Equal(attempts);
    }

    [Fact]
    public void SubmitExam_ComputesScorePercentOverAllItems()
    {
        var session = SessionWithFirstAnswerSaved();

        session.SubmitExam(Grades(session, 1m), SubmittedAt);

        session.ScorePercent.Should().Be(50.00m);
    }

    [Fact]
    public void SubmitExam_SetsSubmittedAtAndTouches()
    {
        var session = _builder.Build();

        session.SubmitExam(new Dictionary<Guid, QuestionGrade>(), SubmittedAt);

        session.SubmittedAt.Should().Be(SubmittedAt);
        session.LastActivityAt.Should().Be(SubmittedAt);
        session.UpdationDate.Should().Be(SubmittedAt);
    }

    [Fact]
    public void SubmitExam_AlreadySubmitted_ReturnsEmptyAndKeepsScore()
    {
        var session = SessionWithFirstAnswerSaved();
        session.SubmitExam(Grades(session, 1m), SubmittedAt);

        var second = session.SubmitExam(Grades(session, 0m), SubmittedAt.AddMinutes(1));

        second.Should().BeEmpty();
        session.ScorePercent.Should().Be(50.00m);
        session.Attempts.Should().HaveCount(1);
        session.SubmittedAt.Should().Be(SubmittedAt);
    }

    [Fact]
    public void SubmitExam_MissingGrade_ThrowsInvalidOperation()
    {
        var session = SessionWithFirstAnswerSaved();

        var act = () => session.SubmitExam(new Dictionary<Guid, QuestionGrade>(), SubmittedAt);

        act.Should().Throw<InvalidOperationException>();
        session.IsSubmitted.Should().BeFalse();
        session.Attempts.Should().BeEmpty();
    }

    [Fact]
    public void SubmitExam_QuizSession_ThrowsInvalidOperation()
    {
        var quiz = new SessionBuilder().Build();

        var act = () => quiz.SubmitExam(new Dictionary<Guid, QuestionGrade>(), SubmittedAt);

        act.Should().Throw<InvalidOperationException>();
        quiz.IsSubmitted.Should().BeFalse();
    }

    [Fact]
    public void Submit_ExamSession_ThrowsInvalidOperation()
    {
        var session = _builder.Build();

        var act = session.Submit;

        act.Should().Throw<InvalidOperationException>();
        session.IsSubmitted.Should().BeFalse();
    }

    private Session SessionWithFirstAnswerSaved()
    {
        var session = _builder.Build(count: 2);
        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));
        return session;
    }

    private static Dictionary<Guid, QuestionGrade> Grades(Session session, decimal normalised) => new() { [session.Items[0].QuestionId] = SessionBuilder.Grade(normalised) };
}
