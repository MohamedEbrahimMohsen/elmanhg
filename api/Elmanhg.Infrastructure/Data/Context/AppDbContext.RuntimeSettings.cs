using Elmanhg.Domain.RuntimeSettings;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public const string RuntimeSettingKeyIndex = "IX_RuntimeSettingOverrides_Key";

    // Setting keys are short dotted identifiers ("group.name"); a schema invariant.
    private const int RuntimeSettingKeyMaxLength = 100;

    public DbSet<RuntimeSettingOverride> RuntimeSettingOverrides { get; set; }

    private static void ConfigureRuntimeSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RuntimeSettingOverride>(builder =>
        {
            builder.Property(x => x.Key).IsRequired().HasMaxLength(RuntimeSettingKeyMaxLength);
            builder.Property(x => x.Value).HasColumnType("jsonb");
            builder.Property(x => x.Version).IsRowVersion();
            builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName(RuntimeSettingKeyIndex);
        });
    }
}
