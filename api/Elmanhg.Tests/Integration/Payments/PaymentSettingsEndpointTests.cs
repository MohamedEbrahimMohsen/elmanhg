using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Configuration;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Payments;

[Collection(RuntimeSettingsCollection.Name)]
public sealed class PaymentSettingsEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SettingsRoute = $"{PaymentsTestData.Route}/settings";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await ConfigurationTestData.ClearOverridesAsync(factory);

    public async ValueTask DisposeAsync() => await ConfigurationTestData.ClearOverridesAsync(factory);

    [Fact]
    public async Task Get_FlagNeverSet_ReturnsRefundsDisabled()
    {
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.GetAsync(SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RefundsEnabledAsync(response)).Should().BeFalse();
    }

    [Fact]
    public async Task Get_AfterAdminTurnsRefundsOn_ReturnsRefundsEnabled()
    {
        await PaymentsTestData.SetRefundsEnabledAsync(factory, true, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.GetAsync(SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RefundsEnabledAsync(response)).Should().BeTrue();
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync(SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetConfigurationSettings_RefundsFlag_ListedUnderFeaturesAndOff()
    {
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.GetAsync(ConfigurationTestData.SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var features = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).EnumerateArray().Single(x => x.GetProperty("group").GetString() == "Features");
        var refunds = features.GetProperty("settings").EnumerateArray().Single(x => x.GetProperty("key").GetString() == PaymentsTestData.RefundsEnabledKey);
        (refunds.GetProperty("value").GetBoolean(), refunds.GetProperty("defaultValue").GetBoolean(), refunds.GetProperty("isOverridden").GetBoolean()).Should().Be((false, false, false));
    }

    private static async Task<bool> RefundsEnabledAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("refundsEnabled").GetBoolean();
}
