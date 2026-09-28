using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Sessions.StartQuizSession;

public sealed class StartQuizSessionValidator : AbstractValidator<StartQuizSessionCommand>
{
    public StartQuizSessionValidator(IOptions<SessionsOptions> sessionsOptions)
    {
        var options = sessionsOptions.Value;
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.QuestionCount.GetValueOrDefault())
            .ValidateRange(options.MinQuizSize, options.MaxQuizSize, ErrorCodes.SessionQuestionCountInvalid)
            .When(x => x.QuestionCount.HasValue)
            .OverridePropertyName(nameof(StartQuizSessionCommand.QuestionCount));
    }
}
