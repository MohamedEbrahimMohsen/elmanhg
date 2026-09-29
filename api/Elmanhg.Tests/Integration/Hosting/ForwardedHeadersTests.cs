using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace Elmanhg.Tests.Integration.Hosting;

public sealed class ForwardedHeadersTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_TrustedProxyForwardsDifferentClients_UsesSeparateRateLimitBuckets()
    {
        await using var host = CreateFactory();

        var first = await PostLoginAsync(host, "172.30.0.5", "203.0.113.10");
        var second = await PostLoginAsync(host, "172.30.0.5", "203.0.113.20");

        first.Should().NotBe(StatusCodes.Status429TooManyRequests);
        second.Should().NotBe(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Post_TrustedProxyForwardsSameClientTwice_Returns429()
    {
        await using var host = CreateFactory();

        var first = await PostLoginAsync(host, "172.30.0.6", "203.0.113.30");
        var second = await PostLoginAsync(host, "172.30.0.6", "203.0.113.30");

        first.Should().NotBe(StatusCodes.Status429TooManyRequests);
        second.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Post_UntrustedPeerSendsForwardedFor_IgnoresHeaderAndReturns429()
    {
        await using var host = CreateFactory();

        await PostLoginAsync(host, "198.51.100.7", "203.0.113.40");
        var second = await PostLoginAsync(host, "198.51.100.7", "203.0.113.50");

        second.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:CredentialPermitLimit"] = "1", ["ReverseProxy:TrustedNetworks:0"] = "172.30.0.0/24" })));
    }

    private static async Task<int> PostLoginAsync(WebApplicationFactory<Program> host, string peerAddress, string forwardedFor)
    {
        var httpContext = await host.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/api/auth/login/email";
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream("{}"u8.ToArray());
            context.Connection.RemoteIpAddress = IPAddress.Parse(peerAddress);
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return httpContext.Response.StatusCode;
    }
}
