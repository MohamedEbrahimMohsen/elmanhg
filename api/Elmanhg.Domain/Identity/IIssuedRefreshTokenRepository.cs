using Core.DDD.Repositories;

namespace Elmanhg.Domain.Identity;

public interface IIssuedRefreshTokenRepository : IRepository<IssuedRefreshToken>
{
    Task AddIfAbsentAsync(IssuedRefreshToken token, CancellationToken cancellationToken);
}
