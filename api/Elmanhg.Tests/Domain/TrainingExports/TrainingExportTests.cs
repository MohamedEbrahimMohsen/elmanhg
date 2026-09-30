using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TrainingExports;

public sealed class TrainingExportTests
{
    private static readonly DateTimeOffset RequestedAt = TrainingExportBuilder.DefaultRequestedAt;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(60);

    [Fact]
    public void Request_ValidRange_IsPendingAndDueAtRequest()
    {
        var adminId = Guid.NewGuid();

        var export = new TrainingExportBuilder().RequestedBy(adminId).Build();

        (export.Status, export.Attempts, export.NextAttemptAt, export.RequestedAt, export.CreatedBy).Should().Be((TrainingExportStatus.Pending, 0, (DateTimeOffset?)RequestedAt, RequestedAt, (Guid?)adminId));
        (export.From, export.To, export.FileKey, export.ExpiresAt).Should().Be((TrainingExportBuilder.DefaultFrom, TrainingExportBuilder.DefaultTo, (string?)null, (DateTimeOffset?)null));
        export.IsDueAt(RequestedAt).Should().BeTrue();
    }

    [Fact]
    public void Request_FromNotBeforeTo_ThrowsArgumentException()
    {
        var act = () => TrainingExport.Request(TrainingExportSource.Attempts, TrainingExportBuilder.DefaultTo, TrainingExportBuilder.DefaultTo, null, Guid.NewGuid(), RequestedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Complete_Pending_StoresFileAndMarksCompleted()
    {
        var export = new TrainingExportBuilder().Build();
        var completedAt = RequestedAt.AddMinutes(2);

        export.Complete(TrainingExportBuilder.FileKey, 3, 512, TrainingExportBuilder.Sha256, completedAt.AddTicks(5), TrainingExportBuilder.Retention);

        (export.Status, export.FileKey, export.RowCount, export.FileSizeBytes, export.Sha256).Should().Be((TrainingExportStatus.Completed, (string?)TrainingExportBuilder.FileKey, (long?)3, (long?)512, (string?)TrainingExportBuilder.Sha256));
        (export.CompletedAt, export.ExpiresAt, export.NextAttemptAt, export.Attempts, export.UpdationDate).Should().Be(((DateTimeOffset?)completedAt, (DateTimeOffset?)completedAt.AddDays(7), (DateTimeOffset?)null, 1, completedAt));
    }

    [Fact]
    public void Complete_NotPending_ThrowsConflictTrainingExportNotPending()
    {
        var export = new TrainingExportBuilder().Completed().Build();

        var act = () => export.Complete(TrainingExportBuilder.FileKey, 1, 1, TrainingExportBuilder.Sha256, RequestedAt.AddMinutes(5), TrainingExportBuilder.Retention);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportNotPending);
    }

    [Fact]
    public void FailAttempt_BelowMax_SchedulesExponentialRetry()
    {
        var export = new TrainingExportBuilder().Build();
        var firstFailure = RequestedAt.AddSeconds(5);
        var secondFailure = RequestedAt.AddMinutes(5);

        export.FailAttempt("InvalidOperationException", firstFailure, 3, RetryBaseDelay);
        var firstRetry = export.NextAttemptAt;
        export.FailAttempt("InvalidOperationException", secondFailure, 3, RetryBaseDelay);

        firstRetry.Should().Be(firstFailure.AddSeconds(60));
        (export.NextAttemptAt, export.Attempts, export.LastErrorCode, export.Status).Should().Be(((DateTimeOffset?)secondFailure.AddSeconds(120), 2, "InvalidOperationException", TrainingExportStatus.Pending));
    }

    [Fact]
    public void FailAttempt_ReachesMax_MarksFailed()
    {
        var export = new TrainingExportBuilder().Build();

        export.FailAttempt("InvalidOperationException", RequestedAt.AddSeconds(5), 1, RetryBaseDelay);

        (export.Status, export.NextAttemptAt).Should().Be((TrainingExportStatus.Failed, (DateTimeOffset?)null));
    }

    [Fact]
    public void FailAttempt_LongErrorCode_TruncatesTo100()
    {
        var export = new TrainingExportBuilder().Build();

        export.FailAttempt(new string('X', 150), RequestedAt.AddSeconds(5), 3, RetryBaseDelay);

        export.LastErrorCode.Should().HaveLength(100);
    }

    [Fact]
    public void DownloadFileName_TeacherThreads_UsesSlugAndDates()
    {
        var export = new TrainingExportBuilder().WithSource(TrainingExportSource.TeacherThreads).Build();

        export.DownloadFileName.Should().Be("elmanhg-teacher-threads-2026-01-01-2026-02-01.jsonl");
    }
}
