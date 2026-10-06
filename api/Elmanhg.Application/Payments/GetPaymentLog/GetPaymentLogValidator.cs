using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Payments.GetPaymentLog;

public sealed class GetPaymentLogValidator : AbstractValidator<GetPaymentLogQuery>
{
    public GetPaymentLogValidator(IOptions<SubscriptionsOptions> subscriptionsOptions)
    {
        var options = subscriptionsOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.AdminPaymentLogMaxPageSize, ErrorCodes.PaymentLogPageNumberInvalid, ErrorCodes.PaymentLogPageSizeInvalid);
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.PaymentLogStatusInvalid);
        RuleFor(x => x.Plan)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.PaymentLogPlanInvalid);
        RuleFor(x => x.Reference).ValidateMaxLength(options.PaymentLogReferenceMaxLength, ErrorCodes.PaymentLogReferenceTooLong);
        RuleFor(x => x).ValidateDateRange(x => x.From, x => x.To, ErrorCodes.PaymentLogDateRangeInvalid);
    }
}
