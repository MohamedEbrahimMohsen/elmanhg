using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.GetLessons;
using Elmanhg.Application.Lessons.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Lessons.GetLessons;

public sealed class GetLessonsHandlerTests
{
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());
    private readonly GetLessonsHandler _handler;

    public GetLessonsHandlerTests()
    {
        _unitRepository.GetByIdAsync(_unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_unit);
        _handler = new GetLessonsHandler(_unitRepository, _lessonRepository, _questionRepository);
    }

    [Fact]
    public async Task Handle_ExistingUnit_ReturnsMappedLessons()
    {
        var first = Lesson.Create(_unit, "Newton's laws", 1, Guid.NewGuid());
        var second = Lesson.Create(_unit, "Momentum", 2, Guid.NewGuid());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns([first, second]);
        _questionRepository.CountByLessonAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int> { [first.Id] = 3 });

        var result = await _handler.Handle(new GetLessonsQuery(_unit.Id), TestContext.Current.CancellationToken);

        result.Should().Equal(new LessonResult(first.Id, _unit.Id, "Newton's laws", 1, "Draft", 3), new LessonResult(second.Id, _unit.Id, "Momentum", 2, "Draft", 0));
    }

    [Fact]
    public async Task Handle_UnitNotFound_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new GetLessonsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _lessonRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>());
    }
}
