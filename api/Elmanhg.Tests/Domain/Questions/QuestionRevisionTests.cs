using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionRevisionTests
{
    private readonly QuestionBuilder _builder = new();

    [Fact]
    public void ReadSnapshot_CreatedQuestion_ReturnsServedContent()
    {
        var question = _builder.Build();

        var snapshot = question.Revisions.Single().ReadSnapshot();

        snapshot.Type.Should().Be(QuestionType.Mcq);
        snapshot.Stem.Should().Be(QuestionBuilder.McqContent().Stem);
        snapshot.MaxScore.Should().Be(1);
        snapshot.GradingSpec!["correctOptionId"]!.GetValue<string>().Should().Be("b");
    }

    [Fact]
    public void Grade_AfterContentEdit_GradesAgainstOldVersion()
    {
        var question = _builder.Approved().Build();
        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { GradingSpec = """{"correctOptionId":"a"}""" }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _builder.Lesson, Guid.NewGuid());
        var answer = QuestionBuilder.Json(SessionBuilder.AnswerB);

        var original = question.Revisions.Single(x => x.Version == 1).Grade(answer);
        var edited = question.Revisions.Single(x => x.Version == 2).Grade(answer);

        original.Outcome.Should().Be(GradeOutcome.Correct);
        edited.Outcome.Should().Be(GradeOutcome.Incorrect);
    }
}
