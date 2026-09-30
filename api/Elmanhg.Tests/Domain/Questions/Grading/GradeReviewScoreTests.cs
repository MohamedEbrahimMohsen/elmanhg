using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions.Grading;

public sealed class GradeReviewScoreTests
{
    [Fact]
    public void Normalise_ThirdOfMaxScore_RoundsToFourDecimals()
    {
        GradeReviewScore.Normalise(1m, 3).Should().Be(0.3333m);
    }
}
