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

        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.PaymentHistoryPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.PaymentHistoryMaxPageSize, ErrorCodes.PaymentHistoryPageSizeInvalid);
    }
}
