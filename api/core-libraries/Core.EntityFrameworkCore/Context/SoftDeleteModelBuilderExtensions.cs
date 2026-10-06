using Core.DDD.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Core.EntityFrameworkCore.Context;

public static class SoftDeleteModelBuilderExtensions
{
    public static ModelBuilder ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        // EF allows a query filter only on a hierarchy root; an existing filter wins so a context's own filters are never overwritten.
        var entityTypes = modelBuilder.Model.GetEntityTypes()
                                            .Where(entityType => typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)
                                                                 && entityType.BaseType is null
                                                                 && !entityType.IsOwned()
                                                                 && entityType.GetDeclaredQueryFilters().Count == 0)
                                            .ToList();

        foreach (var entityType in entityTypes)
        {
            var parameter = Expression.Parameter(entityType.ClrType, "x");
            var notDeleted = Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(notDeleted, parameter));
        }

        return modelBuilder;
    }
}
