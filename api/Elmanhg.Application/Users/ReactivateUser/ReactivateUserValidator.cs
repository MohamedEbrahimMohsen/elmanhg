using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Users.ReactivateUser;

public sealed class ReactivateUserValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserValidator()
    {
        RuleFor(x => x.UserId).ValidateRequired(ErrorCodes.UserIdRequired);
    }
}
