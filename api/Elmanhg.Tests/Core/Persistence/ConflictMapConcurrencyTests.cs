using Core.EntityFrameworkCore.Conflicts;
using FluentAssertions;
using static Elmanhg.Tests.Core.Persistence.ConflictMapProbe;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class ConflictMapConcurrencyTests
{
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
    public void TryTranslate_ConcurrencyAndUniqueRulesBothMatch_ReturnsConcurrencyCode()
    {
        using var context = new CorePersistenceProbeDbContext();
        var exception = ConcurrencyFailures.For(context, new ProbeAlpha());
        var map = new ConflictMap(_ => ProbeViolation).MapUniqueConstraint("IX_Probe", ProbeTaken).MapConcurrency<ProbeAlpha>(AlphaStale);

        var translated = map.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(AlphaStale);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }
}
