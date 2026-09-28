using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Elmanhg.Application.Exams.SaveExamAnswer;

public sealed class SaveExamAnswerValidator : AbstractValidator<SaveExamAnswerCommand>
{
    public SaveExamAnswerValidator(IOptions<SessionsOptions> sessionsOptions)
    {
        var options = sessionsOptions.Value;
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
        RuleFor(x => x.Answer)
            .Must(x => x.ValueKind == JsonValueKind.Object)
            .WithErrorCode(ErrorCodes.QuestionAnswerInvalid);
        RuleFor(x => x.Answer)
            .Must(x => x.GetRawText().Length <= options.AnswerMaxLength)
            .WithErrorCode(ErrorCodes.AttemptAnswerTooLong)
            .When(x => x.Answer.ValueKind == JsonValueKind.Object);
    }
}
