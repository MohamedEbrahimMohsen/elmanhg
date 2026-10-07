using Core.Identity.Tokens.AccessToken;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Users.CheckUserActive;

public sealed class CheckUserActiveHandler(IUserRepository userRepository, IMemoryCache memoryCache, IOptions<UsersOptions> usersOptions) : IRequestHandler<CheckUserActiveQuery, bool>
{
    public async Task<bool> Handle(CheckUserActiveQuery request, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        return state.IsActive && SecurityStampClaim.Matches(request.SecurityStampFingerprint, state.SecurityStamp);
    }

    private async Task<UserTokenState> GetStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = UserActiveCacheKey.For(userId);
        if (memoryCache.TryGetValue(key, out UserTokenState? cached) && cached is not null)
        {
            return cached;
        }

        var user = await userRepository.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var state = new UserTokenState(user is { IsActive: true }, user?.SecurityStamp);
        var seconds = usersOptions.Value.ActiveStatusCacheSeconds;
        if (seconds > 0)
        {
            memoryCache.Set(key, state, TimeSpan.FromSeconds(seconds));
        }

        return state;
    }

    private sealed record UserTokenState(bool IsActive, string? SecurityStamp);
}
