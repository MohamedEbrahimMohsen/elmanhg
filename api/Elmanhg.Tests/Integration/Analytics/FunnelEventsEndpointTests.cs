using Elmanhg.Domain.Analytics;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Analytics;

public sealed class FunnelEventsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/analytics/funnel-events";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Anonymous_RecordsEventWithoutUser()
    {
        using var anonymous = AuthTestClient.Create(factory);
        var anonymousId = Guid.NewGuid();

        using var response = await anonymous.PostAsJsonAsync(Route, new { anonymousId, type = "LandingViewed" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await ReadEventsAsync(anonymousId);
        stored.Should().ContainSingle();
        stored[0].Type.Should().Be(FunnelEventType.LandingViewed);
        stored[0].UserId.Should().BeNull();
    }

    [Fact]
    public async Task Post_SignedInStudent_RecordsEventWithUserId()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        var anonymousId = Guid.NewGuid();

        using var response = await client.PostAsJsonAsync(Route, new { anonymousId, type = "OnboardingCompleted" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadEventsAsync(anonymousId)).Should().ContainSingle().Which.UserId.Should().Be(student.Id);
    }

    [Fact]
    public async Task Post_EmptyAnonymousId_Returns422()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PostAsJsonAsync(Route, new { anonymousId = Guid.Empty, type = "LandingViewed" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("FUNNEL_ANONYMOUS_ID_REQUIRED");
        (await ReadEventsAsync(Guid.Empty)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_UnknownNumericType_Returns422()
    {
        using var anonymous = AuthTestClient.Create(factory);
        var anonymousId = Guid.NewGuid();

        using var response = await anonymous.PostAsJsonAsync(Route, new { anonymousId, type = 99 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("FUNNEL_EVENT_TYPE_INVALID");
        (await ReadEventsAsync(anonymousId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_OverIpLimit_Returns429TooManyRequests()
    {
        await using var limitedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Analytics:FunnelEventPermitLimit"] = "1" })));
        using var client = limitedFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var first = await client.PostAsJsonAsync(Route, new { anonymousId = Guid.NewGuid(), type = "LandingViewed" }, CancellationToken);

        using var response = await client.PostAsJsonAsync(Route, new { anonymousId = Guid.NewGuid(), type = "LandingViewed" }, CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(response)).Should().Be("TOO_MANY_REQUESTS");
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }

    private async Task<List<FunnelEvent>> ReadEventsAsync(Guid anonymousId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.FunnelEvents.AsNoTracking().Where(x => x.AnonymousId == anonymousId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
