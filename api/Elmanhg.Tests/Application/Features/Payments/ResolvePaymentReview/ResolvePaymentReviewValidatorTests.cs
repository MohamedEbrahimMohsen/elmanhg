using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Payments.ResolvePaymentReview;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Payments.ResolvePaymentReview;

public sealed class ResolvePaymentReviewValidatorTests
{
    private readonly ResolvePaymentReviewValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new ResolvePaymentReviewCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyPaymentId_FailsWithPaymentIdRequired()
    {
        var result = _validator.Validate(new ResolvePaymentReviewCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentIdRequired);
    }
}
