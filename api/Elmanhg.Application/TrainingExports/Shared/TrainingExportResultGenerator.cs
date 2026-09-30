using Elmanhg.Domain.TrainingExports;

namespace Elmanhg.Application.TrainingExports.Shared;

public static class TrainingExportResultGenerator
{
    public static TrainingExportResult Generate(TrainingExport export)
    {
        return new TrainingExportResult(export.Id, export.Source, export.From, export.To, export.SubjectId, export.Status, export.Attempts, export.LastErrorCode, export.RowCount, export.FileSizeBytes, export.Sha256, export.DownloadFileName, export.RequestedAt, export.CompletedAt, export.ExpiresAt);
    }
}
