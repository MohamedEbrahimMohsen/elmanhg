using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Sessions.FinishSession;

public sealed class FinishSessionValidator : AbstractValidator<FinishSessionCommand>
{
    public FinishSessionValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
    }
}
