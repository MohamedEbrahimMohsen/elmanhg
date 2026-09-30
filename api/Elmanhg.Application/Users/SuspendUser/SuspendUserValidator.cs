using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Users.SuspendUser;

public sealed class SuspendUserValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserValidator()
    {
        RuleFor(x => x.UserId).ValidateRequired(ErrorCodes.UserIdRequired);
    }
}
