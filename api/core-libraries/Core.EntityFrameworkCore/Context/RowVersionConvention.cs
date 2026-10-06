using Core.DDD.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Context;

public static class RowVersionConvention
{
    public static void ApplyRowVersionConvention(this ModelBuilder modelBuilder)
    {
        var versionedTypes = modelBuilder.Model.GetEntityTypes()
            .Where(x => x.BaseType is null)
            .Where(x => !x.IsOwned())
            .Where(x => !x.HasSharedClrType)
            .Where(x => typeof(IVersioned).IsAssignableFrom(x.ClrType))
            .Select(x => x.ClrType)
            .ToList();

        foreach (var clrType in versionedTypes)
        {
            modelBuilder.Entity(clrType).Property(nameof(IVersioned.Version)).IsRowVersion();
        }
    }
}
