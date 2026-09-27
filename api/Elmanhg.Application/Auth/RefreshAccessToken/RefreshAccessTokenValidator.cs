using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Auth.RefreshAccessToken;

public sealed class RefreshAccessTokenValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .ValidateRequired(ErrorCodes.RefreshTokenIsRequired);
    }
}
