using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.ReorderLesson;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Lessons.ReorderLesson;

public sealed class ReorderLessonHandlerTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Lesson _first;
    private readonly Lesson _second;
    private readonly Lesson _third;
    private readonly ReorderLessonHandler _handler;

    public ReorderLessonHandlerTests()
    {
        var id = Guid.NewGuid();
        var unit = CurriculumUnit.Create(Subject.Create("Physics", 1, id), "Mechanics", 1, id);
        _first = Lesson.Create(unit, "A", 1, id);
        _second = Lesson.Create(unit, "B", 2, id);
        _third = Lesson.Create(unit, "C", 3, id);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        foreach (var lesson in new[] { _first, _second, _third })
        {
            _lessonRepository.GetByIdAsync(lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(lesson);
        }

        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_ => [_first, _second, _third]);
        _handler = new ReorderLessonHandler(_lessonRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_MoveThirdToFirst_RenumbersSiblingsAndSaves()
    {
        await _handler.Handle(new ReorderLessonCommand(_third.Id, 1), TestContext.Current.CancellationToken);

        _third.Order.Should().Be(1);
        _first.Order.Should().Be(2);
        _second.Order.Should().Be(3);
        await _lessonRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PositionBeyondCount_MovesToLast()
    {
        await _handler.Handle(new ReorderLessonCommand(_first.Id, 99), TestContext.Current.CancellationToken);

        _second.Order.Should().Be(1);
        _third.Order.Should().Be(2);
        _first.Order.Should().Be(3);
        await _lessonRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new ReorderLessonCommand(Guid.NewGuid(), 1), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new ReorderLessonCommand(_first.Id, 3), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
