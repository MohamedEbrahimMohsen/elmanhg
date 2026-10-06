using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Tests.Core.Persistence;

public class ProbeAlpha
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public sealed class ProbeAlphaDerived : ProbeAlpha;

public sealed class ProbeBeta
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public class VersionedProbe : IVersioned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public uint Version { get; private set; }
    public VersionedProbeDetail Detail { get; set; } = new();
}

public sealed class VersionedProbeDerived : VersionedProbe;

public sealed class VersionedProbeDetail : IVersioned
{
    public uint Version { get; private set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class UnversionedProbe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public uint Version { get; private set; }
}

public sealed class CorePersistenceProbeDbContext() : DbContext(new DbContextOptionsBuilder<CorePersistenceProbeDbContext>().UseNpgsql("Host=localhost;Database=core-persistence-probe").Options)
{
    public DbSet<ProbeAlpha> Alphas { get; set; }
    public DbSet<ProbeBeta> Betas { get; set; }
    public DbSet<VersionedProbe> VersionedProbes { get; set; }
    public DbSet<UnversionedProbe> UnversionedProbes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeAlphaDerived>();
        modelBuilder.Entity<VersionedProbeDerived>();
        modelBuilder.Entity<VersionedProbe>().OwnsOne(x => x.Detail);
        modelBuilder.ApplyRowVersionConvention();
    }
}
