namespace Elmanhg.Application.TrainingExports.Shared;

public static class TrainingExportFiles
{
    public const string StorageFolder = "training-exports";

    public static string NewKey(Guid exportId) => $"{StorageFolder}/{exportId:N}-{Guid.NewGuid():N}.jsonl";
}
