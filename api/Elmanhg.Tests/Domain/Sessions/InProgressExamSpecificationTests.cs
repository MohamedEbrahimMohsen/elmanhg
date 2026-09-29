using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class InProgressExamSpecificationTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly ExamSessionBuilder _builder = new();

    [Fact]
    public void For_OpenTimedExamBeforeDeadline_IsTrue()
    {
        var exam = _builder.Build();

        IsInProgress(exam, _builder.StudentId, ExamSessionBuilder.Now.AddMinutes(5)).Should().BeTrue();
    }

    [Fact]
    public void For_OpenExamPastDeadlineWithinGrace_IsTrue()
    {
        var exam = _builder.Build();

        IsInProgress(exam, _builder.StudentId, exam.Deadline!.Value.AddSeconds(10)).Should().BeTrue();
    }

    [Fact]
    public void For_OpenExamPastGrace_IsFalse()
    {
        var exam = _builder.Build();

        IsInProgress(exam, _builder.StudentId, exam.Deadline!.Value.AddSeconds(31)).Should().BeFalse();
    }

    [Fact]
    public void For_UntimedOpenExam_IsTrue()
    {
        var exam = _builder.Build(timeLimitMinutes: null);

        IsInProgress(exam, _builder.StudentId, ExamSessionBuilder.Now.AddDays(3)).Should().BeTrue();
    }

    [Fact]
    public void For_SubmittedExam_IsFalse()
    {
        var exam = _builder.Build();
        exam.SubmitExam(new Dictionary<Guid, QuestionGrade>(), ExamSessionBuilder.Now.AddMinutes(1));

        IsInProgress(exam, _builder.StudentId, ExamSessionBuilder.Now.AddMinutes(2)).Should().BeFalse();
    }

    [Fact]
    public void For_OpenQuiz_IsFalse()
    {
        var sessionBuilder = new SessionBuilder();
        var quiz = sessionBuilder.Build();

        IsInProgress(quiz, sessionBuilder.StudentId, ExamSessionBuilder.Now).Should().BeFalse();
    }

    [Fact]
    public void For_OtherStudentsExam_IsFalse()
    {
        var exam = _builder.Build();

        IsInProgress(exam, Guid.NewGuid(), ExamSessionBuilder.Now.AddMinutes(5)).Should().BeFalse();
    }

    private static bool IsInProgress(Session session, Guid studentId, DateTimeOffset now) => InProgressExamSpecification.For(studentId, now, Grace).Compile()(session);
}
