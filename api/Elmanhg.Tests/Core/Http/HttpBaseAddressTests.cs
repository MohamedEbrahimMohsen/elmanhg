using Core.Http;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Http;

public sealed class HttpBaseAddressTests
{
    [Fact]
    public void From_UrlWithoutTrailingSlash_AppendsSlash()
    {
        HttpBaseAddress.From("https://api.resend.com").Should().Be(new Uri("https://api.resend.com/"));
    }

    [Fact]
    public void From_UrlWithTrailingSlashes_KeepsOneSlash()
    {
        HttpBaseAddress.From("http://ai.test//").Should().Be(new Uri("http://ai.test/"));
    }

    [Fact]
    public void From_UrlWithPath_KeepsPath()
    {
        HttpBaseAddress.From("https://x.test/v1").Should().Be(new Uri("https://x.test/v1/"));
    }

    [Fact]
    public void Combine_BaseWithTrailingSlash_JoinsWithOneSlash()
    {
        HttpBaseAddress.Combine("https://site.test/teacher/thread/", "abc").Should().Be("https://site.test/teacher/thread/abc");
    }

    [Fact]
    public void Combine_RelativeBase_JoinsWithOneSlash()
    {
        HttpBaseAddress.Combine("/student/fake-checkout", "p1").Should().Be("/student/fake-checkout/p1");
    }
}
