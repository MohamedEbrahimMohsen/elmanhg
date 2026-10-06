using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Retries;
using Elmanhg.Domain.TrainingExports;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TrainingExports.FailTrainingExport;

public sealed class FailTrainingExportHandler(ITrainingExportRepository trainingExportRepository, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider) : FailRetriedWorkHandler<FailTrainingExportCommand, TrainingExport>(trainingExportRepository, timeProvider)
{
    protected override int MaxAttempts => trainingExportsOptions.Value.MaxAttempts;
    protected override TimeSpan RetryBaseDelay => TimeSpan.FromSeconds(trainingExportsOptions.Value.RetryBaseDelaySeconds);
}
