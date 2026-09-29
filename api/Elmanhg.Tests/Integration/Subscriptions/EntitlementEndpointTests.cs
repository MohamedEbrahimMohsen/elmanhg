using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class EntitlementEndpointTests(ApiFactory factory)
{
    private const string EntitlementPath = $"{SubscriptionTestData.SubscriptionsRoute}/entitlement";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(EntitlementPath, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(EntitlementPath, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_NewStudent_ReturnsFree()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await GetEntitlementAsync(client);

        body.GetProperty("tier").GetString().Should().Be("Free");
        body.GetProperty("dailyQuizQuestionLimit").GetInt32().Should().Be(10);
        body.GetProperty("subscriptions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Get_ActiveBaseAndAskTeacher_ReturnsBaseWithAskTeacher()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var start = DateTimeOffset.UtcNow.AddDays(-5);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(student.Id).StartingAt(start).Build(), CancellationToken);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(student.Id).WithPlan(SubscriptionPlan.AskTeacher).StartingAt(start).Build(), CancellationToken);

        var body = await GetEntitlementAsync(client);

        (body.GetProperty("tier").GetString(), body.GetProperty("hasAskTeacher").GetBoolean(), body.GetProperty("dailyQuizQuestionLimit").ValueKind).Should().Be(("Base", true, JsonValueKind.Null));
        body.GetProperty("monthlyAskTeacherQuestionLimit").GetInt32().Should().Be(20);
        body.GetProperty("subscriptions").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Get_BaseLapsedBeyondGrace_ReturnsFree()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(student.Id).StartingAt(DateTimeOffset.UtcNow.AddMonths(-2)).Build(), CancellationToken);

        var body = await GetEntitlementAsync(client);

        body.GetProperty("tier").GetString().Should().Be("Free");
    }

    [Fact]
    public async Task Get_OtherStudentsSubscription_IsNotCounted()
    {
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(other.Id).StartingAt(DateTimeOffset.UtcNow.AddDays(-5)).Build(), CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await GetEntitlementAsync(client);

        body.GetProperty("tier").GetString().Should().Be("Free");
    }

    private static async Task<JsonElement> GetEntitlementAsync(HttpClient client)
    {
        using var response = await client.GetAsync(EntitlementPath, CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }
}
