using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class SessionEndpointTests(ApiFactory factory)
{
    private const string RefreshRoute = "/api/auth/refresh";
    private const string LogoutRoute = "/api/auth/logout";

    [Fact]
    public async Task Refresh_ValidCookie_Returns200AndRotatesCookie()
    {
        using var client = AuthTestClient.Create(factory);
        using var registered = await AuthTestClient.RegisterByPhoneAsync(client, factory, AuthTestClient.NewPhoneNumber(), TestContext.Current.CancellationToken);
        var cookie = AuthTestClient.ReadRefreshCookie(registered);

        using var response = await SendRefreshAsync(client, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        AuthTestClient.ReadRefreshCookie(response).Should().NotBe(cookie);
    }

    [Fact]
    public async Task Refresh_NoCookie_Returns422()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsync(RefreshRoute, null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("REFRESH_TOKEN_IS_REQUIRED");
    }

    [Fact]
    public async Task Refresh_TamperedCookie_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await SendRefreshAsync(client, $"{AuthTestClient.RefreshCookieName}=tampered-value");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadCodeAsync(response)).Should().Be("REFRESH_TOKEN_IS_EXPIRED");
    }

    [Fact]
    public async Task Logout_Authenticated_Returns200AndRevokesRefreshToken()
    {
        using var client = AuthTestClient.Create(factory);
        using var registered = await AuthTestClient.RegisterByPhoneAsync(client, factory, AuthTestClient.NewPhoneNumber(), TestContext.Current.CancellationToken);
        var cookie = AuthTestClient.ReadRefreshCookie(registered);
        var accessToken = (await registered.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("accessToken").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, LogoutRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleted = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith($"{AuthTestClient.RefreshCookieName}=", StringComparison.Ordinal));
        deleted.Should().Contain("expires=Thu, 01 Jan 1970");
        using var refresh = await SendRefreshAsync(client, cookie);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadCodeAsync(refresh)).Should().Be("REFRESH_TOKEN_USER_NOT_FOUND");
    }

    [Fact]
    public async Task Logout_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsync(LogoutRoute, null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ThenOldAccessToken_Returns401()
    {
        using var client = AuthTestClient.Create(factory);
        using var registered = await AuthTestClient.RegisterByPhoneAsync(client, factory, AuthTestClient.NewPhoneNumber(), TestContext.Current.CancellationToken);
        var accessToken = (await registered.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("accessToken").GetString();
        using var logout = await SendLogoutAsync(client, accessToken);

        using var response = await SendLogoutAsync(client, accessToken);

        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> SendLogoutAsync(HttpClient client, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LogoutRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
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
