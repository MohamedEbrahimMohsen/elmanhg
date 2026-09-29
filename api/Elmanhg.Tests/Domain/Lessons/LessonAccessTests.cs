using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Lessons;

public sealed class LessonAccessTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _mechanics = Guid.NewGuid();
    private readonly Guid _waves = Guid.NewGuid();

    [Fact]
    public void OpenLessonIds_NullLimit_ReturnsEveryLesson()
    {
        List<LessonPosition> lessons = [Position(_mechanics, 1), Position(_mechanics, 2), Position(_waves, 1)];

        var open = LessonAccess.OpenLessonIds(lessons, null);

        open.Should().BeEquivalentTo(lessons.Select(x => x.Id));
    }

    [Fact]
    public void OpenLessonIds_LimitOne_ReturnsFirstLessonOfEachUnitByOrder()
    {
        var mechanicsSecond = Position(_mechanics, 2);
        var mechanicsFirst = Position(_mechanics, 1);
        var wavesThird = Position(_waves, 3);
        var wavesSecond = Position(_waves, 2);

        var open = LessonAccess.OpenLessonIds([mechanicsSecond, wavesThird, mechanicsFirst, wavesSecond], 1);

        open.Should().BeEquivalentTo([mechanicsFirst.Id, wavesSecond.Id]);
    }

    [Fact]
    public void OpenLessonIds_EqualOrder_BreaksTieByCreationDateThenId()
    {
        var later = Position(_mechanics, 1, Created.AddMinutes(1));
        var earlier = Position(_mechanics, 1, Created);
        var lowerId = new LessonPosition(new Guid("00000000-0000-0000-0000-000000000001"), _waves, 1, Created);
        var higherId = new LessonPosition(new Guid("00000000-0000-0000-0000-000000000002"), _waves, 1, Created);

        var open = LessonAccess.OpenLessonIds([later, earlier, higherId, lowerId], 1);

        open.Should().BeEquivalentTo([earlier.Id, lowerId.Id]);
    }

    [Fact]
    public void OpenLessonIds_LimitZero_ReturnsNoLesson()
    {
        var open = LessonAccess.OpenLessonIds([Position(_mechanics, 1), Position(_waves, 1)], 0);

        open.Should().BeEmpty();
    }

    [Fact]
    public void OpenLessonIds_LimitAboveUnitSize_ReturnsWholeUnit()
    {
        List<LessonPosition> lessons = [Position(_mechanics, 1), Position(_mechanics, 2)];

        var open = LessonAccess.OpenLessonIds(lessons, 5);

        open.Should().BeEquivalentTo(lessons.Select(x => x.Id));
    }

    [Fact]
    public void IsOpen_LessonNotInPublishedList_ReturnsFalse()
    {
        var isOpen = LessonAccess.IsOpen(Guid.NewGuid(), [Position(_mechanics, 1)], 1);

        isOpen.Should().BeFalse();
    }

    [Fact]
    public void Of_Lesson_CopiesIdUnitOrderAndCreationDate()
    {
        var unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());
        var lesson = Lesson.Create(unit, "Forces", 3, Guid.NewGuid());

        var position = LessonPosition.Of(lesson);

        position.Should().Be(new LessonPosition(lesson.Id, unit.Id, 3, lesson.CreationDate));
    }

    private static LessonPosition Position(Guid unitId, int order, DateTimeOffset? created = null) => new(Guid.NewGuid(), unitId, order, created ?? Created);
}
