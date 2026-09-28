using Elmanhg.Domain.Mastery;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Mastery;

public sealed class WeakSpotsTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly Guid LessonId = Guid.NewGuid();

    [Fact]
    public void PickLessons_UnseenOrFullyMastered_AreExcluded()
    {
        var unseen = Lesson(1, 1, 1, servable: 4, mastered: 0, seen: 0);
        var mastered = Lesson(1, 1, 2, servable: 4, mastered: 4, seen: 4);
        var empty = Lesson(1, 1, 3, servable: 0, mastered: 0, seen: 1);
        var partial = Lesson(1, 1, 4, servable: 4, mastered: 1, seen: 2);

        var weak = WeakSpots.PickLessons([unseen, mastered, empty, partial], 4);

        weak.Should().Equal(partial);
    }

    [Fact]
    public void PickLessons_Candidates_OrdersByLowestRatioThenCurriculumOrder()
    {
        var half = Lesson(1, 1, 1, servable: 2, mastered: 1, seen: 2);
        var quarter = Lesson(9, 9, 9, servable: 4, mastered: 1, seen: 4);
        var subjectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var laterSubject = Lesson(3, 1, 1, servable: 2, mastered: 0, seen: 1);
        var laterUnit = Lesson(2, 2, 1, servable: 2, mastered: 0, seen: 1) with { SubjectId = subjectId };
        var laterLesson = Lesson(2, 1, 2, servable: 2, mastered: 0, seen: 1) with { SubjectId = subjectId, UnitId = unitId };
        var first = Lesson(2, 1, 1, servable: 2, mastered: 0, seen: 1) with { SubjectId = subjectId, UnitId = unitId };

        var weak = WeakSpots.PickLessons([half, quarter, laterSubject, laterUnit, laterLesson, first], 20);

        weak.Should().Equal(first, laterLesson, laterUnit, laterSubject, quarter, half);
    }

    [Fact]
    public void PickLessons_MoreThanCount_TakesCount()
    {
        List<LessonMasteryCount> candidates = [.. Enumerable.Range(0, 5).Select(index => Lesson(1, 1, index + 1, servable: 10, mastered: 4 - index, seen: 10))];

        var weak = WeakSpots.PickLessons(candidates, 4);

        weak.Should().Equal(candidates[4], candidates[3], candidates[2], candidates[1]);
    }

    [Fact]
    public void PickObjectives_UnseenOrFullyMastered_AreExcluded()
    {
        var unseen = Objective(1, servable: 3, mastered: 0, seen: 0);
        var mastered = Objective(2, servable: 3, mastered: 3, seen: 3);
        var empty = Objective(3, servable: 0, mastered: 0, seen: 1);
        var partial = Objective(4, servable: 3, mastered: 1, seen: 3);

        var weak = WeakSpots.PickObjectives([unseen, mastered, empty, partial], 3);

        weak.Should().Equal(partial);
    }

    [Fact]
    public void PickObjectives_SameLessonAndRatio_OrdersByObjectiveOrder()
    {
        var second = Objective(2, servable: 2, mastered: 1, seen: 2) with { ObjectiveId = Guid.Parse("00000000-0000-4000-8000-000000000001") };
        var first = second with { ObjectiveId = Guid.Parse("ffffffff-ffff-4fff-bfff-ffffffffffff"), ObjectiveOrder = 1 };

        var weak = WeakSpots.PickObjectives([second, first], 3);

        weak.Should().Equal(first, second);
    }

    [Fact]
    public void PickObjectives_MoreThanCount_TakesCount()
    {
        List<ObjectiveMasteryCount> candidates = [.. Enumerable.Range(0, 5).Select(index => Objective(index + 1, servable: 10, mastered: index, seen: 10))];

        var weak = WeakSpots.PickObjectives(candidates, 3);

        weak.Should().Equal(candidates[0], candidates[1], candidates[2]);
    }

    private static LessonMasteryCount Lesson(int subjectOrder, int unitOrder, int lessonOrder, int servable, int mastered, int seen) => new(Guid.NewGuid(), subjectOrder, Guid.NewGuid(), unitOrder, Guid.NewGuid(), lessonOrder, servable, mastered, seen);

    private static ObjectiveMasteryCount Objective(int objectiveOrder, int servable, int mastered, int seen) => new(SubjectId, 1, UnitId, 1, LessonId, 1, Guid.NewGuid(), objectiveOrder, servable, mastered, seen);
}
