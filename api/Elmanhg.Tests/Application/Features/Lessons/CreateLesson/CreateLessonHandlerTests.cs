using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.CreateLesson;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Lessons.CreateLesson;

public sealed class CreateLessonHandlerTests
{
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());
    private readonly CreateLessonHandler _handler;
    private Lesson? _added;

    public CreateLessonHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _unitRepository.GetByIdAsync(_unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_unit);
        _lessonRepository.AddAsync(Arg.Do<Lesson>(x => _added = x), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _handler = new CreateLessonHandler(_unitRepository, _lessonRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingLessons_AddsDraftLessonAfterLastAndSaves()
    {
        var last = Lesson.Create(_unit, "Momentum", 2, Guid.NewGuid());
        _lessonRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(last);

        var result = await _handler.Handle(new CreateLessonCommand(_unit.Id, "Energy"), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        _added!.Order.Should().Be(3);
        _added.UnitId.Should().Be(_unit.Id);
        _added.State.Should().Be(LessonState.Draft);
        result.Id.Should().Be(_added.Id);
        await _lessonRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoLessons_AddsLessonAtOrderOne()
    {
        await _handler.Handle(new CreateLessonCommand(_unit.Id, "Energy"), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        _added!.Order.Should().Be(1);
        await _lessonRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitNotFound_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new CreateLessonCommand(Guid.NewGuid(), "Energy"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _lessonRepository.DidNotReceive().AddAsync(Arg.Any<Lesson>(), Arg.Any<CancellationToken>());
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new CreateLessonCommand(_unit.Id, "Energy"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
