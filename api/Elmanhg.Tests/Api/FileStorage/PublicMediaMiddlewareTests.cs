using Elmanhg.Api.FileStorage;
using Elmanhg.Application.Shared.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Elmanhg.Tests.Api.FileStorage;

public sealed class PublicMediaMiddlewareTests
{
    private static readonly byte[] Bytes = [0x89, 0x50, 0x4E, 0x47];
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private bool _nextCalled;

    [Fact]
    public async Task Invoke_PublicKey_StreamsFileWithPublicCache()
    {
        _fileStorage.OpenReadAsync("lessons/a/b.png", Arg.Any<CancellationToken>()).Returns(new StoredFile(new MemoryStream(Bytes), Bytes.Length, "image/png"));

        var context = await InvokeAsync("/api/media/lessons/a/b.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(Bytes);
        (context.Response.ContentType, context.Response.ContentLength).Should().Be(("image/png", (long?)Bytes.Length));
        context.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        context.Response.Headers.CacheControl.ToString().Should().Be("public, max-age=31536000, immutable");
    }

    [Theory]
    [InlineData("teacher-threads/x.png")]
    [InlineData("Teacher-Threads/x.png")]
    [InlineData("teacher-threads./x.png")]
    [InlineData("teacher-threads%2Fx.webm")]
    [InlineData("teacher-threads%2fx.webm")]
    public async Task Invoke_PrivateFolderAnySpelling_Returns404WithoutReading(string key)
    {
        var context = await InvokeAsync($"/api/media/{key}");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("training-exports/x.jsonl")]
    [InlineData("Training-Exports./x.jsonl")]
    public async Task Invoke_TrainingExportKey_Returns404WithoutReading(string key)
    {
        var context = await InvokeAsync($"/api/media/{key}");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("lessons/../x.png")]
    [InlineData("lessons//x.png")]
    [InlineData("lessons\\x.png")]
    [InlineData("a~1/x.png")]
    [InlineData("lessons/x%25.png")]
    public async Task Invoke_UnsafeKey_Returns404(string key)
    {
        var context = await InvokeAsync($"/api/media/{key}");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invoke_MissingFile_Returns404()
    {
        _fileStorage.OpenReadAsync("lessons/missing.png", Arg.Any<CancellationToken>()).Returns((StoredFile?)null);

        var context = await InvokeAsync("/api/media/lessons/missing.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Invoke_OutsideMediaPath_CallsNext()
    {
        await InvokeAsync("/api/subjects");

        _nextCalled.Should().BeTrue();
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private async Task<HttpContext> InvokeAsync(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        var middleware = new PublicMediaMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        }, new PathString("/api/media"));
        await middleware.InvokeAsync(context, _fileStorage);
        return context;
    }
}
