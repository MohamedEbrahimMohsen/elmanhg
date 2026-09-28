using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.UpdateQuestion;

public sealed class UpdateQuestionValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionValidator(IOptions<ContentOptions> contentOptions)
    {
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
        RuleFor(x => x.Question).SetValidator(new QuestionFieldsValidator(contentOptions));
    }
}
