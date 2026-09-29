using Elmanhg.Application.Payments.GetPaymentLog;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Payments.GetPaymentLog;

public sealed class GetPaymentLogFilterTests
{
    private static readonly DateTimeOffset Boundary = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_NoFilters_MatchesEveryPayment()
    {
        List<Payment> payments = [Pending(), Succeeded(), Failed()];

        var matched = Apply(Query(), payments);

        matched.Should().HaveCount(3);
    }

    [Fact]
    public void Build_Status_MatchesOnlyThatStatus()
    {
        var failed = Failed();

        var matched = Apply(Query() with { Status = PaymentStatus.Failed }, [Pending(), Succeeded(), failed]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(failed);
    }

    [Fact]
    public void Build_Plan_MatchesOnlyThatPlan()
    {
        var askTeacher = Pending(SubscriptionPlan.AskTeacher);

        var matched = Apply(Query() with { Plan = SubscriptionPlan.AskTeacher }, [Pending(), askTeacher]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(askTeacher);
    }

    [Fact]
    public void Build_NeedsReview_MatchesOpenReviewsOnly()
    {
        var open = Succeeded();
        open.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);
        var resolved = Succeeded();
        resolved.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);
        resolved.ResolveReview(Guid.NewGuid(), Boundary);

        var matched = Apply(Query() with { NeedsReview = true }, [open, resolved, Succeeded()]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(open);
    }

    [Fact]
    public void Build_StudentId_MatchesThatStudent()
    {
        var mine = Pending();

        var matched = Apply(Query() with { StudentId = mine.StudentId }, [mine, Pending()]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(mine);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("paymob")]
    [InlineData("refund")]
    [InlineData("order")]
    public void Build_Reference_MatchesPaymentIdOrTransactionIds(string field)
    {
        var target = Pending();
        target.LinkProviderOrder("order-42");
        target.MarkSucceeded(Guid.NewGuid(), "paymob-42", "{}", Boundary);
        target.MarkRefunded("refund-42", Boundary, Guid.NewGuid(), "Reason", Guid.NewGuid());
        var reference = field switch
        {
            "id" => target.Id.ToString(),
            "paymob" => "paymob-42",
            "refund" => "refund-42",
            _ => "order-42",
        };

        var matched = Apply(Query() with { Reference = $" {reference} " }, [target, Succeeded()]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(target);
    }

    [Fact]
    public void Build_DateRange_IsFromInclusiveToExclusive()
    {
        var atFrom = Pending(createdAt: Boundary);
        var beforeFrom = Pending(createdAt: Boundary.AddTicks(-1));
        var atTo = Pending(createdAt: Boundary.AddDays(1));
        var beforeTo = Pending(createdAt: Boundary.AddDays(1).AddTicks(-1));

        var matched = Apply(Query() with { From = Boundary, To = Boundary.AddDays(1) }, [atFrom, beforeFrom, atTo, beforeTo]);

        matched.Should().BeEquivalentTo([atFrom, beforeTo]);
    }

    private static GetPaymentLogQuery Query() => new(null, null, false, null, null, null, null);

    private static List<Payment> Apply(GetPaymentLogQuery query, List<Payment> payments) => payments
        .Where(GetPaymentLogFilter.Build(query).Compile())
        .ToList();

    private static Payment Pending(SubscriptionPlan plan = SubscriptionPlan.Base, DateTimeOffset? createdAt = null)
    {
        var payment = Payment.Create(Guid.NewGuid(), plan, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        payment.CreationDate = createdAt ?? Boundary;
        return payment;
    }

    private static Payment Succeeded()
    {
        var payment = Pending();
        payment.MarkSucceeded(Guid.NewGuid(), $"txn-{Guid.NewGuid():N}", "{}", Boundary);
        return payment;
    }

    private static Payment Failed()
    {
        var payment = Pending();
        payment.MarkFailed($"txn-{Guid.NewGuid():N}", "{}", Boundary);
        return payment;
    }
}
