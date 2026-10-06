using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.UploadDiagramImage;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Lessons.UploadDiagramImage;

public sealed class UploadDiagramImageHandlerTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Lesson _lesson = Lesson.Create(CurriculumUnit.Create(Subject.Create("Biology", 1, Guid.NewGuid()), "Cells", 1, Guid.NewGuid()), "Plant cells", 1, Guid.NewGuid());
    private readonly UploadDiagramImageHandler _handler;

    public UploadDiagramImageHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _lessonRepository.GetByIdAsync(_lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_lesson);
        _handler = new UploadDiagramImageHandler(_lessonRepository, _fileStorage, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingLesson_StoresUnderDiagramKeyAndReturnsUrl()
    {
        _fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("/api/media/question-diagrams/stored.png");

        var result = await _handler.Handle(new UploadDiagramImageCommand(_lesson.Id, Image("Cell.PNG")), TestContext.Current.CancellationToken);

        await _fileStorage.Received(1).SaveAsync(Arg.Any<Stream>(), result.Key, Arg.Any<CancellationToken>());
        result.Key.Should().StartWith($"question-diagrams/{_lesson.Id}/").And.EndWith(".png");
        DiagramImageKey.IsValid(result.Key).Should().BeTrue();
        result.Url.Should().Be("/api/media/question-diagrams/stored.png");
        await _lessonRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new UploadDiagramImageCommand(Guid.NewGuid(), Image("cell.png")), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UploadDiagramImageCommand(_lesson.Id, Image("cell.png")), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static FormFile Image(string fileName)
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "image/png" };
    }
}
