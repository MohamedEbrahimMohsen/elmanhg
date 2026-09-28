using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Sessions.GetSession;

public sealed class GetSessionValidator : AbstractValidator<GetSessionQuery>
{
    public GetSessionValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
    }
}
