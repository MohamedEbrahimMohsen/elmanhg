using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class PlanCatalogueEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_ReturnsConfiguredCatalogue()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(SubscriptionTestData.PlansRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("free").GetProperty("dailyQuizQuestions").GetInt32().Should().Be(10);
        body.GetProperty("base").GetProperty("prices").EnumerateArray()
            .Select(x => (x.GetProperty("period").GetString(), x.GetProperty("months").GetInt32(), x.GetProperty("price").GetProperty("amountMinor").GetInt64(), x.GetProperty("price").GetProperty("currency").GetString()))
            .Should().Equal(("Monthly", 1, 19900L, "EGP"), ("Termly", 4, 69900L, "EGP"), ("Yearly", 12, 179900L, "EGP"));
        var askTeacher = body.GetProperty("askTeacher");
        askTeacher.GetProperty("monthlyQuestions").GetInt32().Should().Be(20);
        askTeacher.GetProperty("prices")[0].GetProperty("price").GetProperty("amountMinor").GetInt64().Should().Be(9900);
    }

    [Fact]
    public async Task Get_SignedInStudent_Returns200()
    {
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await client.GetAsync(SubscriptionTestData.PlansRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
