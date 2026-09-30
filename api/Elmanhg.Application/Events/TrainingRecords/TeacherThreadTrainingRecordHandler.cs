using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.TrainingData;
using MediatR;

namespace Elmanhg.Application.Events.TrainingRecords;

public sealed class TeacherThreadTrainingRecordHandler(ITeacherThreadTrainingRecordRepository teacherThreadTrainingRecordRepository, IStudentIdHasher studentIdHasher, TimeProvider timeProvider) : INotificationHandler<TeacherThreadClosed>, INotificationHandler<TeacherThreadRatedAfterClose>
{
    public Task Handle(TeacherThreadClosed notification, CancellationToken cancellationToken)
    {
        return AppendAsync(notification.Thread, TeacherThreadTrainingTrigger.Closed, notification.Thread.ClosedAt ?? throw new InvalidOperationException("A closed thread has no ClosedAt."), cancellationToken);
    }

    public Task Handle(TeacherThreadRatedAfterClose notification, CancellationToken cancellationToken)
    {
        return AppendAsync(notification.Thread, TeacherThreadTrainingTrigger.RatedAfterClose, notification.RatedAt, cancellationToken);
    }

    private async Task AppendAsync(TeacherThread thread, TeacherThreadTrainingTrigger trigger, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var record = TeacherThreadTrainingRecord.From(thread, trigger, studentIdHasher.Hash(thread.StudentId), occurredAt, timeProvider.GetUtcNow());
        await teacherThreadTrainingRecordRepository.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }
}
