using Elmanhg.Domain.Mastery;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Mastery;

public sealed class NextLessonRecommendationTests
{
    [Fact]
    public void Pick_Lessons_ReturnsLowestMastery()
    {
        var half = Lesson(1, 1, 1, servable: 10, mastered: 5);
        var tenth = Lesson(2, 2, 2, servable: 10, mastered: 1);

        var next = NextLessonRecommendation.Pick([half, tenth]);

        next.Should().Be(tenth);
    }

    [Fact]
    public void Pick_EqualMastery_ReturnsFirstInCurriculumOrder()
    {
        var laterSubject = Lesson(2, 1, 1, servable: 4, mastered: 0);
        var earlierSubject = Lesson(1, 3, 1, servable: 4, mastered: 0);
        var subjectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var secondLesson = Lesson(1, 1, 2, servable: 4, mastered: 0) with { SubjectId = subjectId, UnitId = unitId };
        var firstLesson = Lesson(1, 1, 1, servable: 4, mastered: 0) with { SubjectId = subjectId, UnitId = unitId };

        var bySubject = NextLessonRecommendation.Pick([laterSubject, earlierSubject]);
        var byLesson = NextLessonRecommendation.Pick([secondLesson, firstLesson]);

        bySubject.Should().Be(earlierSubject);
        byLesson.Should().Be(firstLesson);
    }

    [Fact]
    public void Pick_OnlyMasteredOrEmptyLessons_ReturnsNull()
    {
        var mastered = Lesson(1, 1, 1, servable: 5, mastered: 5);
        var empty = Lesson(1, 1, 2, servable: 0, mastered: 0);

        var next = NextLessonRecommendation.Pick([mastered, empty]);

        next.Should().BeNull();
    }

    private static LessonMasteryCount Lesson(int subjectOrder, int unitOrder, int lessonOrder, int servable, int mastered) => new(Guid.NewGuid(), subjectOrder, Guid.NewGuid(), unitOrder, Guid.NewGuid(), lessonOrder, servable, mastered, mastered);
}
