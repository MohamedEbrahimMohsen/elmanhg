using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Auth.Logout;

public sealed class LogoutHandler(UserManager<User> userManager, ICurrentUserService currentUserService) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var user = await userManager.FindByIdAsync(currentUserService.UserId.Value.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotFound);
        }

        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
    }
}
