using Core.Errors;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.TrainingExports;
using MediatR;

namespace Elmanhg.Application.TrainingExports.DownloadTrainingExport;

public sealed class DownloadTrainingExportHandler(ITrainingExportRepository trainingExportRepository, IFileStorage fileStorage, TimeProvider timeProvider) : IRequestHandler<DownloadTrainingExportQuery, TrainingExportFileResult>
{
    public async Task<TrainingExportFileResult> Handle(DownloadTrainingExportQuery request, CancellationToken cancellationToken)
    {
        var export = await trainingExportRepository.FirstOrDefaultAsync(x => x.Id == request.ExportId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TrainingExportNotFound);
        export.EnsureDownloadableAt(timeProvider.GetUtcNow());

        var key = export.FileKey ?? throw new NotFoundCoreException(ErrorCodes.TrainingExportNotFound);
        var file = await fileStorage.OpenReadAsync(key, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TrainingExportNotFound);
        return new TrainingExportFileResult(file.Content, file.ContentType, export.DownloadFileName);
    }
}
