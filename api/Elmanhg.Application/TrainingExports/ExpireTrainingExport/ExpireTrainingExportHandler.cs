using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.TrainingExports;
using MediatR;

namespace Elmanhg.Application.TrainingExports.ExpireTrainingExport;

public sealed class ExpireTrainingExportHandler(ITrainingExportRepository trainingExportRepository, IFileStorage fileStorage, TimeProvider timeProvider) : IRequestHandler<ExpireTrainingExportCommand>
{
    public async Task Handle(ExpireTrainingExportCommand request, CancellationToken cancellationToken)
    {
        var export = await trainingExportRepository.FirstOrDefaultAsync(x => x.Id == request.ExportId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        if (export is null || !export.HasFileToDeleteAt(now))
        {
            return;
        }

        if (export.FileKey is { } fileKey)
        {
            await fileStorage.DeleteAsync(fileKey, cancellationToken).ConfigureAwait(false);
        }

        if (export.Status == TrainingExportStatus.Failed)
        {
            export.DiscardFile(now);
        }
        else
        {
            export.Expire(now);
        }

        await trainingExportRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
