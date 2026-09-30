using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public sealed class SubmitAnswerValidator : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerValidator(IOptions<SessionsOptions> sessionsOptions)
    {
        var options = sessionsOptions.Value;
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
        RuleFor(x => x.Answer)
            .Must(x => x.ValueKind == JsonValueKind.Object)
            .WithErrorCode(ErrorCodes.QuestionAnswerInvalid);
        RuleFor(x => x.Answer)
            .Must(x => x.GetRawText().Length <= options.RequestAnswerMaxLength)
            .WithErrorCode(ErrorCodes.AttemptAnswerTooLong)
            .When(x => x.Answer.ValueKind == JsonValueKind.Object);
        RuleFor(x => x.TimeTakenMilliseconds.GetValueOrDefault())
            .ValidateNonNegative(ErrorCodes.AttemptTimeTakenInvalid)
            .When(x => x.TimeTakenMilliseconds.HasValue)
            .OverridePropertyName(nameof(SubmitAnswerCommand.TimeTakenMilliseconds));
    }
}
