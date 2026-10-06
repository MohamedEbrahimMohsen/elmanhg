using Core.Storage;
using Core.Storage.Media;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Core.Storage;

public sealed class StoredFileResponseExtensionsTests
{
    [Fact]
    public async Task WriteStoredFileAsync_File_WritesBodyTypeLengthAndHeaders()
    {
        byte[] bytes = [0x1A, 0x45, 0xDF];
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await using var file = new StoredFile(new MemoryStream(bytes), bytes.Length, "audio/webm");

        await context.Response.WriteStoredFileAsync(file, MediaCacheControl.PrivateNoStore);

        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(bytes);
        (context.Response.ContentType, context.Response.ContentLength).Should().Be(("audio/webm", (long?)bytes.Length));
        context.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        context.Response.Headers.CacheControl.ToString().Should().Be("private, no-store");
    }

    [Fact]
    public void SetMediaHeaders_CacheControl_SetsNosniffAndCacheControl()
    {
        var context = new DefaultHttpContext();

        context.Response.SetMediaHeaders(MediaCacheControl.PublicImmutable);

        context.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
        context.Response.Headers.CacheControl.ToString().Should().Be("public, max-age=31536000, immutable");
    }
}
