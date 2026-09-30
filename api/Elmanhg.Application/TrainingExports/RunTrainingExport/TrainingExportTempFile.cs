namespace Elmanhg.Application.TrainingExports.RunTrainingExport;

public static class TrainingExportTempFile
{
    public static FileStream Create() => new(Path.Combine(Path.GetTempPath(), $"elmanhg-training-export-{Guid.NewGuid():N}.jsonl"), new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None, Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose });
}
