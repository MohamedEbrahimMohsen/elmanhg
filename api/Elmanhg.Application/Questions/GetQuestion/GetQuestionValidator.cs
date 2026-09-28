using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Questions.GetQuestion;

public sealed class GetQuestionValidator : AbstractValidator<GetQuestionQuery>
{
    public GetQuestionValidator()
    {
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
    }
}
