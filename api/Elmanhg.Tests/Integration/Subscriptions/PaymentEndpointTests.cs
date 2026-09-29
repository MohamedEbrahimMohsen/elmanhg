using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class PaymentEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(PaymentPath(Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_OwnPendingPayment_Returns200WithPendingStatus()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var payment = await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, 19900, CancellationToken);

        using var response = await client.GetAsync(PaymentPath(payment.Id), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("id").GetGuid(), body.GetProperty("status").GetString()).Should().Be((payment.Id, "Pending"));
    }

    [Fact]
    public async Task Get_OtherStudentsPayment_Returns404PaymentNotFound()
    {
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SubscriptionTestData.SeedPendingPaymentAsync(factory, other.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, 19900, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync(PaymentPath(payment.Id), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("PAYMENT_NOT_FOUND");
    }

    private static string PaymentPath(Guid paymentId) => $"{SubscriptionTestData.SubscriptionsRoute}/payments/{paymentId}";
}
