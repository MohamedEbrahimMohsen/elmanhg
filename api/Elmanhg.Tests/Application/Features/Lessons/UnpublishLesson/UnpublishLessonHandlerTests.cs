using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UnpublishLesson;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Lessons.UnpublishLesson;

public sealed class UnpublishLessonHandlerTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Lesson _lesson = Lesson.Create(CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid()), "Newton's laws", 1, Guid.NewGuid());
    private readonly UnpublishLessonHandler _handler;

    public UnpublishLessonHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _lessonRepository.GetByIdAsync(_lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_lesson);
        _handler = new UnpublishLessonHandler(_lessonRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_PublishedLesson_ReturnsToDraftAndSaves()
    {
        _lesson.Publish(_currentUserId);

        await _handler.Handle(new UnpublishLessonCommand(_lesson.Id), TestContext.Current.CancellationToken);

        _lesson.State.Should().Be(LessonState.Draft);
        await _lessonRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DraftLesson_ThrowsLessonAlreadyDraft()
    {
        var act = () => _handler.Handle(new UnpublishLessonCommand(_lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.LessonAlreadyDraft);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new UnpublishLessonCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UnpublishLessonCommand(_lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
