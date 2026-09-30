using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TrainingExports;

public sealed class TrainingExportRunTests
{
    private static readonly DateTimeOffset StartedAt = TrainingExportBuilder.DefaultRequestedAt.AddSeconds(5);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);

    [Fact]
    public void BeginRun_Pending_RecordsKeyAndLeasesTheExport()
    {
        var export = new TrainingExportBuilder().Build();

        export.BeginRun(TrainingExportBuilder.FileKey, StartedAt.AddTicks(5), Lease);

        (export.Status, export.FileKey, export.Attempts, export.NextAttemptAt, export.UpdationDate).Should().Be((TrainingExportStatus.Pending, TrainingExportBuilder.FileKey, 0, (DateTimeOffset?)(StartedAt + Lease), StartedAt));
        (export.IsDueAt(StartedAt + Lease - TimeSpan.FromTicks(10)), export.IsDueAt(StartedAt + Lease)).Should().Be((false, true));
    }

    [Fact]
    public void BeginRun_Completed_ThrowsConflictTrainingExportNotPending()
    {
        var export = new TrainingExportBuilder().Completed().Build();

        var act = () => export.BeginRun(TrainingExportBuilder.FileKey, StartedAt, Lease);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportNotPending);
    }

    [Fact]
    public void FailAttempt_LastAttemptAfterBeginRun_KeepsKeyForTheSweep()
    {
        var export = new TrainingExportBuilder().Build();
        export.BeginRun(TrainingExportBuilder.FileKey, StartedAt, Lease);

        export.FailAttempt("IOException", StartedAt.AddSeconds(1), 1, TimeSpan.FromSeconds(60));

        (export.Status, export.FileKey).Should().Be((TrainingExportStatus.Failed, TrainingExportBuilder.FileKey));
        export.HasFileToDeleteAt(StartedAt.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void HasFileToDeleteAt_FailedWithoutKeyOrPendingWithKey_ReturnsFalse()
    {
        var failed = new TrainingExportBuilder().Build();
        failed.FailAttempt("IOException", StartedAt, 1, TimeSpan.FromSeconds(60));
        var running = new TrainingExportBuilder().Build();
        running.BeginRun(TrainingExportBuilder.FileKey, StartedAt, Lease);

        (failed.HasFileToDeleteAt(StartedAt.AddYears(1)), running.HasFileToDeleteAt(StartedAt.AddYears(1))).Should().Be((false, false));
    }

    [Fact]
    public void DiscardFile_FailedWithKey_ClearsKeyAndStaysFailed()
    {
        var export = new TrainingExportBuilder().Build();
        export.BeginRun(TrainingExportBuilder.FileKey, StartedAt, Lease);
        export.FailAttempt("IOException", StartedAt, 1, TimeSpan.FromSeconds(60));

        export.DiscardFile(StartedAt.AddMinutes(1));

        (export.Status, export.FileKey, export.LastErrorCode, export.UpdationDate).Should().Be((TrainingExportStatus.Failed, (string?)null, "IOException", StartedAt.AddMinutes(1)));
        export.HasFileToDeleteAt(StartedAt.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void DiscardFile_Completed_ThrowsConflictTrainingExportNotReady()
    {
        var export = new TrainingExportBuilder().Completed().Build();

        var act = () => export.DiscardFile(StartedAt);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportNotReady);
    }
}
