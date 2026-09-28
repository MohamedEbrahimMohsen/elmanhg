using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.QuestionValidation.GetValidationQuestion;

public sealed class GetValidationQuestionValidator : AbstractValidator<GetValidationQuestionQuery>
{
    public GetValidationQuestionValidator()
    {
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
    }
}
