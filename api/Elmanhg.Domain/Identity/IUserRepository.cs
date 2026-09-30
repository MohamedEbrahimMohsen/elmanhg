using Core.DDD.Repositories;

namespace Elmanhg.Domain.Identity;

public interface IUserRepository : IRepository<User>
{
    Task ExecuteInAdminRosterLockAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}
