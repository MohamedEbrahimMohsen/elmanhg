using Core.EntityFrameworkCore.Context;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator) : CoreDbContext<User, Role, Guid>(options, mediator)
{
    // Enum names are short identifiers; the column width is a schema invariant, not a tunable.
    private const int EnumColumnMaxLength = 50;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUsers(modelBuilder);
        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.Property(x => x.DisplayName).IsRequired();
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
        });
    }

    private static void ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
    }
}
