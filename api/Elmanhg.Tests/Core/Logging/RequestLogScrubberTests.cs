using Core.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace Elmanhg.Tests.Core.Logging;

public sealed class RequestLogScrubberTests
{
    [Fact]
    public void Query_PaymobHmac_KeepsKeyAndDropsValue()
    {
        var request = RequestWithQuery("?hmac=4f1c9a0b7e2d");

        RequestLogScrubber.Query(request).Should().Be("?hmac=[redacted]");
    }

    [Fact]
    public void Query_UrlEncodedEmail_KeepsKeyAndDropsValue()
    {
        var request = RequestWithQuery("?actor=mona%40example.com&page=2");

        RequestLogScrubber.Query(request).Should().Be("?actor=[redacted]&page=[redacted]");
    }

    [Fact]
    public void Query_NoQuery_ReturnsEmpty()
    {
        RequestLogScrubber.Query(RequestWithQuery(string.Empty)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("203.0.113.77", "203.0.113.0/24")]
    [InlineData("2001:db8:abcd:12:1:2:3:4", "2001:db8:abcd::/48")]
    [InlineData("::ffff:198.51.100.9", "198.51.100.0/24")]
    [InlineData("not-an-ip", "")]
    public void Ip_Address_TruncatesToPrefix(string value, string expected)
    {
        RequestLogScrubber.Ip(value).Should().Be(expected);
    }

    [Fact]
    public void Ip_NullAddress_ReturnsEmpty()
    {
        RequestLogScrubber.Ip((IPAddress?)null).Should().BeEmpty();
    }

    [Fact]
    public void IpList_ForwardedChain_TruncatesEachHop()
    {
        RequestLogScrubber.IpList("203.0.113.77, 198.51.100.9").Should().Be("203.0.113.0/24, 198.51.100.0/24");
    }

    private static HttpRequest RequestWithQuery(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }
}
