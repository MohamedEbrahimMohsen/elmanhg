using Core.DDD.Entities;
using Elmanhg.Application.Events.ServableQuestionCountInvalidation;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace Elmanhg.Tests.Application.Features.Events;

public sealed class ServableQuestionCountInvalidationHandlerTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly ServableQuestionCountInvalidationHandler _handler;

    public ServableQuestionCountInvalidationHandlerTests()
    {
        _handler = new ServableQuestionCountInvalidationHandler(_memoryCache);
    }

    public static TheoryData<DomainEvent> ServabilityEvents =>
    [
        new LessonPublished(Guid.NewGuid(), Guid.NewGuid()),
        new LessonUnpublished(Guid.NewGuid(), Guid.NewGuid()),
        new LessonArchived(Guid.NewGuid(), Guid.NewGuid()),
        new QuestionApproved(Guid.NewGuid(), Guid.NewGuid()),
        new QuestionRejected(Guid.NewGuid(), Guid.NewGuid()),
        new QuestionReturnedToPending(Guid.NewGuid(), Guid.NewGuid()),
        new QuestionRetired(Guid.NewGuid(), Guid.NewGuid()),
    ];

    [Theory]
    [MemberData(nameof(ServabilityEvents))]
    public async Task Handle_ServabilityEvent_RemovesCachedCount(DomainEvent domainEvent)
    {
        _memoryCache.Set(ServableQuestionCountCache.Key, 5);

        await Dispatch(domainEvent);

        _memoryCache.TryGetValue(ServableQuestionCountCache.Key, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnrelatedCacheEntry_IsKept()
    {
        _memoryCache.Set("other", 1);

        await _handler.Handle(new QuestionRetired(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        _memoryCache.TryGetValue("other", out _).Should().BeTrue();
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    private Task Dispatch(DomainEvent domainEvent)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return domainEvent switch
        {
            LessonPublished x => _handler.Handle(x, cancellationToken),
            LessonUnpublished x => _handler.Handle(x, cancellationToken),
            LessonArchived x => _handler.Handle(x, cancellationToken),
            QuestionApproved x => _handler.Handle(x, cancellationToken),
            QuestionRejected x => _handler.Handle(x, cancellationToken),
            QuestionReturnedToPending x => _handler.Handle(x, cancellationToken),
            QuestionRetired x => _handler.Handle(x, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(domainEvent)),
        };
    }
}
