using Core.DDD.Entities;
using Core.DDD.Models;
using Core.DDD.Time;
using Elmanhg.Domain.SharedKernel;
using System.Globalization;

namespace Elmanhg.Domain.TrainingExports;

public partial class TrainingExport : AuditEntity, IAuditedEntity, IRetriedWork, IVersioned
{
    public TrainingExportSource Source { get; private set; }
    public DateTimeOffset From { get; private set; }
    public DateTimeOffset To { get; private set; }
    public Guid? SubjectId { get; private set; }
    public TrainingExportStatus Status { get; private set; }
    public RetrySchedule Retry { get; private set; } = default!;
    public int Attempts => Retry.Attempts;
    public DateTimeOffset? NextAttemptAt => Retry.NextAttemptAt;
    public string? LastErrorCode => Retry.LastErrorCode;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? FileKey { get; private set; }
    public long? RowCount { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public string? Sha256 { get; private set; }
    public uint Version { get; private set; }

    public string DownloadFileName => string.Create(CultureInfo.InvariantCulture, $"elmanhg-{Source.ToFileSlug()}-{From:yyyy-MM-dd}-{To:yyyy-MM-dd}.jsonl");

    private TrainingExport(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static TrainingExport Request(TrainingExportSource source, DateTimeOffset from, DateTimeOffset to, Guid? subjectId, Guid requestedBy, DateTimeOffset requestedAt)
    {
        if (from >= to)
        {
            throw new ArgumentException("An export range must end after it starts.", nameof(to));
        }

        var at = requestedAt.TruncateToMicroseconds();
        return new TrainingExport(Guid.NewGuid(), requestedBy)
        {
            Source = source,
            From = from.TruncateToMicroseconds(),
            To = to.TruncateToMicroseconds(),
            SubjectId = subjectId,
            Status = TrainingExportStatus.Pending,
            Retry = RetrySchedule.DueAt(at),
            RequestedAt = at,
        };
    }

    public bool IsDueAt(DateTimeOffset now) => Status == TrainingExportStatus.Pending && Retry.IsDueAt(now);

    public bool IsPending => Status == TrainingExportStatus.Pending;

    public bool IsExpiredAt(DateTimeOffset now) => Status == TrainingExportStatus.Completed && ExpiresAt <= now;

    public bool HasFileToDeleteAt(DateTimeOffset now) => IsExpiredAt(now) || (Status == TrainingExportStatus.Failed && FileKey is not null);
}
