using Core.Storage;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Application.TrainingExports.Shared;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Domain.TrainingExports;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TrainingExports.RunTrainingExport;

public sealed class RunTrainingExportHandler(ITrainingExportRepository trainingExportRepository, IAttemptTrainingRecordRepository attemptRepository, IAvatarTrainingRecordRepository avatarRepository, ITeacherThreadTrainingRecordRepository threadRepository, IEssayGradeTrainingRecordRepository essayRepository, IStudentIdHasher studentIdHasher, IFileStorage fileStorage, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider) : IRequestHandler<RunTrainingExportCommand>
{
    public async Task Handle(RunTrainingExportCommand request, CancellationToken cancellationToken)
    {
        var export = await trainingExportRepository.FirstOrDefaultAsync(x => x.Id == request.ExportId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        if (export is null || !export.IsDueAt(now))
        {
            return;
        }

        var options = trainingExportsOptions.Value;
        var previousKey = export.FileKey;
        var key = previousKey ?? TrainingExportFiles.NewKey(export.Id);
        export.BeginRun(key, now, options.RunLease);
        // The key is persisted before any byte is uploaded, so no file ever exists without a row naming it; this save is also the xmin claim a second replica loses.
        await trainingExportRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (previousKey is not null)
        {
            await fileStorage.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        var filter = new TrainingRecordFilter(export.From, export.To, export.SubjectId);
        var batchSize = options.ReadBatchSize;
        await using var file = TrainingExportTempFile.Create();
        using var writer = new TrainingExportJsonlWriter(file);
        await (export.Source switch
        {
            TrainingExportSource.Attempts => TrainingExportSourceWriter.WriteAttemptsAsync(attemptRepository, filter, batchSize, writer, cancellationToken),
            TrainingExportSource.Avatar => TrainingExportSourceWriter.WriteAvatarAsync(avatarRepository, filter, batchSize, studentIdHasher, writer, cancellationToken),
            TrainingExportSource.TeacherThreads => TrainingExportSourceWriter.WriteTeacherThreadsAsync(threadRepository, filter, batchSize, studentIdHasher, writer, cancellationToken),
            TrainingExportSource.EssayGrades => TrainingExportSourceWriter.WriteEssayGradesAsync(essayRepository, filter, batchSize, writer, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), export.Source, "Unknown training export source."),
        }).ConfigureAwait(false);
        await file.FlushAsync(cancellationToken).ConfigureAwait(false);
        file.Position = 0;

        await fileStorage.SaveAsync(file, key, cancellationToken).ConfigureAwait(false);
        export.Complete(key, writer.RowCount, file.Length, writer.Sha256Hex(), timeProvider.GetUtcNow(), options.Retention);

        await trainingExportRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
