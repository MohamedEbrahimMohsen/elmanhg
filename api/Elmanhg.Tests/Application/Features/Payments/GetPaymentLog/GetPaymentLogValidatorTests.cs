using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Payments.GetPaymentLog;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Payments.GetPaymentLog;

public sealed class GetPaymentLogValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly GetPaymentLogValidator _validator = new(Options.Create(new SubscriptionsOptions { AdminPaymentLogMaxPageSize = 100, PaymentLogReferenceMaxLength = 100 }));

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var result = _validator.Validate(Query() with { Status = PaymentStatus.Refunded, Plan = SubscriptionPlan.Base, Reference = "txn-1", From = From, To = From.AddDays(1), PageSize = 100 });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberBelowOne_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(Query() with { PageNumber = 0 });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentLogPageNumberInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_PageSizeOutOfRange_FailsWithPageSizeInvalid(int pageSize)
    {
        var result = _validator.Validate(Query() with { PageSize = pageSize });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentLogPageSizeInvalid);
    }

    [Fact]
    public void Validate_UndefinedStatus_FailsWithStatusInvalid()
    {
        var result = _validator.Validate(Query() with { Status = (PaymentStatus)99 });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentLogStatusInvalid);
    }

    [Fact]
    public void Validate_UndefinedPlan_FailsWithPlanInvalid()
    {
        var result = _validator.Validate(Query() with { Plan = (SubscriptionPlan)99 });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentLogPlanInvalid);
    }

    [Fact]
    public void Validate_ReferenceTooLong_FailsWithReferenceTooLong()
    {
        var result = _validator.Validate(Query() with { Reference = new string('1', 101) });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentLogReferenceTooLong);
    }

    [Fact]
    public void Validate_FromNotBeforeTo_FailsWithDateRangeInvalid()
    {
        var result = _validator.Validate(Query() with { From = From, To = From });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentLogDateRangeInvalid);
    }

    [Fact]
    public void Validate_PageOffsetPastIntRange_FailsPageNumberInvalid()
    {
        var result = _validator.Validate(Query() with { PageNumber = int.MaxValue, PageSize = 20 });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentLogPageNumberInvalid);
    }

    private static GetPaymentLogQuery Query() => new(null, null, false, null, null, null, null);
}
