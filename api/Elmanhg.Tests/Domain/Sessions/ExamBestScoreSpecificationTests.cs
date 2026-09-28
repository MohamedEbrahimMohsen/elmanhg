using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class ExamBestScoreSpecificationTests
{
    private static readonly DateTimeOffset SubmittedAt = ExamSessionBuilder.Now.AddMinutes(1);

    [Fact]
    public void IsSatisfiedBy_SubmittedUnitExam_ReturnsTrue()
    {
        var session = Submitted(new ExamSessionBuilder().Build());

        ExamBestScoreSpecification.IsSatisfiedBy(session).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_SubmittedMultiUnitExam_ReturnsTrue()
    {
        var session = Submitted(new MultiUnitExamBuilder().Build());

        ExamBestScoreSpecification.IsSatisfiedBy(session).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_OpenExam_ReturnsFalse()
    {
        var session = new ExamSessionBuilder().Build();

        ExamBestScoreSpecification.IsSatisfiedBy(session).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_TestModeExam_ReturnsFalse()
    {
        var session = Submitted(new ExamSessionBuilder().Build(isTestMode: true));

        ExamBestScoreSpecification.IsSatisfiedBy(session).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_SubmittedQuiz_ReturnsFalse()
    {
        var quiz = new SessionBuilder().Build();
        quiz.RecordAttempt(quiz.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        quiz.Submit();

        ExamBestScoreSpecification.IsSatisfiedBy(quiz).Should().BeFalse();
    }

    [Fact]
    public void WhereCountsTowardBestScore_KeepsOnlyTheStudentsCountedSittings()
    {
        var builder = new ExamSessionBuilder();
        var counted = Submitted(builder.Build());
        var otherStudents = Submitted(new ExamSessionBuilder().Build());
        var open = builder.Build();

        var result = new[] { counted, otherStudents, open }.AsQueryable().WhereCountsTowardBestScore(builder.StudentId).ToList();

        result.Should().Equal(counted);
    }

    private static Session Submitted(Session session)
    {
        session.SubmitExam(new Dictionary<Guid, QuestionGrade>(), SubmittedAt);
        return session;
    }
}
