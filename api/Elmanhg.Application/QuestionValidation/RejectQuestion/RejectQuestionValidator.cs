using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Application.QuestionValidation.RejectQuestion;

public sealed class RejectQuestionValidator : AbstractValidator<RejectQuestionCommand>
{
    public RejectQuestionValidator(IOptions<QuestionValidationOptions> questionValidationOptions)
    {
        var options = questionValidationOptions.Value;

        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
        RuleFor(x => x.Version).ValidateMin(1, ErrorCodes.QuestionVersionInvalid);
        RuleFor(x => x.Reason)
            .ValidateRequired(DomainErrorCodes.QuestionRejectionReasonRequired)
            .ValidateMaxLength(options.RejectionReasonMaxLength, ErrorCodes.QuestionRejectionReasonTooLong);
    }
}
