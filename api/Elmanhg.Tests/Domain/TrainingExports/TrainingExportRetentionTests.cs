using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TrainingExports;

public sealed class TrainingExportRetentionTests
{
    private static readonly DateTimeOffset ExpiresAt = TrainingExportBuilder.ExpiresAt;

    [Fact]
    public void EnsureDownloadableAt_CompletedBeforeExpiry_DoesNotThrow()
    {
        var export = new TrainingExportBuilder().Completed().Build();

        var act = () => export.EnsureDownloadableAt(ExpiresAt.AddTicks(-10));

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureDownloadableAt_Pending_ThrowsConflictTrainingExportNotReady()
    {
        var export = new TrainingExportBuilder().Build();

        var act = () => export.EnsureDownloadableAt(TrainingExportBuilder.DefaultRequestedAt);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportNotReady);
    }

    [Fact]
    public void EnsureDownloadableAt_AtExpiryInstant_ThrowsConflictTrainingExportExpired()
    {
        var export = new TrainingExportBuilder().Completed().Build();

        var act = () => export.EnsureDownloadableAt(ExpiresAt);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportExpired);
    }

    [Fact]
    public void EnsureDownloadableAt_ExpiredExport_ThrowsConflictTrainingExportExpired()
    {
        var export = new TrainingExportBuilder().Completed().Build();
        export.Expire(ExpiresAt);

        var act = () => export.EnsureDownloadableAt(ExpiresAt.AddDays(-1));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportExpired);
    }

    [Fact]
    public void IsExpiredAt_CompletedAtOrAfterExpiry_ReturnsTrueOnlyFromTheExpiryInstant()
    {
        var export = new TrainingExportBuilder().Completed().Build();

        (export.IsExpiredAt(ExpiresAt.AddTicks(-10)), export.IsExpiredAt(ExpiresAt)).Should().Be((false, true));
    }

    [Fact]
    public void IsExpiredAt_Pending_ReturnsFalse()
    {
        var export = new TrainingExportBuilder().Build();

        export.IsExpiredAt(ExpiresAt.AddYears(1)).Should().BeFalse();
    }

    [Fact]
    public void Expire_Completed_ClearsFileKeyAndMarksExpired()
    {
        var export = new TrainingExportBuilder().Completed().Build();

        export.Expire(ExpiresAt.AddMinutes(1));

        (export.Status, export.FileKey, export.UpdationDate).Should().Be((TrainingExportStatus.Expired, (string?)null, ExpiresAt.AddMinutes(1)));
        (export.RowCount, export.Sha256).Should().Be(((long?)2, (string?)TrainingExportBuilder.Sha256));
    }

    [Fact]
    public void Expire_Pending_ThrowsConflictTrainingExportNotReady()
    {
        var export = new TrainingExportBuilder().Build();

        var act = () => export.Expire(ExpiresAt);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportNotReady);
    }
}
