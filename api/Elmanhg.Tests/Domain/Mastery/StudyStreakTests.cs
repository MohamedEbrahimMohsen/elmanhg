using Elmanhg.Domain.Mastery;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Mastery;

public sealed class StudyStreakTests
{
    private static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public void Count_NoActivity_ReturnsZero()
    {
        var streak = StudyStreak.Count([], Today);

        streak.Should().Be(0);
    }

    [Fact]
    public void Count_ActiveToday_CountsConsecutiveDaysBack()
    {
        var streak = StudyStreak.Count(Days(0, -1, -2, -4), Today);

        streak.Should().Be(3);
    }

    [Fact]
    public void Count_InactiveTodayActiveYesterday_CountsFromYesterday()
    {
        var streak = StudyStreak.Count(Days(-1, -2), Today);

        streak.Should().Be(2);
    }

    [Fact]
    public void Count_LastActiveTwoDaysAgo_ReturnsZero()
    {
        var streak = StudyStreak.Count(Days(-2, -3), Today);

        streak.Should().Be(0);
    }

    private static List<DateOnly> Days(params int[] offsets) => offsets.Select(Today.AddDays).ToList();
}
