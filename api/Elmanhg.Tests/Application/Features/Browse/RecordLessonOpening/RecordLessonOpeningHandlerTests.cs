using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.RecordLessonOpening;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Browse.RecordLessonOpening;

public sealed class RecordLessonOpeningHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ILessonOpeningRepository _lessonOpeningRepository = Substitute.For<ILessonOpeningRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Lesson _lesson;
    private readonly RecordLessonOpeningHandler _handler;

    public RecordLessonOpeningHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _lesson = NewLesson();
        _lesson.Publish(Guid.NewGuid());
        StubLesson(_lesson);
        _handler = new RecordLessonOpeningHandler(_lessonRepository, _lessonOpeningRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new RecordLessonOpeningCommand(_lesson.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _lessonOpeningRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownLesson_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new RecordLessonOpeningCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _lessonOpeningRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DraftLesson_ThrowsLessonNotFound()
    {
        var draft = NewLesson();
        StubLesson(draft);

        var act = () => _handler.Handle(new RecordLessonOpeningCommand(draft.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _lessonOpeningRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstOpening_AddsOpeningAndSaves()
    {
        await _handler.Handle(new RecordLessonOpeningCommand(_lesson.Id), TestContext.Current.CancellationToken);

        await _lessonOpeningRepository.Received(1).AddAsync(Arg.Is<LessonOpening>(x => x.StudentId == _studentId && x.LessonId == _lesson.Id && x.OpenedAt == Now), Arg.Any<CancellationToken>());
        await _lessonOpeningRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyOpened_DoesNotAddOrSave()
    {
        _lessonOpeningRepository.IsOpenedAsync(_studentId, _lesson.Id, Arg.Any<CancellationToken>()).Returns(true);

        await _handler.Handle(new RecordLessonOpeningCommand(_lesson.Id), TestContext.Current.CancellationToken);

        await _lessonOpeningRepository.DidNotReceive().AddAsync(Arg.Any<LessonOpening>(), Arg.Any<CancellationToken>());
        await _lessonOpeningRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Lesson NewLesson() => Lesson.Create(CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid()), "Energy", 1, Guid.NewGuid());

    private void StubLesson(Lesson lesson) => _lessonRepository.GetByIdAsync(lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(lesson);
}
