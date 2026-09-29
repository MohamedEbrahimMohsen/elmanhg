using Elmanhg.Application.Payments.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Payments.Shared;

public sealed class AdminPaymentResultGeneratorTests
{
    private static readonly DateTimeOffset PaidAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RefundedAt = PaidAt.AddDays(2);

    [Fact]
    public void Generate_RefundedPayment_MapsRefundFieldsAndCannotRefund()
    {
        var student = User.CreateStudentWithPhone("Omar Said", "+201000000001");
        var subscriptionId = Guid.NewGuid();
        var payment = Payment.Create(student.Id, SubscriptionPlan.Base, BillingPeriod.Termly, 4, new Money(69900, "EGP"));
        payment.MarkSucceeded(subscriptionId, "txn-1", "{}", PaidAt);
        payment.MarkRefunded("refund-1", RefundedAt, Guid.NewGuid(), "Duplicate charge", Guid.NewGuid());

        var result = AdminPaymentResultGenerator.Generate(payment, student);

        (result.Id, result.StudentId, result.StudentName, result.StudentContact).Should().Be((payment.Id, student.Id, "Omar Said", "+201000000001"));
        (result.Plan, result.Period, result.PeriodMonths, result.Amount, result.Status).Should().Be((SubscriptionPlan.Base, BillingPeriod.Termly, 4, new Money(69900, "EGP"), PaymentStatus.Refunded));
        (result.PaymobTransactionId, result.SubscriptionId, result.CreatedAt, result.CompletedAt).Should().Be(("txn-1", (Guid?)subscriptionId, payment.CreationDate, (DateTimeOffset?)PaidAt));
        (result.RefundedAt, result.RefundReason, result.RefundTransactionId, result.CanRefund, result.NeedsReview).Should().Be(((DateTimeOffset?)RefundedAt, "Duplicate charge", "refund-1", false, false));
        AdminPaymentResultGenerator.Generate(payment, User.CreateStudentWithEmail("Mona Ali", "mona@example.com")).StudentContact.Should().Be("mona@example.com");
    }
}
