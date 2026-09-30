using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class AttemptsRecordedEventTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset SubmittedAt = ExamSessionBuilder.Now.AddMinutes(10);

    [Fact]
    public void RecordAttempt_NewAttempt_RaisesAttemptsRecordedWithAttempt()
    {
        var session = new SessionBuilder().Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        var recorded = session.GetDomainEvents().OfType<AttemptsRecorded>().Should().ContainSingle().Subject;
        recorded.Session.Should().BeSameAs(session);
        recorded.Attempts.Should().Equal(attempt);
    }

    [Fact]
    public void RecordAttempt_SameAnswerAgain_RaisesNoSecondEvent()
    {
        var session = new SessionBuilder().Build();
        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        session.GetDomainEvents().OfType<AttemptsRecorded>().Should().ContainSingle();
    }

    [Fact]
    public void SubmitExam_AnsweredItems_RaisesOneEventWithAllAttempts()
    {
        var session = new ExamSessionBuilder().Build(count: 2);
        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));
        session.SaveExamAnswer(session.Items[1], SessionBuilder.AnswerA, Grace, ExamSessionBuilder.Now.AddMinutes(2));

        var attempts = session.SubmitExam(Grades(session, 1m, 0m), SubmittedAt);

        var recorded = session.GetDomainEvents().OfType<AttemptsRecorded>().Should().ContainSingle().Subject;
        recorded.Attempts.Should().HaveCount(2).And.Equal(attempts);
    }

    [Fact]
    public void SubmitExam_NoAnsweredItems_RaisesNoEvent()
    {
        var session = new ExamSessionBuilder().Build(count: 2);

        session.SubmitExam(new Dictionary<Guid, QuestionGrade>(), SubmittedAt);

        session.GetDomainEvents().OfType<AttemptsRecorded>().Should().BeEmpty();
    }

    private static Dictionary<Guid, QuestionGrade> Grades(Session session, decimal first, decimal second) => new()
    {
        [session.Items[0].QuestionId] = SessionBuilder.Grade(first),
        [session.Items[1].QuestionId] = SessionBuilder.Grade(second),
    };
}
