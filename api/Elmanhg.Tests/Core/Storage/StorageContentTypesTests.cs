using Core.Storage;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Storage;

public sealed class StorageContentTypesTests
{
    [Theory]
    [InlineData("lessons/a.png", "image/png")]
    [InlineData("lessons/a.jpg", "image/jpeg")]
    [InlineData("lessons/a.jpeg", "image/jpeg")]
    [InlineData("lessons/a.webp", "image/webp")]
    [InlineData("lessons/a.gif", "image/gif")]
    [InlineData("teacher-threads/a.webm", "audio/webm")]
    [InlineData("teacher-threads/a.WEBM", "audio/webm")]
    [InlineData("teacher-threads/a.ogg", "audio/ogg")]
    [InlineData("teacher-threads/a.m4a", "audio/mp4")]
    [InlineData("teacher-threads/a.mp4", "audio/mp4")]
    [InlineData("training-exports/a.jsonl", "application/x-ndjson")]
    public void FromKey_KnownExtensions_MapsContentType(string key, string contentType)
    {
        StorageContentTypes.FromKey(key).Should().Be(contentType);
    }

    [Fact]
    public void FromKey_UnknownExtension_ReturnsFallback()
    {
        StorageContentTypes.FromKey("lessons/a.svg").Should().Be("application/octet-stream");
    }

    [Fact]
    public void FromKey_NoExtension_ReturnsFallback()
    {
        StorageContentTypes.FromKey("lessons/a").Should().Be("application/octet-stream");
    }
}
