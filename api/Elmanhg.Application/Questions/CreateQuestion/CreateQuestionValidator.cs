using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.CreateQuestion;

public sealed class CreateQuestionValidator : AbstractValidator<CreateQuestionCommand>
{
    public CreateQuestionValidator(IOptions<ContentOptions> contentOptions)
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.Question).SetValidator(new QuestionFieldsValidator(contentOptions));
    }
}
