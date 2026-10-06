using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Payments.RefundPayment;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Payments.RefundPayment;

public sealed class RefundPaymentValidatorTests
{
    private readonly RefundPaymentValidator _validator = new(Options.Create(new SubscriptionsOptions { RefundReasonMaxLength = 500 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new RefundPaymentCommand(Guid.NewGuid(), "Duplicate charge", Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyPaymentId_FailsWithPaymentIdRequired()
    {
        var result = _validator.Validate(new RefundPaymentCommand(Guid.Empty, "Duplicate charge", Guid.NewGuid()));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentIdRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingReason_FailsWithReasonRequired(string? reason)
    {
        var result = _validator.Validate(new RefundPaymentCommand(Guid.NewGuid(), reason, Guid.NewGuid()));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentRefundReasonRequired);
    }

    [Fact]
    public void Validate_ReasonTooLong_FailsWithReasonTooLong()
    {
        var result = _validator.Validate(new RefundPaymentCommand(Guid.NewGuid(), new string('a', 501), Guid.NewGuid()));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentRefundReasonTooLong);
    }

    [Fact]
    public void Validate_NullIdempotencyKey_FailsWithKeyRequired()
    {
        var result = _validator.Validate(new RefundPaymentCommand(Guid.NewGuid(), "Duplicate charge", null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentRefundIdempotencyKeyRequired);
    }

    [Fact]
    public void Validate_EmptyIdempotencyKey_FailsWithKeyRequired()
    {
        var result = _validator.Validate(new RefundPaymentCommand(Guid.NewGuid(), "Duplicate charge", Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentRefundIdempotencyKeyRequired);
    }

    [Fact]
    public void Validate_EmptyIdempotencyKey_ReportsKeyRequiredOnce()
    {
        var result = _validator.Validate(new RefundPaymentCommand(Guid.NewGuid(), "Duplicate charge", Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Where(x => x == ErrorCodes.PaymentRefundIdempotencyKeyRequired).Should().ContainSingle();
    }
}
