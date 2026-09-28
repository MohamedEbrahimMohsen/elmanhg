using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UpdateLesson;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Lessons.UpdateLesson;

public sealed class UpdateLessonHandlerTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Lesson _lesson = Lesson.Create(CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid()), "Newton's laws", 1, Guid.NewGuid());
    private readonly UpdateLessonHandler _handler;

    public UpdateLessonHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _lessonRepository.GetWithObjectivesAsync(_lesson.Id, false, Arg.Any<CancellationToken>()).Returns(_lesson);
        _richTextSanitizer.Sanitize("raw-e").Returns("clean-e");
        _richTextSanitizer.Sanitize("raw-s").Returns("clean-s");
        _handler = new UpdateLessonHandler(_lessonRepository, _richTextSanitizer, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingLesson_SanitisesContentUpdatesAndSaves()
    {
        var command = new UpdateLessonCommand(_lesson.Id, "Momentum", "raw-e", "raw-s", null, [new LessonObjectiveContent(null, "Define momentum")]);

        await _handler.Handle(command, TestContext.Current.CancellationToken);

        _lesson.Name.Should().Be("Momentum");
        _lesson.Explanation.Should().Be("clean-e");
        _lesson.Summary.Should().Be("clean-s");
        _lesson.Objectives.Should().ContainSingle().Which.Text.Should().Be("Define momentum");
        _lesson.UpdatedBy.Should().Be(_currentUserId);
        await _lessonRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new UpdateLessonCommand(Guid.NewGuid(), "Momentum", "raw-e", "raw-s", null, []), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        _richTextSanitizer.DidNotReceive().Sanitize(Arg.Any<string?>());
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownObjective_ThrowsLessonObjectiveUnknown()
    {
        var command = new UpdateLessonCommand(_lesson.Id, "Momentum", "raw-e", "raw-s", null, [new LessonObjectiveContent(Guid.NewGuid(), "Unknown")]);

        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.LessonObjectiveUnknown);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UpdateLessonCommand(_lesson.Id, "Momentum", "raw-e", "raw-s", null, []), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
