using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class EssayGraderTests
{
    private static readonly EssayGradingSpec Spec = new([Criterion("c1", 2), Criterion("c2", 3)], ["<p>Model</p>"]);

    [Fact]
    public void Grade_AllFullPoints_ReturnsOne()
    {
        var grade = EssayGrader.Grade(Spec, [new("c1", 2), new("c2", 3)]);

        grade.Should().Be(new NormalisedGrade(1m, null));
    }

    [Fact]
    public void Grade_PartialPoints_ReturnsAwardedOverTotal()
    {
        var grade = EssayGrader.Grade(Spec, [new("c1", 1), new("c2", 3)]);

        grade.Value.Should().Be(0.8m);
    }

    [Fact]
    public void Grade_MissingCriterion_ThrowsInvalidOperationException()
    {
        var act = () => EssayGrader.Grade(Spec, [new("c1", 2)]);

        act.Should().Throw<InvalidOperationException>().WithMessage("Essay awards do not match the rubric.");
    }

    [Fact]
    public void Grade_UnknownCriterion_ThrowsInvalidOperationException()
    {
        var act = () => EssayGrader.Grade(Spec, [new("c1", 2), new("c9", 1)]);

        act.Should().Throw<InvalidOperationException>().WithMessage("Essay awards do not match the rubric.");
    }

    [Fact]
    public void Grade_DuplicateCriterion_ThrowsInvalidOperationException()
    {
        var act = () => EssayGrader.Grade(Spec, [new("c1", 1), new("c1", 1)]);

        act.Should().Throw<InvalidOperationException>().WithMessage("Essay awards do not match the rubric.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Grade_PointsOutsideCriterion_ThrowsInvalidOperationException(int points)
    {
        var act = () => EssayGrader.Grade(Spec, [new("c1", points), new("c2", 3)]);

        act.Should().Throw<InvalidOperationException>().WithMessage("Essay awards do not match the rubric.");
    }

    private static RubricCriterion Criterion(string id, int points) => new(id, id, null, points, [new RubricLevel(0, "None"), new RubricLevel(points, "Full")]);
}
