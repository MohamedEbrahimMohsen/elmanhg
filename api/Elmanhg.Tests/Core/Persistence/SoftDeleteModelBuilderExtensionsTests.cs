using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class SoftDeleteModelBuilderExtensionsTests
{
    [Fact]
    public void ApplySoftDeleteQueryFilters_SoftDeletableEntity_FiltersOutDeletedRows()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<ProbeEntity>();

        modelBuilder.ApplySoftDeleteQueryFilters();

        var filter = modelBuilder.Model.FindEntityType(typeof(ProbeEntity))!.GetDeclaredQueryFilters().Should().ContainSingle().Which.Expression!;
        var isVisible = (Func<ProbeEntity, bool>)filter.Compile();
        isVisible(new ProbeEntity(Guid.NewGuid()) { IsDeleted = true }).Should().BeFalse();
        isVisible(new ProbeEntity(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public void ApplySoftDeleteQueryFilters_PlainEntity_AddsNoFilter()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<PlainEntity>().HasKey(x => x.Id);

        modelBuilder.ApplySoftDeleteQueryFilters();

        modelBuilder.Model.FindEntityType(typeof(PlainEntity))!.GetDeclaredQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void ApplySoftDeleteQueryFilters_DerivedEntity_FiltersRootOnly()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<ProbeEntity>();
        modelBuilder.Entity<DerivedProbeEntity>().HasBaseType<ProbeEntity>();

        modelBuilder.ApplySoftDeleteQueryFilters();

        modelBuilder.Model.FindEntityType(typeof(ProbeEntity))!.GetDeclaredQueryFilters().Should().HaveCount(1);
        modelBuilder.Model.FindEntityType(typeof(DerivedProbeEntity))!.GetDeclaredQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void ApplySoftDeleteQueryFilters_ExistingFilter_KeepsIt()
    {
        var modelBuilder = new ModelBuilder();
        Expression<Func<ProbeEntity, bool>> existing = x => x.Id != Guid.Empty;
        modelBuilder.Entity<ProbeEntity>().HasQueryFilter(existing);

        modelBuilder.ApplySoftDeleteQueryFilters();

        modelBuilder.Model.FindEntityType(typeof(ProbeEntity))!.GetDeclaredQueryFilters().Should().ContainSingle().Which.Expression.Should().BeSameAs(existing);
    }

    private class ProbeEntity(Guid id) : Entity(id);

    private sealed class DerivedProbeEntity(Guid id) : ProbeEntity(id);

    private sealed class PlainEntity
    {
        public Guid Id { get; set; }
    }
}
