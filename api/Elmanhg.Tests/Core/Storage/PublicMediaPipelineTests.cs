using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Core.Storage;

public sealed class PublicMediaPipelineTests : PublicMediaPipelineTestBase
{
    [Fact]
    public async Task UseCorePublicMedia_Local_ServesPublicFileWithImmutableCache()
    {
        WriteLocalFile("lessons", "a.png");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(Bytes);
        context.Response.Headers.CacheControl.ToString().Should().Be("public, max-age=31536000, immutable");
        context.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task UseCorePublicMedia_Local_PrivateFolder_Returns404()
    {
        WriteLocalFile("teacher-threads", "x.png");

        var context = await SendAsync(LocalOptions(), "/api/media/teacher-threads/x.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ((MemoryStream)context.Response.Body).Length.Should().Be(0);
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalSvgKey_ServesOctetStreamWithNosniff()
    {
        WriteLocalFile("lessons", "a.svg");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.svg");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/octet-stream");
        context.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalHtmlKey_ServesOctetStream()
    {
        WriteLocalFile("lessons", "a.html");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.html");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalWebmKey_ServesAudioWebm()
    {
        WriteLocalFile("lessons", "a.webm");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.webm");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("audio/webm");
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalRootWithTrailingSlash_ServesPublicFile()
    {
        WriteLocalFile("lessons", "a.png");

        var context = await SendAsync(LocalOptions("media/"), "/api/media/lessons/a.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(Bytes);
    }
}
