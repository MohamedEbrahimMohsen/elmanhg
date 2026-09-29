using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Payments;

public sealed class RefundPaymentEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Admin_RefundsPaymentAndStudentLosesAccess()
    {
        var (student, studentClient) = await SignedInStudentAsync(factory);
        var (payment, subscription) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow.AddDays(-1), null, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("refundTransactionId").GetString(), body.GetProperty("canRefund").GetBoolean()).Should().Be(("Refunded", $"fake-refund-{payment.Id:N}", false));
        var (stored, storedSubscription) = await ReadAsync(payment.Id, subscription.Id);
        (stored.Status, stored.RefundReason, storedSubscription.Status).Should().Be((PaymentStatus.Refunded, "Duplicate charge", SubscriptionStatus.Expired));
        (await GetTierAsync(studentClient)).Should().Be("Free");
    }

    [Fact]
    public async Task Post_SameIdempotencyKeyTwice_ReplaysSameResult()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);
        var key = Guid.NewGuid();

        using var first = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", key), CancellationToken);
        using var second = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", key), CancellationToken);

        (first.StatusCode, second.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        (await RefundIdAsync(second)).Should().Be(await RefundIdAsync(first)).And.NotBeNull();
    }

    [Fact]
    public async Task Post_SameIdempotencyKeyConcurrently_RefundsAndShortensSubscriptionOnce()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, subscription) = await SeedRenewedAsync(student.Id);
        var expectedEnd = subscription.CurrentPeriodEnd.AddMonths(-payment.PeriodMonths);
        using var firstAdmin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);
        using var secondAdmin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(
            firstAdmin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", key), CancellationToken),
            secondAdmin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", key), CancellationToken));

        using var first = responses[0];
        using var second = responses[1];
        var statuses = new[] { first.StatusCode, second.StatusCode };
        statuses.Should().Contain(HttpStatusCode.OK).And.OnlyContain(x => x == HttpStatusCode.OK || x == HttpStatusCode.Conflict);
        foreach (var conflict in responses.Where(x => x.StatusCode == HttpStatusCode.Conflict))
        {
            (await CodeAsync(conflict)).Should().BeOneOf("PAYMENT_MODIFIED_CONCURRENTLY", "PAYMENT_TRANSACTION_ALREADY_RECORDED", "SUBSCRIPTION_MODIFIED_CONCURRENTLY");
        }

        var (stored, storedSubscription) = await ReadAsync(payment.Id, subscription.Id);
        (stored.Status, stored.RefundTransactionId, stored.RefundIdempotencyKey).Should().Be((PaymentStatus.Refunded, $"fake-refund-{payment.Id:N}", (Guid?)key));
        (storedSubscription.Status, storedSubscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Active, expectedEnd));
    }

    [Fact]
    public async Task Post_AlreadyRefundedWithNewKey_Returns400AlreadyRefunded()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);
        using var first = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", Guid.NewGuid()), CancellationToken);

        using var response = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Again", Guid.NewGuid()), CancellationToken);

        (first.StatusCode, response.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.BadRequest));
        (await CodeAsync(response)).Should().Be("PAYMENT_ALREADY_REFUNDED");
    }

    [Fact]
    public async Task Post_MissingIdempotencyKey_Returns422WithCode()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, subscription) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", null), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(response)).Should().Contain("PAYMENT_REFUND_IDEMPOTENCY_KEY_REQUIRED");
        (await ReadAsync(payment.Id, subscription.Id)).Payment.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task Post_UnknownPayment_Returns404()
    {
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.SendAsync(PaymentsTestData.RefundRequest(Guid.NewGuid(), "Duplicate charge", Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeAsync(response)).Should().Be("PAYMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.SendAsync(PaymentsTestData.RefundRequest(Guid.NewGuid(), "Duplicate charge", Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Admin_WritesAuditRowWithRefundDiff()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.SendAsync(PaymentsTestData.RefundRequest(payment.Id, "Duplicate charge", Guid.NewGuid()), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var audit = await ContentTestData.ReadAuditAsync(factory, "Payment.Refund", payment.Id, CancellationToken);
        (audit.Outcome, audit.ResourceType).Should().Be(("Success", "Payment"));
        audit.Diff.Should().Contain("refundReason").And.Contain("Duplicate charge");
    }

    private async Task<(Payment Payment, Subscription Subscription)> SeedRenewedAsync(Guid studentId)
    {
        var startsAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds());
        var subscription = Subscription.Start(studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, startsAt, null, studentId);
        subscription.Renew(BillingPeriod.Monthly, 1, null, startsAt.AddDays(1), TimeSpan.Zero);
        var payment = Payment.Create(studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        payment.MarkSucceeded(subscription.Id, $"txn-{Guid.NewGuid():N}", "{}", startsAt.AddDays(1));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Subscriptions.Add(subscription);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (payment, subscription);
    }

    private async Task<(Payment Payment, Subscription Subscription)> ReadAsync(Guid paymentId, Guid subscriptionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await context.Payments.AsNoTracking().SingleAsync(x => x.Id == paymentId, CancellationToken).ConfigureAwait(false);
        var subscription = await context.Subscriptions.AsNoTracking().SingleAsync(x => x.Id == subscriptionId, CancellationToken).ConfigureAwait(false);
        return (payment, subscription);
    }

    private static async Task<string?> RefundIdAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("refundTransactionId").GetString();

    private static async Task<string?> CodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("code").GetString();

    private static async Task<string?> GetTierAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/subscriptions/entitlement", CancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("tier").GetString();
    }
}
