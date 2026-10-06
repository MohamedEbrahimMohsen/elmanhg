using Core.Storage;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Storage;

public sealed class FileStorageOptionsTests
{
    [Theory]
    [InlineData("/api/media")]
    [InlineData("/api/media/")]
    public void GetPublicUrl_BaseWithOrWithoutTrailingSlash_JoinsWithOneSlash(string publicBaseUrl)
    {
        var options = new FileStorageOptions { PublicBaseUrl = publicBaseUrl };

        options.GetPublicUrl("lessons/a.png").Should().Be("/api/media/lessons/a.png");
    }

    [Fact]
    public void GetPublicUrl_EmptyKey_ReturnsPrefixWithTrailingSlash()
    {
        var options = new FileStorageOptions { PublicBaseUrl = "/api/media" };

        options.GetPublicUrl(string.Empty).Should().Be("/api/media/");
    }

    [Fact]
    public void ResolveLocalRoot_RelativePath_ResolvesAgainstContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "x");
        var options = new FileStorageOptions { LocalRootPath = "media" };

        options.ResolveLocalRoot(contentRoot).Should().Be(Path.GetFullPath(Path.Combine(contentRoot, "media")));
    }
}
