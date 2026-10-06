using Core.EntityFrameworkCore.Conflicts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class ConflictMapTests
{
    private const string AlphaStale = "ALPHA_STALE";
    private const string BetaStale = "BETA_STALE";
    private const string ProbeTaken = "PROBE_TAKEN";
    private const string ProbesTableTaken = "PROBES_TABLE_TAKEN";

    private static readonly UniqueViolation ProbeViolation = new("IX_Probe", "Probes");

    [Fact]
    public void TryTranslate_ConcurrencyOnMappedEntity_ReturnsConflictWithMappedCode()
    {
        using var context = new CorePersistenceProbeDbContext();
        var exception = ConcurrencyFailures.For(context, new ProbeAlpha());
        var map = new ConflictMap(_ => null).MapConcurrency<ProbeAlpha>(AlphaStale);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(AlphaStale);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public void TryTranslate_ConcurrencyOnDerivedEntity_UsesBaseTypeMapping()
    {
        using var context = new CorePersistenceProbeDbContext();
        var exception = ConcurrencyFailures.For(context, new ProbeAlphaDerived());
        var map = new ConflictMap(_ => null).MapConcurrency<ProbeAlpha>(AlphaStale);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(AlphaStale);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public void TryTranslate_ConcurrencyOnSeveralMappedEntities_UsesFirstRegisteredMapping()
    {
        using var context = new CorePersistenceProbeDbContext();
        var exception = ConcurrencyFailures.For(context, new ProbeBeta(), new ProbeAlpha());
        var map = new ConflictMap(_ => null).MapConcurrency<ProbeAlpha>(AlphaStale).MapConcurrency<ProbeBeta>(BetaStale);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(AlphaStale);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public void TryTranslate_ConcurrencyOnUnmappedEntity_ReturnsFalse()
    {
        using var context = new CorePersistenceProbeDbContext();
        var exception = ConcurrencyFailures.For(context, new ProbeAlpha());
        var map = new ConflictMap(_ => null).MapConcurrency<ProbeBeta>(BetaStale);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeFalse();
        conflict.Should().BeNull();
    }

    [Fact]
    public void TryTranslate_UnmappedConcurrencyWithMappedUniqueViolation_ReturnsUniqueCode()
    {
        using var context = new CorePersistenceProbeDbContext();
        var exception = ConcurrencyFailures.For(context, new ProbeAlpha());
        var map = new ConflictMap(_ => ProbeViolation).MapConcurrency<ProbeBeta>(BetaStale).MapUniqueConstraint("IX_Probe", ProbeTaken);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(ProbeTaken);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

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
