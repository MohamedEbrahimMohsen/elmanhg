using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TrainingExports;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TrainingExports.FailTrainingExport;

public sealed class FailTrainingExportHandler(ITrainingExportRepository trainingExportRepository, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider) : IRequestHandler<FailTrainingExportCommand>
{
    public async Task Handle(FailTrainingExportCommand request, CancellationToken cancellationToken)
    {
        var export = await trainingExportRepository.FirstOrDefaultAsync(x => x.Id == request.ExportId, cancellationToken).ConfigureAwait(false);
        if (export is null || export.Status != TrainingExportStatus.Pending)
        {
            return;
        }

        var options = trainingExportsOptions.Value;
        export.FailAttempt(request.ErrorCode, timeProvider.GetUtcNow(), options.MaxAttempts, TimeSpan.FromSeconds(options.RetryBaseDelaySeconds));

        await trainingExportRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
