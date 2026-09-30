using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Elmanhg.Application.Users.SuspendUser;

public sealed class SuspendUserHandler(UserManager<User> userManager, IUserRepository userRepository, ICurrentUserService currentUserService, IMemoryCache memoryCache) : IRequestHandler<SuspendUserCommand>
{
    public async Task Handle(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var actorId = currentUserService.UserId.Value;
        await userRepository.ExecuteInAdminRosterLockAsync(async token =>
        {
            var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.UserNotFound);
            var activeAdminCount = user.Role == UserRole.Admin ? await userRepository.CountAsync(token, x => x.Role == UserRole.Admin && x.Status == UserStatus.Active).ConfigureAwait(false) : 0;
            user.Suspend(actorId, activeAdminCount);
            var result = await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new ConflictCoreException(ErrorCodes.UserModifiedConcurrently);
            }
        }, cancellationToken).ConfigureAwait(false);

        memoryCache.Remove(UserActiveCacheKey.For(request.UserId));
    }
}
