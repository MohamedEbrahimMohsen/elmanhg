using Core.DDD.Models;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Repositories;

public static class QueryablePagingExtensions
{
    public static async Task<PageData<T>> ToPageDataAsync<T>(this IQueryable<T> query, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var totalItems = await query.LongCountAsync(cancellationToken)
                                    .ConfigureAwait(false);

        var offset = PageCalculator.Offset(pageNumber, pageSize);

        List<T> items = PageCalculator.IsPastEnd(offset, totalItems) ? [] : await query.Skip((int)offset)
                                                                                     .Take(pageSize)
                                                                                     .ToListAsync(cancellationToken)
                                                                                     .ConfigureAwait(false);

        return new PageData<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = PageCalculator.TotalPages(totalItems, pageSize),
        };
    }
}
