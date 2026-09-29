using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Fixtures.Paymob;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Payments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class PaymobRefundWebhookEndpointTests(ApiFactory factory)
{
    private const long OrderId = 217503754;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_SignedFullRefund_MarksRefundedAndStudentBecomesFree()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow.AddDays(-1), null, CancellationToken);
        var refundId = NewTransactionId();

        using var response = await PostSignedAsync(RefundPayload(payment, refundId, 19900));

        (response.StatusCode, await OutcomeAsync(response)).Should().Be((HttpStatusCode.OK, "Refunded"));
        var stored = await ReadAsync(payment.Id);
        (stored.Status, stored.RefundTransactionId, stored.RefundedBy).Should().Be((PaymentStatus.Refunded, refundId.ToString(), (Guid?)null));
        (await GetTierAsync(client)).Should().Be("Free");
    }

    [Fact]
    public async Task Post_SameRefundTwice_SecondIsDuplicate()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        var payload = RefundPayload(payment, NewTransactionId(), 19900);

        using var first = await PostSignedAsync(payload);
        using var second = await PostSignedAsync(payload);

        (await OutcomeAsync(first), await OutcomeAsync(second)).Should().Be(("Refunded", "Duplicate"));
    }

    [Fact]
    public async Task Post_SignedPartialRefund_FlagsForReviewAndKeepsAccess()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow.AddDays(-1), null, CancellationToken);

        using var response = await PostSignedAsync(RefundPayload(payment, NewTransactionId(), 5000));

        (response.StatusCode, await OutcomeAsync(response)).Should().Be((HttpStatusCode.OK, "FlaggedForReview"));
        var stored = await ReadAsync(payment.Id);
        (stored.Status, stored.ReviewReason).Should().Be((PaymentStatus.Succeeded, (PaymentReviewReason?)PaymentReviewReason.PartialRefundAtProvider));
        (await GetTierAsync(client)).Should().Be("Base");
    }

    [Fact]
    public async Task Post_RefundBeforeSettlement_Returns409NotSettled()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, 19900, CancellationToken);

        using var response = await PostSignedAsync(RefundPayload(payment, NewTransactionId(), 19900));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("PAYMENT_NOT_SETTLED");
        (await ReadAsync(payment.Id)).Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Post_UnsignedRefundFlagOnCharge_SettlesAsCharge()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, 19900, CancellationToken);
        var root = JsonNode.Parse(PaymobPayloads.ForPayment(PaymobPayloads.Succeeded, payment.Id, NewTransactionId(), 19900, OrderId))!;
        root["obj"]!["is_refund"] = true;

        using var response = await PostSignedAsync(root.ToJsonString());

        (response.StatusCode, await OutcomeAsync(response)).Should().Be((HttpStatusCode.OK, "Succeeded"));
        (await ReadAsync(payment.Id)).Status.Should().Be(PaymentStatus.Succeeded);
    }

    private static string RefundPayload(Payment payment, long refundId, long amountMinor) => PaymobPayloads.ForPayment(PaymobPayloads.RefundChild, payment.Id, refundId, amountMinor, OrderId);

    private async Task<HttpResponseMessage> PostSignedAsync(string payload)
    {
        using var client = AuthTestClient.Create(factory);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        return await client.PostAsync($"{SubscriptionTestData.WebhookRoute}?hmac={PaymobPayloads.Sign(payload, ApiFactory.TestPaymobHmacSecret)}", content, CancellationToken).ConfigureAwait(false);
    }

    private async Task<Payment> ReadAsync(Guid paymentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Payments.AsNoTracking().SingleAsync(x => x.Id == paymentId, CancellationToken).ConfigureAwait(false);
    }

    private static long NewTransactionId() => Random.Shared.NextInt64(1_000_000_000, long.MaxValue);

    private static async Task<string?> OutcomeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("outcome").GetString();

    private static async Task<string?> GetTierAsync(HttpClient client)
    {
        using var response = await client.GetAsync($"{SubscriptionTestData.SubscriptionsRoute}/entitlement", CancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("tier").GetString();
    }
}
