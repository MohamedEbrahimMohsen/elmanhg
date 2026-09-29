using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Payments;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class PaymentHistoryEndpointTests(ApiFactory factory)
{
    private const string PaymentsPath = $"{SubscriptionTestData.SubscriptionsRoute}/payments";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(PaymentsPath, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(PaymentsPath, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_StudentWithPayments_ReturnsOwnCompletedPaymentsNewestFirst()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var failed = await SubscriptionTestData.SeedCompletedPaymentAsync(factory, student.Id, succeeded: false, 19900, CancellationToken);
        var succeeded = await SubscriptionTestData.SeedCompletedPaymentAsync(factory, student.Id, succeeded: true, 69900, CancellationToken);
        await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, CancellationToken);
        await SubscriptionTestData.SeedCompletedPaymentAsync(factory, other.Id, succeeded: true, 19900, CancellationToken);

        using var response = await client.GetAsync(PaymentsPath, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("totalItems").GetInt64().Should().Be(2);
        body.GetProperty("items").EnumerateArray()
            .Select(x => (x.GetProperty("id").GetGuid(), x.GetProperty("status").GetString(), x.GetProperty("amount").GetProperty("amountMinor").GetInt64(), x.GetProperty("amount").GetProperty("currency").GetString()))
            .Should().Equal((succeeded.Id, "Succeeded", 69900L, "EGP"), (failed.Id, "Failed", 19900L, "EGP"));
    }

    [Fact]
    public async Task Get_StudentWithRefundedPayment_ListsRefundedStatus()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var payment = await SubscriptionTestData.SeedCompletedPaymentAsync(factory, student.Id, succeeded: true, 19900, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);
        using var refund = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", Guid.NewGuid()), CancellationToken);

        using var response = await client.GetAsync(PaymentsPath, CancellationToken);

        refund.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        (item.GetProperty("id").GetGuid(), item.GetProperty("status").GetString()).Should().Be((payment.Id, "Refunded"));
    }

    [Fact]
    public async Task Get_PageSizeAboveMax_Returns422WithCode()
    {
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await client.GetAsync($"{PaymentsPath}?pageSize=51", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Contain("PAYMENT_HISTORY_PAGE_SIZE_INVALID");
    }
}
