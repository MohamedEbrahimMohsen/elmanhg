using Core.DDD.Repositories;

namespace Core.Identity.Tokens.RefreshToken;

public interface IIssuedRefreshTokenRepository : IRepository<IssuedRefreshToken>
{
    Task AddIfAbsentAsync(IssuedRefreshToken token, CancellationToken cancellationToken);
}
