using Core.DDD.Entities;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class StampedProbe : AuditEntity
{
    public StampedProbe(Guid? createdBy) : base(Guid.NewGuid(), createdBy) { }

    public string Name { get; set; } = "first";
}

public sealed class AuditStampingProbeDbContext() : DbContext(new DbContextOptionsBuilder<AuditStampingProbeDbContext>().UseNpgsql("Host=localhost;Database=audit-stamping-probe").Options)
{
    public DbSet<StampedProbe> Probes { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}
