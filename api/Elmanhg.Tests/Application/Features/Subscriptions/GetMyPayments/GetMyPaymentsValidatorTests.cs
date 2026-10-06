using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GetMyPayments;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GetMyPayments;

public sealed class GetMyPaymentsValidatorTests
{
    private readonly GetMyPaymentsValidator _validator = new(Options.Create(new SubscriptionsOptions { PaymentHistoryMaxPageSize = 50 }));

    [Fact]
    public void Validate_DefaultPaging_Passes()
    {
        var result = _validator.Validate(new GetMyPaymentsQuery());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberBelowOne_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(new GetMyPaymentsQuery(PageNumber: 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentHistoryPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeBelowOne_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetMyPaymentsQuery(PageSize: 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentHistoryPageSizeInvalid);
    }

    [Fact]
    public void Validate_PageSizeAboveMax_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetMyPaymentsQuery(PageSize: 51));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentHistoryPageSizeInvalid);
    }

    [Fact]
    public void Validate_PageOffsetPastIntRange_FailsPageNumberInvalid()
    {
        var result = _validator.Validate(new GetMyPaymentsQuery(PageNumber: int.MaxValue, PageSize: 20));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PaymentHistoryPageNumberInvalid);
    }
}
