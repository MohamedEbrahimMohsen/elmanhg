using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions.Exams;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Exams;

public sealed class ExamDifficultyTargetsTests
{
    [Theory]
    [InlineData(10, 30, 50, 20, 3, 5, 2)]
    [InlineData(3, 30, 50, 20, 1, 1, 1)]
    [InlineData(1, 0, 100, 0, 0, 1, 0)]
    [InlineData(7, 34, 33, 33, 3, 2, 2)]
    public void Apportion_Mix_ReturnsLargestRemainderTargets(int count, int easy, int medium, int hard, int expectedEasy, int expectedMedium, int expectedHard)
    {
        var targets = ExamDifficultyTargets.Apportion(count, new ExamDifficultyMix(easy, medium, hard));

        targets.Should().BeEquivalentTo(new Dictionary<QuestionDifficulty, int> { [QuestionDifficulty.Easy] = expectedEasy, [QuestionDifficulty.Medium] = expectedMedium, [QuestionDifficulty.Hard] = expectedHard });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(100)]
    public void Apportion_AnyCount_SumsToCount(int count)
    {
        var targets = ExamDifficultyTargets.Apportion(count, new ExamDifficultyMix(33, 33, 34));

        targets.Values.Sum().Should().Be(count);
        targets.Keys.Should().BeEquivalentTo(Enum.GetValues<QuestionDifficulty>());
    }
}
