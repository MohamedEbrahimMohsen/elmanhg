using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Fixtures.Paymob;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class PaymobWebhookEndpointTests(ApiFactory factory)
{
    private const long OrderId = 217503754;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_SignedSuccess_ActivatesBaseAndStoresRawPayload()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await PostSignedAsync(PaymobPayloads.Succeeded, payment);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await OutcomeAsync(response)).Should().Be("Succeeded");
        (await GetTierAsync(client)).Should().Be("Base");
        var (stored, subscriptions) = await ReadAsync(payment.Id, student.Id);
        (stored.Status, stored.SubscriptionId).Should().Be((PaymentStatus.Succeeded, (Guid?)subscriptions.Single().Id));
        stored.RawWebhook.Should().Contain("\"merchant_order_id\"");
        subscriptions.Single().Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Post_SignedDeclined_MarksFailedAndStudentStaysFree()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await PostSignedAsync(PaymobPayloads.Declined, payment);

        (response.StatusCode, await OutcomeAsync(response)).Should().Be((HttpStatusCode.OK, "MarkedFailed"));
        (await GetTierAsync(client)).Should().Be("Free");
        var (stored, subscriptions) = await ReadAsync(payment.Id, student.Id);
        (stored.Status, subscriptions.Count).Should().Be((PaymentStatus.Failed, 0));
    }

    [Fact]
    public async Task Post_SameTransactionTwice_SecondIsDuplicate()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);
        var transactionId = NewTransactionId();

        using var first = await PostSignedAsync(PaymobPayloads.Succeeded, payment, transactionId);
        using var second = await PostSignedAsync(PaymobPayloads.Succeeded, payment, transactionId);

        (first.StatusCode, second.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        (await OutcomeAsync(first), await OutcomeAsync(second)).Should().Be(("Succeeded", "Duplicate"));
        (await ReadAsync(payment.Id, student.Id)).Subscriptions.Should().ContainSingle();
    }

    [Fact]
    public async Task Post_SuccessAfterDeclinedAttempt_ActivatesBase()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);
        var approvedTransaction = NewTransactionId();
        using var declined = await PostSignedAsync(PaymobPayloads.Declined, payment);

        using var response = await PostSignedAsync(PaymobPayloads.Succeeded, payment, approvedTransaction);

        (await OutcomeAsync(declined), await OutcomeAsync(response)).Should().Be(("MarkedFailed", "Succeeded"));
        var (stored, subscriptions) = await ReadAsync(payment.Id, student.Id);
        (stored.Status, stored.PaymobTransactionId, subscriptions.Count).Should().Be((PaymentStatus.Succeeded, approvedTransaction.ToString(), 1));
    }

    [Fact]
    public async Task Post_DeclinedAfterSuccess_KeepsSucceeded()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);
        using var succeeded = await PostSignedAsync(PaymobPayloads.Succeeded, payment);

        using var response = await PostSignedAsync(PaymobPayloads.Declined, payment);

        (response.StatusCode, await OutcomeAsync(response)).Should().Be((HttpStatusCode.OK, "OutOfOrder"));
        (await ReadAsync(payment.Id, student.Id)).Payment.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task Post_SuccessWhileBaseHeld_ExtendsExistingSubscription()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var held = new SubscriptionBuilder().ForStudent(student.Id).StartingAt(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(-20).ToUnixTimeSeconds())).Build();
        var previousEnd = held.CurrentPeriodEnd;
        await SubscriptionTestData.SeedSubscriptionAsync(factory, held, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await PostSignedAsync(PaymobPayloads.Succeeded, payment);

        (await OutcomeAsync(response)).Should().Be("Succeeded");
        var (stored, subscriptions) = await ReadAsync(payment.Id, student.Id);
        var subscription = subscriptions.Should().ContainSingle().Subject;
        (subscription.Id, subscription.CurrentPeriodEnd, stored.SubscriptionId).Should().Be((held.Id, previousEnd.AddMonths(1), (Guid?)held.Id));
    }

    [Fact]
    public async Task Post_AskTeacherWithoutBase_SucceedsAndFlagsForReview()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, SubscriptionPlan.AskTeacher, BillingPeriod.Monthly, 9900, CancellationToken);

        using var response = await PostSignedAsync(PaymobPayloads.Succeeded, payment);

        (await OutcomeAsync(response)).Should().Be("Succeeded");
        var stored = (await ReadAsync(payment.Id, student.Id)).Payment;
        (stored.Status, stored.ReviewReason).Should().Be((PaymentStatus.Succeeded, (PaymentReviewReason?)PaymentReviewReason.AskTeacherWithoutBase));
    }

    [Fact]
    public async Task Post_InvalidSignature_Returns401AndChangesNothing()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);
        var payload = Payload(PaymobPayloads.Succeeded, payment, NewTransactionId());

        using var response = await PostAsync(payload, PaymobPayloads.Sign(payload, "not-the-secret"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeAsync(response)).Should().Be("PAYMOB_WEBHOOK_SIGNATURE_INVALID");
        (await ReadAsync(payment.Id, student.Id)).Payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Post_MissingSignature_Returns401()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await PostAsync(Payload(PaymobPayloads.Succeeded, payment, NewTransactionId()), null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeAsync(response)).Should().Be("PAYMOB_WEBHOOK_SIGNATURE_INVALID");
    }

    [Fact]
    public async Task Post_TokenCallback_Returns200Ignored()
    {
        using var response = await PostAsync(PaymobPayloads.Read(PaymobPayloads.Token), null);

        (response.StatusCode, await OutcomeAsync(response)).Should().Be((HttpStatusCode.OK, "Ignored"));
    }

    [Fact]
    public async Task Post_PendingTransaction_LeavesPaymentPending()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await PostSignedAsync(PaymobPayloads.Pending, payment);

        (response.StatusCode, await OutcomeAsync(response)).Should().Be((HttpStatusCode.OK, "Ignored"));
        (await ReadAsync(payment.Id, student.Id)).Payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Post_AmountMismatch_Returns400AndLeavesPending()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);
        var payload = PaymobPayloads.ForPayment(PaymobPayloads.Succeeded, payment.Id, NewTransactionId(), 100, OrderId);

        using var response = await PostAsync(payload, PaymobPayloads.Sign(payload, ApiFactory.TestPaymobHmacSecret));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).Should().Be("PAYMENT_NOTIFICATION_MISMATCH");
        (await ReadAsync(payment.Id, student.Id)).Payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Post_UnknownMerchantOrder_Returns404()
    {
        var payload = PaymobPayloads.ForPayment(PaymobPayloads.Succeeded, Guid.NewGuid(), NewTransactionId(), 19900, OrderId);

        using var response = await PostAsync(payload, PaymobPayloads.Sign(payload, ApiFactory.TestPaymobHmacSecret));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be("PAYMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Post_SignedSuccess_AuditDiffExcludesRawWebhook()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await PostSignedAsync(PaymobPayloads.Succeeded, payment);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var audit = await ContentTestData.ReadAuditAsync(factory, "Payment.ProcessNotification", payment.Id, CancellationToken);
        audit.Diff.Should().Contain("\"status\"").And.NotContain("rawWebhook").And.NotContain("mona@example.test");
    }

    private Task<Payment> SeedPendingAsync(Guid studentId) => SubscriptionTestData.SeedPendingPaymentAsync(factory, studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 19900, CancellationToken);

    private Task<HttpResponseMessage> PostSignedAsync(string fileName, Payment payment, long? transactionId = null)
    {
        var payload = Payload(fileName, payment, transactionId ?? NewTransactionId());
        return PostAsync(payload, PaymobPayloads.Sign(payload, ApiFactory.TestPaymobHmacSecret));
    }

    private async Task<HttpResponseMessage> PostAsync(string payload, string? signature)
    {
        using var client = AuthTestClient.Create(factory);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var route = signature is null ? SubscriptionTestData.WebhookRoute : $"{SubscriptionTestData.WebhookRoute}?hmac={signature}";
        return await client.PostAsync(route, content, CancellationToken).ConfigureAwait(false);
    }

    private async Task<(Payment Payment, List<Subscription> Subscriptions)> ReadAsync(Guid paymentId, Guid studentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await context.Payments.AsNoTracking().SingleAsync(x => x.Id == paymentId, CancellationToken).ConfigureAwait(false);
        var subscriptions = await context.Subscriptions.AsNoTracking().Where(x => x.StudentId == studentId).ToListAsync(CancellationToken).ConfigureAwait(false);
        return (payment, subscriptions);
    }

    private static string Payload(string fileName, Payment payment, long transactionId) => PaymobPayloads.ForPayment(fileName, payment.Id, transactionId, payment.AmountMinor, OrderId);

    private static long NewTransactionId() => Random.Shared.NextInt64(1_000_000_000, long.MaxValue);

    private static async Task<string?> OutcomeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("outcome").GetString();

    private static async Task<string?> CodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("code").GetString();

    private static async Task<string?> GetTierAsync(HttpClient client)
    {
        using var response = await client.GetAsync($"{SubscriptionTestData.SubscriptionsRoute}/entitlement", CancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("tier").GetString();
    }
}
