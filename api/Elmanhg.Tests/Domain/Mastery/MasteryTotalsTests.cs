using Elmanhg.Domain.Mastery;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Mastery;

public sealed class MasteryTotalsTests
{
    [Fact]
    public void Of_Lessons_PoolsCountsWeightedByQuestionCount()
    {
        List<LessonMasteryCount> lessons = [Lesson(10, 5, 6), Lesson(30, 0, 1)];

        var totals = MasteryTotals.Of(lessons);

        totals.Should().Be(new MasteryTotals(40, 5, 7));
        totals.MasteryPercent.Should().Be(12);
    }

    [Fact]
    public void MasteryPercent_NoServable_IsZero()
    {
        var totals = new MasteryTotals(0, 0, 0);

        totals.MasteryPercent.Should().Be(0);
    }

    [Fact]
    public void MasteryPercent_Fraction_RoundsDown()
    {
        var partial = new MasteryTotals(3, 2, 3);
        var complete = new MasteryTotals(3, 3, 3);

        (partial.MasteryPercent, complete.MasteryPercent).Should().Be((66, 100));
    }

    [Fact]
    public void RemainingCount_ServableMinusMastered()
    {
        var totals = new MasteryTotals(60, 20, 30);

        totals.RemainingCount.Should().Be(40);
    }

    private static LessonMasteryCount Lesson(int servable, int mastered, int seen) => new(Guid.NewGuid(), 1, Guid.NewGuid(), 1, Guid.NewGuid(), 1, servable, mastered, seen);
}
