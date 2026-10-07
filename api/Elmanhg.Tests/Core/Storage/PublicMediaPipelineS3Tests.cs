using Core.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Elmanhg.Tests.Core.Storage;

public sealed class PublicMediaPipelineS3Tests : PublicMediaPipelineTestBase
{
    [Fact]
    public async Task UseCorePublicMedia_S3_PrivateFolder_Returns404WithoutReading()
    {
        var context = await SendAsync(S3Options(), "/api/media/teacher-threads/x.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UseCorePublicMedia_S3_PublicKey_StreamsFromStorage()
    {
        _fileStorage.OpenReadAsync("lessons/a.png", Arg.Any<CancellationToken>()).Returns(new StoredFile(new MemoryStream(Bytes), Bytes.Length, "image/png"));

        var context = await SendAsync(S3Options(), "/api/media/lessons/a.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(Bytes);
    }
}
