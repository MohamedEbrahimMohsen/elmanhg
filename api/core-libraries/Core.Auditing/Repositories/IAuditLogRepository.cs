using Core.Auditing.Entities;
using Core.DDD.Models;
using System.Linq.Expressions;

namespace Core.Auditing.Repositories;

public interface IAuditLogRepository
{
    Task AppendAsync(AuditLog entry, CancellationToken cancellationToken);
    Task<PageData<AuditLog>> FindPaginatedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken, Expression<Func<AuditLog, bool>>? filter = null, Func<IQueryable<AuditLog>, IQueryable<AuditLog>>? include = null, Func<IQueryable<AuditLog>, IOrderedQueryable<AuditLog>>? orderBy = null, bool asNoTracking = false);
    Task<List<string>> GetResourceTypesAsync(CancellationToken cancellationToken);
}
