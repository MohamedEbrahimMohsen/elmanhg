using Elmanhg.Api.Realtime;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Api.Realtime;

public sealed class HubAccessTokenTests
{
    [Fact]
    public async Task OnMessageReceived_HubPathWithAccessToken_SetsToken()
    {
        var context = Context("/api/hubs/notifications/negotiate", "?access_token=abc");

        await HubAccessToken.OnMessageReceived(context);

        context.Token.Should().Be("abc");
    }

    [Fact]
    public async Task OnMessageReceived_OtherPath_LeavesTokenUnset()
    {
        var context = Context("/api/teacher-threads", "?access_token=abc");

        await HubAccessToken.OnMessageReceived(context);

        context.Token.Should().BeNull();
    }

    [Fact]
    public async Task OnMessageReceived_HubPathWithoutToken_LeavesTokenUnset()
    {
        var context = Context("/api/hubs/notifications", "?id=connection");

        await HubAccessToken.OnMessageReceived(context);

        context.Token.Should().BeNull();
    }

    private static MessageReceivedContext Context(string path, string query)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString(query);
        return new MessageReceivedContext(httpContext, new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), new JwtBearerOptions());
    }
}
