using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class LessonEventRecorder(LessonEventLog log) : INotificationHandler<LessonPublished>, INotificationHandler<LessonUnpublished>, INotificationHandler<LessonArchived>
{
    public Task Handle(LessonPublished notification, CancellationToken cancellationToken)
    {
        log.Record(notification);
        return Task.CompletedTask;
    }

    public Task Handle(LessonUnpublished notification, CancellationToken cancellationToken)
    {
        log.Record(notification);
        return Task.CompletedTask;
    }

    public Task Handle(LessonArchived notification, CancellationToken cancellationToken)
    {
        log.Record(notification);
        return Task.CompletedTask;
    }
}
