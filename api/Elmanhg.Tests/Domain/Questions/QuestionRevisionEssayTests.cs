using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionRevisionEssayTests
{
    [Fact]
    public void GradeEssay_FullAward_ReturnsSnapshotMaxScore()
    {
        var question = new QuestionBuilder().Essay().Build();

        var grade = question.Revisions[0].GradeEssay([new EssayCriterionAward("c1", 2)]);

        (grade.Score, grade.Outcome).Should().Be((5m, GradeOutcome.Correct));
    }
}
