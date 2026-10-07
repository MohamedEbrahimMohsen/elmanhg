using Core.DDD.Time;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.TrainingExports;

public partial class TrainingExport
{
    public void BeginRun(string fileKey, DateTimeOffset startedAt, TimeSpan lease)
    {
        EnsurePending();
        var at = startedAt.TruncateToMicroseconds();
        FileKey = fileKey;
        Retry.Lease(at, lease);
        UpdationDate = at;
    }

    public void Complete(string fileKey, long rowCount, long fileSizeBytes, string sha256, DateTimeOffset completedAt, TimeSpan retention)
    {
        EnsurePending();
        var at = completedAt.TruncateToMicroseconds();
        Retry.RecordSuccess();
        FileKey = fileKey;
        RowCount = rowCount;
        FileSizeBytes = fileSizeBytes;
        Sha256 = sha256;
        CompletedAt = at;
        ExpiresAt = at + retention;
        Status = TrainingExportStatus.Completed;
        UpdationDate = at;
    }

    public void FailAttempt(string errorCode, DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay)
    {
        EnsurePending();
        var at = failedAt.TruncateToMicroseconds();
        if (Retry.RecordFailure(errorCode, at, maxAttempts, retryBaseDelay))
        {
            Status = TrainingExportStatus.Failed;
        }

        UpdationDate = at;
    }

    public void EnsureDownloadableAt(DateTimeOffset now)
    {
        if (Status == TrainingExportStatus.Expired || IsExpiredAt(now))
        {
            throw new ConflictCoreException(ErrorCodes.TrainingExportExpired);
        }

        if (Status != TrainingExportStatus.Completed)
        {
            throw new ConflictCoreException(ErrorCodes.TrainingExportNotReady);
        }
    }

    public void Expire(DateTimeOffset expiredAt)
    {
        if (Status != TrainingExportStatus.Completed)
        {
            throw new ConflictCoreException(ErrorCodes.TrainingExportNotReady);
        }

        FileKey = null;
        Status = TrainingExportStatus.Expired;
        UpdationDate = expiredAt.TruncateToMicroseconds();
    }

    public void DiscardFile(DateTimeOffset discardedAt)
    {
        if (Status != TrainingExportStatus.Failed)
        {
            throw new ConflictCoreException(ErrorCodes.TrainingExportNotReady);
        }

        FileKey = null;
        UpdationDate = discardedAt.TruncateToMicroseconds();
    }

    private void EnsurePending()
    {
        if (Status != TrainingExportStatus.Pending)
        {
            throw new ConflictCoreException(ErrorCodes.TrainingExportNotPending);
        }
    }
}
