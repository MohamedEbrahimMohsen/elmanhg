using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.QuestionValidation.RecordQuestionOpening;

public sealed class RecordQuestionOpeningValidator : AbstractValidator<RecordQuestionOpeningCommand>
{
    public RecordQuestionOpeningValidator()
    {
        RuleFor(x => x.ReviewSessionId).ValidateRequired(ErrorCodes.ReviewSessionIdRequired);
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
    }
}
