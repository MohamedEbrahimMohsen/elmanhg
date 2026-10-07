using Elmanhg.Application.Shared.Retries;

namespace Elmanhg.Application.TrainingExports.FailTrainingExport;

public sealed record FailTrainingExportCommand(Guid ExportId, string ErrorCode) : IFailRetriedWorkCommand
{
    Guid IFailRetriedWorkCommand.WorkId => ExportId;
}
