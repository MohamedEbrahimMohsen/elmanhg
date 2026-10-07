using Core.Localization;
using Core.Storage;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Sessions.Shared;

public sealed class SessionResultGeneratorTests
{
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();
    private readonly SessionBuilder _builder = new();

    public SessionResultGeneratorTests()
    {
        _fileStorage.GetPublicUrl(Arg.Any<string>()).Returns(call => $"/api/media/{call.Arg<string>()}");
    }

    [Fact]
    public void GenerateItem_DragDropItem_AddsResolvedImageUrl()
    {
        var session = _builder.BuildWithDragDrop();
        var revision = new QuestionBuilder().DragDrop().Approved().Build().Revisions[0];

        var result = SessionResultGenerator.GenerateItem(session, session.Items[1], revision, _localizer, _fileStorage);

        var image = result.Body.GetProperty("image");
        image.GetProperty("url").GetString().Should().Be($"/api/media/{QuestionBuilder.DragDropImageKey}");
        image.GetProperty("key").GetString().Should().Be(QuestionBuilder.DragDropImageKey);
    }

    [Fact]
    public void GenerateItem_McqItem_ReturnsStoredBody()
    {
        var session = _builder.BuildWithDragDrop();
        var revision = new QuestionBuilder().Approved().Build().Revisions[0];

        var result = SessionResultGenerator.GenerateItem(session, session.Items[0], revision, _localizer, _fileStorage);

        result.Body.GetProperty("options").GetArrayLength().Should().Be(2);
        result.Body.TryGetProperty("image", out _).Should().BeFalse();
    }
}
