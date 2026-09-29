using Core.Errors;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Exams.Shared;

public sealed class ExamLessonGateTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ILessonOpeningRepository _lessonOpeningRepository = Substitute.For<ILessonOpeningRepository>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());
    private readonly List<Lesson> _lessons = [];
    private readonly List<LessonOpening> _openings = [];

    public ExamLessonGateTests()
    {
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _lessonOpeningRepository.FindAsync(Arg.Any<Expression<Func<LessonOpening, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<LessonOpening>, IQueryable<LessonOpening>>?>(), Arg.Any<Func<IQueryable<LessonOpening>, IOrderedQueryable<LessonOpening>>?>(), Arg.Any<bool>())
            .Returns(call => _openings.Where(call.Arg<Expression<Func<LessonOpening, bool>>>().Compile()).ToList());
    }

    [Fact]
    public async Task CountUnopenedAsync_SomeLessonsOpened_ReturnsUnopenedCount()
    {
        var opened = AddLesson(publish: true);
        var unopened = AddLesson(publish: true);
        AddLesson(publish: true);
        AddLesson(publish: false);
        _openings.Add(LessonOpening.Record(_studentId, opened, DateTimeOffset.UnixEpoch));
        _openings.Add(LessonOpening.Record(Guid.NewGuid(), unopened, DateTimeOffset.UnixEpoch));

        var count = await ExamLessonGate.CountUnopenedAsync(_studentId, [_unit.Id], _lessonRepository, _lessonOpeningRepository, TestContext.Current.CancellationToken);

        count.Should().Be(2);
    }

    [Fact]
    public async Task CountUnopenedAsync_NoPublishedLessons_ReturnsZero()
    {
        AddLesson(publish: false);

        var count = await ExamLessonGate.CountUnopenedAsync(_studentId, [_unit.Id], _lessonRepository, _lessonOpeningRepository, TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task EnsureOpenedAsync_AllOpened_DoesNotThrow()
    {
        var lesson = AddLesson(publish: true);
        _openings.Add(LessonOpening.Record(_studentId, lesson, DateTimeOffset.UnixEpoch));

        var act = () => ExamLessonGate.EnsureOpenedAsync(_studentId, [_unit.Id], _lessonRepository, _lessonOpeningRepository, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureOpenedAsync_LessonUnopened_ThrowsExamLessonsNotOpened()
    {
        AddLesson(publish: true);

        var act = () => ExamLessonGate.EnsureOpenedAsync(_studentId, [_unit.Id], _lessonRepository, _lessonOpeningRepository, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamLessonsNotOpened);
    }

    private Lesson AddLesson(bool publish)
    {
        var lesson = Lesson.Create(_unit, $"Lesson {_lessons.Count + 1}", _lessons.Count + 1, Guid.NewGuid());
        if (publish)
        {
            lesson.Publish(Guid.NewGuid());
        }

        _lessons.Add(lesson);
        return lesson;
    }
}
