using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Questions.RetireQuestion;

public sealed class RetireQuestionValidator : AbstractValidator<RetireQuestionCommand>
{
    public RetireQuestionValidator()
    {
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
    }
}
