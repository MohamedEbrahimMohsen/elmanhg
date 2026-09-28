using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UploadLessonImage;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Lessons.UploadLessonImage;

public sealed class UploadLessonImageHandlerTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Lesson _lesson = Lesson.Create(CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid()), "Newton's laws", 1, Guid.NewGuid());
    private readonly UploadLessonImageHandler _handler;

    public UploadLessonImageHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _lessonRepository.GetByIdAsync(_lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_lesson);
        _handler = new UploadLessonImageHandler(_lessonRepository, _fileStorage, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingLesson_StoresUnderLessonKeyAndReturnsUrl()
    {
        _fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("/api/media/lessons/stored.png");

        var result = await _handler.Handle(new UploadLessonImageCommand(_lesson.Id, Image("Diagram.PNG")), TestContext.Current.CancellationToken);

        await _fileStorage.Received(1).SaveAsync(Arg.Any<Stream>(), Arg.Is<string>(k => k.StartsWith($"lessons/{_lesson.Id}/") && k.EndsWith(".png")), Arg.Any<CancellationToken>());
        result.Url.Should().Be("/api/media/lessons/stored.png");
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new UploadLessonImageCommand(Guid.NewGuid(), Image("diagram.png")), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UploadLessonImageCommand(_lesson.Id, Image("diagram.png")), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static FormFile Image(string fileName)
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47];
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "image/png" };
    }
}
