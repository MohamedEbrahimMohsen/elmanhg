using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Subscriptions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.RateLimiting;

public sealed class AnonymousRateLimitTests(ApiFactory factory)
{
    [Fact]
    public async Task Refresh_OverIpLimit_Returns429TooManyRequests()
    {
        await using var host = LimitedHost("RateLimiting:AuthRefreshPermitLimit");
        using var client = Client(host);
        using var first = await PostRefreshAsync(client);

        using var second = await PostRefreshAsync(client);

        first.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(second)).Should().Be("TOO_MANY_REQUESTS");
    }

    [Fact]
    public async Task ServableCount_OverIpLimit_Returns429TooManyRequests()
    {
        await using var host = LimitedHost("RateLimiting:PublicReadPermitLimit");
        using var client = Client(host);
        using var first = await client.GetAsync("/api/questions/servable-count", TestContext.Current.CancellationToken);

        using var second = await client.GetAsync("/api/questions/servable-count", TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(second)).Should().Be("TOO_MANY_REQUESTS");
    }

    [Fact]
    public async Task Plans_AfterServableCountUsedTheSharedBucket_Returns429()
    {
        await using var host = LimitedHost("RateLimiting:PublicReadPermitLimit");
        using var client = Client(host);
        using var first = await client.GetAsync("/api/questions/servable-count", TestContext.Current.CancellationToken);

        using var plans = await client.GetAsync("/api/plans", TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        plans.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task PaymobWebhook_OverIpLimit_Returns429TooManyRequests()
    {
        await using var host = LimitedHost("RateLimiting:PaymentWebhookPermitLimit");
        using var client = Client(host);
        using var first = await client.PostAsJsonAsync(SubscriptionTestData.WebhookRoute, new { }, TestContext.Current.CancellationToken);

        using var second = await client.PostAsJsonAsync(SubscriptionTestData.WebhookRoute, new { }, TestContext.Current.CancellationToken);

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(second)).Should().Be("TOO_MANY_REQUESTS");
    }

    private WebApplicationFactory<Program> LimitedHost(string permitLimitKey) => factory.WithWebHostBuilder(builder => builder.UseSetting(permitLimitKey, "1"));

    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static async Task<HttpResponseMessage> PostRefreshAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "elmanhg_refresh=tampered-value");
        return await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
