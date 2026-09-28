using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Mastery.GetSubjectMastery;
using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Mastery.GetSubjectMastery;

public sealed class GetSubjectMasteryHandlerTests
{
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _unit;
    private readonly List<Lesson> _lessons = [];
    private readonly GetSubjectMasteryHandler _handler;

    public GetSubjectMasteryHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _unit = CurriculumUnit.Create(_subject, "Mechanics", 1, Guid.NewGuid());
        List<CurriculumUnit> units = [_unit, CurriculumUnit.Create(Subject.Create("Chemistry", 2, Guid.NewGuid()), "Atoms", 1, Guid.NewGuid())];
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => units.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        StubCounts([]);
        _handler = new GetSubjectMasteryHandler(_questionMasteryRepository, _subjectRepository, _unitRepository, _lessonRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetSubjectMasteryQuery(_subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetSubjectMasteryQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
    }

    [Fact]
    public async Task Handle_Subject_ReturnsUnitsAndPublishedLessonsWithWeightedMastery()
    {
        var practised = AddLesson("Forces", publish: true);
        var untouched = AddLesson("Energy", publish: true);
        AddLesson("Draft", publish: false);
        StubCounts([Count(practised, 4, 2, 3), Count(untouched, 4, 0, 0)]);

        var result = await _handler.Handle(new GetSubjectMasteryQuery(_subject.Id), TestContext.Current.CancellationToken);

        (result.SubjectId, result.ServableCount, result.MasteredCount, result.SeenCount, result.MasteryPercent).Should().Be((_subject.Id, 8, 2, 3, 25));
        var unit = result.Units.Should().ContainSingle().Subject;
        (unit.UnitId, unit.ServableCount, unit.MasteredCount, unit.MasteryPercent).Should().Be((_unit.Id, 8, 2, 25));
        unit.Lessons.Should().Equal(new LessonMasteryResult(practised.Id, "Forces", 4, 2, 3, 50), new LessonMasteryResult(untouched.Id, "Energy", 4, 0, 0, 0));
    }

    [Fact]
    public async Task Handle_LessonWithoutServableQuestions_ReturnsZeroCounts()
    {
        var empty = AddLesson("Empty", publish: true);

        var result = await _handler.Handle(new GetSubjectMasteryQuery(_subject.Id), TestContext.Current.CancellationToken);

        result.Units.Single().Lessons.Should().Equal(new LessonMasteryResult(empty.Id, "Empty", 0, 0, 0, 0));
    }

    private Lesson AddLesson(string name, bool publish)
    {
        var lesson = Lesson.Create(_unit, name, _lessons.Count + 1, Guid.NewGuid());
        if (publish)
        {
            lesson.Publish(Guid.NewGuid());
        }

        _lessons.Add(lesson);
        return lesson;
    }

    private LessonMasteryCount Count(Lesson lesson, int servable, int mastered, int seen) => new(_subject.Id, 1, _unit.Id, 1, lesson.Id, lesson.Order, servable, mastered, seen);

    private void StubCounts(List<LessonMasteryCount> counts) => _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns(counts);
}
