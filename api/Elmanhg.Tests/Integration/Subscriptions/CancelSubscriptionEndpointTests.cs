using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class CancelSubscriptionEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PostAsync(CancelPath(Guid.NewGuid()), null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.PostAsync(CancelPath(Guid.NewGuid()), null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_OwnActiveBase_Returns200AndKeepsAccessUntilPeriodEnd()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var subscription = await SeedActiveBaseAsync(student.Id);

        using var response = await client.PostAsync(CancelPath(subscription.Id), null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var line = body.GetProperty("subscriptions")[0];
        (body.GetProperty("tier").GetString(), line.GetProperty("status").GetString()).Should().Be(("Base", "Cancelled"));
        line.GetProperty("entitledUntil").GetDateTimeOffset().Should().Be(line.GetProperty("currentPeriodEnd").GetDateTimeOffset());
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions.AsNoTracking().SingleAsync(x => x.Id == subscription.Id, CancellationToken);
        (stored.Status, stored.CancelledAt.HasValue).Should().Be((SubscriptionStatus.Cancelled, true));
    }

    [Fact]
    public async Task Post_OtherStudentsSubscription_Returns404()
    {
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var subscription = await SeedActiveBaseAsync(other.Id);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsync(CancelPath(subscription.Id), null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("SUBSCRIPTION_NOT_FOUND");
    }

    [Fact]
    public async Task Post_AlreadyCancelled_Returns400SubscriptionEnded()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var subscription = await SeedActiveBaseAsync(student.Id);
        using var first = await client.PostAsync(CancelPath(subscription.Id), null, CancellationToken);

        using var response = await client.PostAsync(CancelPath(subscription.Id), null, CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("SUBSCRIPTION_ENDED");
    }

    private async Task<Subscription> SeedActiveBaseAsync(Guid studentId)
    {
        var subscription = new SubscriptionBuilder().ForStudent(studentId).StartingAt(DateTimeOffset.UtcNow.AddDays(-5)).Build();
        await SubscriptionTestData.SeedSubscriptionAsync(factory, subscription, CancellationToken).ConfigureAwait(false);
        return subscription;
    }

    private static string CancelPath(Guid subscriptionId) => $"{SubscriptionTestData.SubscriptionsRoute}/{subscriptionId}/cancel";
}
