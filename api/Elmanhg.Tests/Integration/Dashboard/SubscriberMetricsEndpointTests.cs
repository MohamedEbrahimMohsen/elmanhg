using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class SubscriberMetricsEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ActiveTermlySubscription_ContributesMonthlyRevenue()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        await SeedPaymentAsync(factory, student.Id, BillingPeriod.Termly, 4, 69900, PaymentStatus.Succeeded, TimeProvider.System.GetUtcNow().AddDays(-1));
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, "subscribers");

        body.GetProperty("activeByPlan").EnumerateArray().Select(x => x.GetProperty("plan").GetString()).Should().Equal("Base", "AskTeacher");
        body.GetProperty("activeSubscriptions").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var revenue = body.GetProperty("monthlyRecurringRevenue");
        revenue.GetProperty("currency").GetString().Should().Be("EGP");
        revenue.GetProperty("amountMinor").GetInt64().Should().BeGreaterThanOrEqualTo(17475);
    }
}
