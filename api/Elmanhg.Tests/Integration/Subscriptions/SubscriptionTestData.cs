using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Subscriptions;

public static class SubscriptionTestData
{
    public const string PlansRoute = "/api/plans";
    public const string SubscriptionsRoute = "/api/subscriptions";
    public const string Currency = "EGP";
    public const string FakeCheckoutPath = "/student/fake-checkout";
    public const string WebhookRoute = "/api/payments/paymob/webhook";

    public static int MonthsFor(BillingPeriod period) => period switch { BillingPeriod.Monthly => 1, BillingPeriod.Termly => 4, _ => 12 };

    public static async Task SeedSubscriptionAsync(ApiFactory factory, Subscription subscription, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Payment> SeedCompletedPaymentAsync(ApiFactory factory, Guid studentId, bool succeeded, long amountMinor, CancellationToken cancellationToken)
    {
        var payment = NewPayment(studentId, amountMinor);
        if (succeeded)
        {
            var subscription = new SubscriptionBuilder().ForStudent(studentId).StartingAt(DateTimeOffset.UtcNow).Build();
            await SeedSubscriptionAsync(factory, subscription, cancellationToken).ConfigureAwait(false);
            payment.MarkSucceeded(subscription.Id, $"txn-{Guid.NewGuid():N}", "{}", DateTimeOffset.UtcNow);
        }
        else
        {
            payment.MarkFailed($"txn-{Guid.NewGuid():N}", "{}", DateTimeOffset.UtcNow);
        }

        await SavePaymentAsync(factory, payment, cancellationToken).ConfigureAwait(false);
        return payment;
    }

    public static Task SeedPendingPaymentAsync(ApiFactory factory, Guid studentId, CancellationToken cancellationToken) => SavePaymentAsync(factory, NewPayment(studentId, 19900), cancellationToken);

    public static async Task<Payment> SeedPendingPaymentAsync(ApiFactory factory, Guid studentId, SubscriptionPlan plan, BillingPeriod period, long amountMinor, CancellationToken cancellationToken)
    {
        var payment = Payment.Create(studentId, plan, period, MonthsFor(period), new Money(amountMinor, Currency));
        await SavePaymentAsync(factory, payment, cancellationToken).ConfigureAwait(false);
        return payment;
    }

    public static Payment NewPayment(Guid studentId, long amountMinor) => Payment.Create(studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(amountMinor, Currency));

    private static async Task SavePaymentAsync(ApiFactory factory, Payment payment, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
