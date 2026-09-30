using Core.DDD.Entities;
using System.Globalization;

namespace Elmanhg.Domain.TrainingExports;

public partial class TrainingExport : AuditEntity, IAuditedEntity
{
    // Matches the LastErrorCode column width.
    private const int ErrorCodeMaxLength = 100;

    public TrainingExportSource Source { get; private set; }
    public DateTimeOffset From { get; private set; }
    public DateTimeOffset To { get; private set; }
    public Guid? SubjectId { get; private set; }
    public TrainingExportStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastErrorCode { get; private set; }
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

        var at = ToMicroseconds(requestedAt);
        return new TrainingExport(Guid.NewGuid(), requestedBy)
        {
            Source = source,
            From = ToMicroseconds(from),
            To = ToMicroseconds(to),
            SubjectId = subjectId,
            Status = TrainingExportStatus.Pending,
            Attempts = 0,
            NextAttemptAt = at,
            RequestedAt = at,
        };
    }

    public bool IsDueAt(DateTimeOffset now) => Status == TrainingExportStatus.Pending && NextAttemptAt <= now;

    public bool IsExpiredAt(DateTimeOffset now) => Status == TrainingExportStatus.Completed && ExpiresAt <= now;

    public bool HasFileToDeleteAt(DateTimeOffset now) => IsExpiredAt(now) || (Status == TrainingExportStatus.Failed && FileKey is not null);

    // timestamptz stores whole microseconds; truncating keeps the first response identical to later reads.
    private static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
}
