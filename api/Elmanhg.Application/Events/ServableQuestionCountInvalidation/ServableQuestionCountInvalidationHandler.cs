using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace Elmanhg.Application.Events.ServableQuestionCountInvalidation;

public sealed class ServableQuestionCountInvalidationHandler(IMemoryCache memoryCache) : INotificationHandler<LessonPublished>, INotificationHandler<LessonUnpublished>, INotificationHandler<LessonArchived>, INotificationHandler<QuestionApproved>, INotificationHandler<QuestionRejected>, INotificationHandler<QuestionReturnedToPending>, INotificationHandler<QuestionRetired>
{
    public Task Handle(LessonPublished notification, CancellationToken cancellationToken)
    {
        return Invalidate();
    }

    public Task Handle(LessonUnpublished notification, CancellationToken cancellationToken)
    {
        return Invalidate();
    }

    public Task Handle(LessonArchived notification, CancellationToken cancellationToken)
    {
        return Invalidate();
    }

    public Task Handle(QuestionApproved notification, CancellationToken cancellationToken)
    {
        return Invalidate();
    }

    public Task Handle(QuestionRejected notification, CancellationToken cancellationToken)
    {
        return Invalidate();
    }

    public Task Handle(QuestionReturnedToPending notification, CancellationToken cancellationToken)
    {
        return Invalidate();
    }

    public Task Handle(QuestionRetired notification, CancellationToken cancellationToken)
    {
        return Invalidate();
    }

    private Task Invalidate()
    {
        memoryCache.Remove(ServableQuestionCountCache.Key);
        return Task.CompletedTask;
    }
}
