namespace Elmanhg.Application.TrainingExports.DownloadTrainingExport;

public sealed record TrainingExportFileResult(Stream Content, string ContentType, string FileName);
