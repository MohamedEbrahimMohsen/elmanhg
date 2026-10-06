using Core.DDD.Entities;
using Core.Errors;
using System.Linq.Expressions;

namespace Core.DDD.Repositories;

public static class RepositoryExtensions
{
    public static async Task<T> GetRequiredAsync<T>(this IRepository<T> repository, Guid id, string errorCode, CancellationToken cancellationToken, Func<IQueryable<T>, IQueryable<T>>? include = null, bool asNoTracking = false) where T : class, IEntity
        => await repository.GetByIdAsync(id, cancellationToken, include, asNoTracking).ConfigureAwait(false) ?? throw new NotFoundCoreException(errorCode);

    public static async Task<T> GetRequiredAsync<T>(this IRepository<T> repository, Expression<Func<T, bool>> predicate, string errorCode, CancellationToken cancellationToken, Func<IQueryable<T>, IQueryable<T>>? include = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false) where T : class, IEntity
        => await repository.FirstOrDefaultAsync(predicate, cancellationToken, include, orderBy, asNoTracking).ConfigureAwait(false) ?? throw new NotFoundCoreException(errorCode);
}
