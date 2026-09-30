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
        var key = UserActiveCacheKey.For(request.UserId);
        if (memoryCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        var user = await userRepository.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var active = user is { IsActive: true };
        var seconds = usersOptions.Value.ActiveStatusCacheSeconds;
        if (seconds > 0)
        {
            memoryCache.Set(key, active, TimeSpan.FromSeconds(seconds));
        }

        return active;
    }
}
