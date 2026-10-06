using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Elmanhg.Application.Users.ReactivateUser;

public sealed class ReactivateUserHandler(UserManager<User> userManager, ICurrentUserService currentUserService, IMemoryCache memoryCache) : IRequestHandler<ReactivateUserCommand>
{
    public async Task Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.UserNotFound);
        user.Reactivate(userId);
        var result = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ConflictCoreException(ErrorCodes.UserModifiedConcurrently);
        }

        memoryCache.Remove(UserActiveCacheKey.For(user.Id));
    }
}
