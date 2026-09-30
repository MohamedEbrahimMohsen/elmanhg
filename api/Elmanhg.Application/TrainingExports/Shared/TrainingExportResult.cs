using Core.Auditing;
using Elmanhg.Domain.TrainingExports;

namespace Elmanhg.Application.TrainingExports.Shared;

public sealed record TrainingExportResult(Guid Id, TrainingExportSource Source, DateTimeOffset From, DateTimeOffset To, Guid? SubjectId, TrainingExportStatus Status, int Attempts, string? LastErrorCode, long? RowCount, long? FileSizeBytes, string? Sha256, string FileName, DateTimeOffset RequestedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}
