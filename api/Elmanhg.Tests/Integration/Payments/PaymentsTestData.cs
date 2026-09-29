using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Elmanhg.Tests.Integration.Payments;

public static class PaymentsTestData
{
    public const string Route = "/api/payments";

    public static async Task<HttpClient> AdminClientAsync(ApiFactory factory, CancellationToken cancellationToken)
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, cancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<(Payment Payment, Subscription Subscription)> SeedSucceededAsync(ApiFactory factory, Guid studentId, DateTimeOffset startsAt, PaymentReviewReason? flag, CancellationToken cancellationToken)
    {
        var payment = Payment.Create(studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        var subscription = Subscription.Start(studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, startsAt, null, studentId);
        payment.MarkSucceeded(subscription.Id, $"txn-{Guid.NewGuid():N}", "{}", startsAt);
        if (flag is { } reason)
        {
            payment.FlagForReview(reason);
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Subscriptions.Add(subscription);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (payment, subscription);
    }

    public static HttpRequestMessage RefundRequest(Guid paymentId, string reason, Guid? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Route}/{paymentId}/refund") { Content = JsonContent.Create(new { reason }) };
        if (key is { } idempotencyKey)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        }

        return request;
    }
}
