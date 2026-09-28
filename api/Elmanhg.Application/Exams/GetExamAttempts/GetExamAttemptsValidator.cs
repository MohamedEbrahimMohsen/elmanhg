using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.GetExamAttempts;

public sealed class GetExamAttemptsValidator : AbstractValidator<GetExamAttemptsQuery>
{
    public GetExamAttemptsValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
    }
}
