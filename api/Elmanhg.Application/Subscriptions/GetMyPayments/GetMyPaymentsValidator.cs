using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.GetMyPayments;

public sealed class GetMyPaymentsValidator : AbstractValidator<GetMyPaymentsQuery>
{
    public GetMyPaymentsValidator(IOptions<SubscriptionsOptions> subscriptionsOptions)
    {
        var options = subscriptionsOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.PaymentHistoryMaxPageSize, ErrorCodes.PaymentHistoryPageNumberInvalid, ErrorCodes.PaymentHistoryPageSizeInvalid);
    }
}
