using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using NSubstitute;

namespace Elmanhg.Tests.Core.Persistence;

public static class ConcurrencyFailures
{
    public static DbUpdateConcurrencyException For(DbContext context, params object[] entities)
    {
        var entries = entities.Select(entity => CreateEntry(context, entity)).ToList();
        return new DbUpdateConcurrencyException("Stale row version.", entries);
    }

    private static IUpdateEntry CreateEntry(DbContext context, object entity)
    {
        var entry = Substitute.For<IUpdateEntry>();
        entry.EntityState.Returns(EntityState.Modified);
        entry.SharedIdentityEntry.Returns((IUpdateEntry?)null);
        entry.ToEntityEntry().Returns(context.Entry(entity));
        return entry;
    }
}
