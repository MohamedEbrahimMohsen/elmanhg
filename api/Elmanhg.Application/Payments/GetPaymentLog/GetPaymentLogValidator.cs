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

        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.PaymentLogPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.AdminPaymentLogMaxPageSize, ErrorCodes.PaymentLogPageSizeInvalid);
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.PaymentLogStatusInvalid);
        RuleFor(x => x.Plan)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.PaymentLogPlanInvalid);
        RuleFor(x => x.Reference).ValidateMaxLength(options.PaymentLogReferenceMaxLength, ErrorCodes.PaymentLogReferenceTooLong);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From < x.To).WithErrorCode(ErrorCodes.PaymentLogDateRangeInvalid);
    }
}
