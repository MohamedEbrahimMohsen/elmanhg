using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class RowVersionConventionTests
{
    [Fact]
    public void ApplyRowVersionConvention_VersionedEntity_MapsVersionToXminRowVersion()
    {
        using var context = new CorePersistenceProbeDbContext();

        var version = context.Model.FindEntityType(typeof(VersionedProbe))!.FindProperty(nameof(VersionedProbe.Version))!;

        version.IsConcurrencyToken.Should().BeTrue();
        version.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
        version.GetColumnName().Should().Be("xmin");
        version.GetColumnType().Should().Be("xid");
    }

    [Fact]
    public void ApplyRowVersionConvention_UnversionedEntityWithVersionProperty_LeavesItPlain()
    {
        using var context = new CorePersistenceProbeDbContext();

        var version = context.Model.FindEntityType(typeof(UnversionedProbe))!.FindProperty(nameof(UnversionedProbe.Version))!;

        version.IsConcurrencyToken.Should().BeFalse();
        version.GetColumnName().Should().Be("Version");
    }

    [Fact]
    public void ApplyRowVersionConvention_OwnedVersionedType_LeavesItOwnedAndPlain()
    {
        using var context = new CorePersistenceProbeDbContext();

        var detail = context.Model.FindEntityType(typeof(VersionedProbeDetail))!;

        detail.IsOwned().Should().BeTrue();
        detail.FindProperty(nameof(VersionedProbeDetail.Version))!.IsConcurrencyToken.Should().BeFalse();
    }

    [Fact]
    public void ApplyRowVersionConvention_DerivedOfVersionedEntity_InheritsRowVersion()
    {
        using var context = new CorePersistenceProbeDbContext();

        var version = context.Model.FindEntityType(typeof(VersionedProbeDerived))!.FindProperty("Version")!;

        version.IsConcurrencyToken.Should().BeTrue();
        version.DeclaringType.ClrType.Should().Be(typeof(VersionedProbe));
    }
}
