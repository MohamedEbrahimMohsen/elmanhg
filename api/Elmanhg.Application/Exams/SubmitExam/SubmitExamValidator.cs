using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.SubmitExam;

public sealed class SubmitExamValidator : AbstractValidator<SubmitExamCommand>
{
    public SubmitExamValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
    }
}
