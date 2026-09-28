using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.AutoSubmitExam;

public sealed class AutoSubmitExamValidator : AbstractValidator<AutoSubmitExamCommand>
{
    public AutoSubmitExamValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
    }
}
