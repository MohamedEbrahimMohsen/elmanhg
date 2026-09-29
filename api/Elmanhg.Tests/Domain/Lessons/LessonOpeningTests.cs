using Core.Errors;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Lessons;

public sealed class LessonOpeningTests
{
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly DateTimeOffset _openedAt = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());

    [Fact]
    public void Record_PublishedLesson_SetsStudentLessonAndTime()
    {
        var lesson = NewLesson();
        lesson.Publish(Guid.NewGuid());

        var opening = LessonOpening.Record(_studentId, lesson, _openedAt);

        (opening.StudentId, opening.LessonId, opening.OpenedAt).Should().Be((_studentId, lesson.Id, _openedAt));
        opening.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Record_DraftLesson_ThrowsLessonNotPublished()
    {
        var lesson = NewLesson();

        var act = () => LessonOpening.Record(_studentId, lesson, _openedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonNotPublished);
    }

    [Fact]
    public void Record_ArchivedLesson_ThrowsLessonNotPublished()
    {
        var lesson = NewLesson();
        lesson.Publish(Guid.NewGuid());
        lesson.Archive(Guid.NewGuid());

        var act = () => LessonOpening.Record(_studentId, lesson, _openedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LessonNotPublished);
    }

    private Lesson NewLesson() => Lesson.Create(_unit, "Energy", 1, Guid.NewGuid());
}
