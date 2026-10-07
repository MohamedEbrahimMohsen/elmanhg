using Core.EntityFrameworkCore.Conflicts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static Elmanhg.Tests.Core.Persistence.ConflictMapProbe;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class ConflictMapUniqueTests
{
    [Fact]
    public void TryTranslate_UniqueViolationOnMappedConstraint_ReturnsConflictWithMappedCode()
    {
        var exception = new DbUpdateException("duplicate");
        var map = new ConflictMap(_ => ProbeViolation).MapUniqueConstraint("IX_Probe", ProbeTaken);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(ProbeTaken);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public void TryTranslate_UniqueViolationOnMappedTable_ReturnsConflictWithMappedCode()
    {
        var exception = new DbUpdateException("duplicate");
        var map = new ConflictMap(_ => new UniqueViolation("PK_Probes", "Probes")).MapUniqueTable("Probes", ProbesTableTaken);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(ProbesTableTaken);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public void TryTranslate_UniqueViolationOnUnmappedConstraint_ReturnsFalse()
    {
        var exception = new DbUpdateException("duplicate");
        var map = new ConflictMap(_ => new UniqueViolation("IX_Other", "Others")).MapUniqueConstraint("IX_Probe", ProbeTaken);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeFalse();
        conflict.Should().BeNull();
    }

    [Fact]
    public void TryTranslate_NoUniqueViolationDetected_ReturnsFalse()
    {
        var exception = new DbUpdateException("duplicate");
        var map = new ConflictMap(_ => null).MapUniqueConstraint("IX_Probe", ProbeTaken).MapUniqueTable("Probes", ProbesTableTaken);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeFalse();
        conflict.Should().BeNull();
    }

    [Fact]
    public void TryTranslate_ConstraintAndTableBothMatch_UsesFirstRegisteredMapping()
    {
        var exception = new DbUpdateException("duplicate");
        var map = new ConflictMap(_ => ProbeViolation).MapUniqueTable("Probes", ProbesTableTaken).MapUniqueConstraint("IX_Probe", ProbeTaken);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(ProbesTableTaken);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public void TryTranslate_ConstraintNameDiffersInCase_ReturnsFalse()
    {
        var exception = new DbUpdateException("duplicate");
        var map = new ConflictMap(_ => new UniqueViolation("ix_probe", null)).MapUniqueConstraint("IX_Probe", ProbeTaken);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeFalse();
        conflict.Should().BeNull();
    }
}
