using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Lessons;

public sealed class LessonSequenceTests
{
    private static readonly Subject Physics = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _first = CurriculumUnit.Create(Physics, "Mechanics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _second = CurriculumUnit.Create(Physics, "Waves", 2, Guid.NewGuid());
    private readonly List<Lesson> _lessons = [];

    [Fact]
    public void Order_LessonsAcrossUnits_OrdersByUnitThenLessonOrder()
    {
        var waves = Lesson.Create(_second, "Wave basics", 1, Guid.NewGuid());
        var energy = Lesson.Create(_first, "Energy", 2, Guid.NewGuid());
        var forces = Lesson.Create(_first, "Forces", 1, Guid.NewGuid());

        var ordered = LessonSequence.Order([_second, _first], [waves, energy, forces]);

        ordered.Should().Equal(forces, energy, waves);
    }

    [Fact]
    public void Order_LessonOfUnknownUnit_IsExcluded()
    {
        var forces = Lesson.Create(_first, "Forces", 1, Guid.NewGuid());
        var stray = Lesson.Create(_second, "Stray", 1, Guid.NewGuid());

        var ordered = LessonSequence.Order([_first], [forces, stray]);

        ordered.Should().Equal(forces);
    }

    [Fact]
    public void Neighbours_MiddleLesson_ReturnsPreviousAndNext()
    {
        var (forces, energy, power, _) = Sequence();

        var neighbours = LessonSequence.Neighbours(Ordered(), energy.Id);

        neighbours.Should().Be(new LessonNeighbours(forces, power));
    }

    [Fact]
    public void Neighbours_LastLessonOfUnit_NextIsFirstLessonOfNextUnit()
    {
        var (_, energy, power, waves) = Sequence();

        var neighbours = LessonSequence.Neighbours(Ordered(), power.Id);

        neighbours.Should().Be(new LessonNeighbours(energy, waves));
    }

    [Fact]
    public void Neighbours_FirstLessonOfSubject_HasNoPrevious()
    {
        var (forces, energy, _, _) = Sequence();

        var neighbours = LessonSequence.Neighbours(Ordered(), forces.Id);

        neighbours.Previous.Should().BeNull();
        neighbours.Next.Should().Be(energy);
    }

    [Fact]
    public void Neighbours_LastLessonOfSubject_HasNoNext()
    {
        var (_, _, _, waves) = Sequence();

        var neighbours = LessonSequence.Neighbours(Ordered(), waves.Id);

        neighbours.Next.Should().BeNull();
    }

    [Fact]
    public void Neighbours_LessonNotInSequence_ReturnsNoNeighbours()
    {
        Sequence();

        var neighbours = LessonSequence.Neighbours(Ordered(), Guid.NewGuid());

        neighbours.Should().Be(new LessonNeighbours(null, null));
    }

    private (Lesson Forces, Lesson Energy, Lesson Power, Lesson Waves) Sequence()
    {
        var forces = Lesson.Create(_first, "Forces", 1, Guid.NewGuid());
        var energy = Lesson.Create(_first, "Energy", 2, Guid.NewGuid());
        var power = Lesson.Create(_first, "Power", 3, Guid.NewGuid());
        var waves = Lesson.Create(_second, "Wave basics", 1, Guid.NewGuid());
        _lessons.AddRange([waves, power, forces, energy]);
        return (forces, energy, power, waves);
    }

    private List<Lesson> Ordered() => LessonSequence.Order([_first, _second], _lessons);
}
