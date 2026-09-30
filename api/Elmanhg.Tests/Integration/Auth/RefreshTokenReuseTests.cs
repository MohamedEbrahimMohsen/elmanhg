using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class RefreshTokenReuseTests(ApiFactory factory)
{
    private const string RefreshRoute = "/api/auth/refresh";

    [Fact]
    public async Task Refresh_RotatedCookieReplayed_Returns401AndRevokesTheWholeFamily()
    {
        await using var strictHost = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:RefreshTokenReuseGraceSeconds"] = "0" })));
        using var client = strictHost.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var registered = await AuthTestClient.RegisterByPhoneAsync(client, factory, AuthTestClient.NewPhoneNumber(), TestContext.Current.CancellationToken);
        var signInCookie = AuthTestClient.ReadRefreshCookie(registered);
        using var rotated = await SendRefreshAsync(client, signInCookie);
        var successorCookie = AuthTestClient.ReadRefreshCookie(rotated);

        using var replay = await SendRefreshAsync(client, signInCookie);

        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadCodeAsync(replay)).Should().Be("REFRESH_TOKEN_REVOKED");
        using var successor = await SendRefreshAsync(client, successorCookie);
        successor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadCodeAsync(successor)).Should().Be("REFRESH_TOKEN_REVOKED");
    }

    [Fact]
    public async Task Refresh_RotatedCookieReplayedWithinGrace_Returns200()
    {
        using var client = AuthTestClient.Create(factory);
        using var registered = await AuthTestClient.RegisterByPhoneAsync(client, factory, AuthTestClient.NewPhoneNumber(), TestContext.Current.CancellationToken);
        var signInCookie = AuthTestClient.ReadRefreshCookie(registered);
        using var firstTab = await SendRefreshAsync(client, signInCookie);

        using var secondTab = await SendRefreshAsync(client, signInCookie);

        firstTab.StatusCode.Should().Be(HttpStatusCode.OK);
        secondTab.StatusCode.Should().Be(HttpStatusCode.OK);
        using var next = await SendRefreshAsync(client, AuthTestClient.ReadRefreshCookie(secondTab));
        next.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ReplayInOneSignIn_LeavesOtherSignInValid()
    {
        await using var strictHost = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:RefreshTokenReuseGraceSeconds"] = "0" })));
        using var client = strictHost.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);
        using var firstSignIn = await client.PostAsJsonAsync("/api/auth/login/email", new { email = student.Email, password = ScopeTestData.Password }, TestContext.Current.CancellationToken);
        using var otherSignIn = await client.PostAsJsonAsync("/api/auth/login/email", new { email = student.Email, password = ScopeTestData.Password }, TestContext.Current.CancellationToken);
        var stolenCookie = AuthTestClient.ReadRefreshCookie(firstSignIn);
        using var rotated = await SendRefreshAsync(client, stolenCookie);

        using var replay = await SendRefreshAsync(client, stolenCookie);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var other = await SendRefreshAsync(client, AuthTestClient.ReadRefreshCookie(otherSignIn));
        other.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RefreshRoute);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
