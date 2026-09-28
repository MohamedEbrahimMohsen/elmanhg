using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.GetExamSession;

public sealed class GetExamSessionValidator : AbstractValidator<GetExamSessionQuery>
{
    public GetExamSessionValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
    }
}
