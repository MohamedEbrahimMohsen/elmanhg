using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Application.TrainingExports.Shared;
using Elmanhg.Domain.TrainingData;

namespace Elmanhg.Application.TrainingExports.RunTrainingExport;

public static class TrainingExportSourceWriter
{
    public static Task WriteAttemptsAsync(IAttemptTrainingRecordRepository repository, TrainingRecordFilter filter, int batchSize, TrainingExportJsonlWriter writer, CancellationToken cancellationToken)
    {
        return WriteAsync(after => repository.GetExportPageAsync(filter, after, batchSize, cancellationToken), x => new TrainingRecordCursor(x.OccurredAt, x.Id), TrainingExportLineGenerator.Attempt, batchSize, writer, cancellationToken);
    }

    public static Task WriteAvatarAsync(IAvatarTrainingRecordRepository repository, TrainingRecordFilter filter, int batchSize, IStudentIdHasher studentIdHasher, TrainingExportJsonlWriter writer, CancellationToken cancellationToken)
    {
        return WriteAsync(after => repository.GetExportPageAsync(filter, after, batchSize, cancellationToken), x => new TrainingRecordCursor(x.OccurredAt, x.Id), x => TrainingExportLineGenerator.Avatar(x, studentIdHasher), batchSize, writer, cancellationToken);
    }

    public static Task WriteTeacherThreadsAsync(ITeacherThreadTrainingRecordRepository repository, TrainingRecordFilter filter, int batchSize, IStudentIdHasher studentIdHasher, TrainingExportJsonlWriter writer, CancellationToken cancellationToken)
    {
        return WriteAsync(after => repository.GetExportPageAsync(filter, after, batchSize, cancellationToken), x => new TrainingRecordCursor(x.OccurredAt, x.Id), x => TrainingExportLineGenerator.TeacherThread(x, studentIdHasher), batchSize, writer, cancellationToken);
    }

    public static Task WriteEssayGradesAsync(IEssayGradeTrainingRecordRepository repository, TrainingRecordFilter filter, int batchSize, TrainingExportJsonlWriter writer, CancellationToken cancellationToken)
    {
        return WriteAsync(after => repository.GetExportPageAsync(filter, after, batchSize, cancellationToken), x => new TrainingRecordCursor(x.OccurredAt, x.Id), TrainingExportLineGenerator.EssayGrade, batchSize, writer, cancellationToken);
    }

    private static async Task WriteAsync<TRecord, TLine>(Func<TrainingRecordCursor?, Task<List<TRecord>>> readPage, Func<TRecord, TrainingRecordCursor> cursorOf, Func<TRecord, TLine> toLine, int batchSize, TrainingExportJsonlWriter writer, CancellationToken cancellationToken)
    {
        TrainingRecordCursor? after = null;
        List<TRecord> page;
        do
        {
            page = await readPage(after).ConfigureAwait(false);
            foreach (var record in page)
            {
                await writer.WriteAsync(toLine(record), cancellationToken).ConfigureAwait(false);
            }

            if (page.Count > 0)
            {
                after = cursorOf(page[^1]);
            }
        }
        while (page.Count == batchSize);
    }
}
