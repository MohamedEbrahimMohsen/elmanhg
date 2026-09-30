using Elmanhg.Domain.TrainingExports;

namespace Elmanhg.Tests.Builders;

public sealed class TrainingExportBuilder
{
    public const string FileKey = "training-exports/0123456789abcdef0123456789abcdef-fedcba9876543210fedcba9876543210.jsonl";
    public const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    public static readonly DateTimeOffset DefaultRequestedAt = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DefaultFrom = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DefaultTo = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private TrainingExportSource _source = TrainingExportSource.TeacherThreads;
    private Guid? _subjectId;
    private Guid _requestedBy = Guid.NewGuid();
    private bool _completed;

    public static DateTimeOffset CompletedAt => DefaultRequestedAt.AddMinutes(1);

    public static DateTimeOffset ExpiresAt => CompletedAt + Retention;

    public TrainingExportBuilder WithSource(TrainingExportSource source)
    {
        _source = source;
        return this;
    }

    public TrainingExportBuilder ForSubject(Guid subjectId)
    {
        _subjectId = subjectId;
        return this;
    }

    public TrainingExportBuilder RequestedBy(Guid adminId)
    {
        _requestedBy = adminId;
        return this;
    }

    public TrainingExportBuilder Completed()
    {
        _completed = true;
        return this;
    }

    public TrainingExport Build()
    {
        var export = TrainingExport.Request(_source, DefaultFrom, DefaultTo, _subjectId, _requestedBy, DefaultRequestedAt);
        if (_completed)
        {
            export.Complete(FileKey, 2, 128, Sha256, CompletedAt, Retention);
        }

        return export;
    }
}
