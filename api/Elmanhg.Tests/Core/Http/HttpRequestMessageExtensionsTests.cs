using Core.Http;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Http;

public sealed class HttpRequestMessageExtensionsTests
{
    [Fact]
    public void WithUserAgent_Value_SetsHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "probe");

        request.WithUserAgent(CoreHttpTestSettings.UserAgent);

        request.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public void WithUserAgent_Blank_LeavesHeaderUnset()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "probe");

        request.WithUserAgent(" ");

        request.Headers.UserAgent.Should().BeEmpty();
    }
}
