using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class PaymentMetricsEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset PaidAt = new(2021, 5, 5, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_SeededDay_SumsRevenueFailuresAndRefunds()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        await SeedPaymentAsync(factory, student.Id, BillingPeriod.Monthly, 1, 19900, PaymentStatus.Succeeded, PaidAt);
        await SeedPaymentAsync(factory, student.Id, BillingPeriod.Monthly, 1, 19900, PaymentStatus.Failed, PaidAt);
        await SeedPaymentAsync(factory, student.Id, BillingPeriod.Termly, 4, 69900, PaymentStatus.Succeeded, PaidAt, PaidAt.AddHours(2));
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, "payments?from=2021-05-05&to=2021-05-05");

        body.GetProperty("succeeded").GetInt32().Should().Be(2);
        body.GetProperty("failed").GetInt32().Should().Be(1);
        body.GetProperty("refunds").GetInt32().Should().Be(1);
        body.GetProperty("revenue").GetProperty("amountMinor").GetInt64().Should().Be(89800);
        body.GetProperty("refunded").GetProperty("amountMinor").GetInt64().Should().Be(69900);
        body.GetProperty("netRevenue").GetProperty("amountMinor").GetInt64().Should().Be(19900);
        body.GetProperty("revenueByDay").EnumerateArray().Should().ContainSingle().Which.GetProperty("value").GetInt64().Should().Be(89800);
    }
}
