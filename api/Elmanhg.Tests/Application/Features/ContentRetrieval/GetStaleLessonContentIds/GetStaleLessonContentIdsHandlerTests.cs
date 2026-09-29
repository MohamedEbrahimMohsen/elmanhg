using Elmanhg.Application.ContentRetrieval.GetStaleLessonContentIds;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.GetStaleLessonContentIds;

public sealed class GetStaleLessonContentIdsHandlerTests
{
    private readonly ILessonContentIndexRepository _indexRepository = Substitute.For<ILessonContentIndexRepository>();

    [Fact]
    public async Task Handle_PassesExcludedIdsAndConfiguredBatchSize()
    {
        Guid[] excluded = [Guid.NewGuid(), Guid.NewGuid()];
        List<Guid> stale = [Guid.NewGuid()];
        _indexRepository.GetStaleLessonIdsAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.SequenceEqual(excluded)), 7, Arg.Any<CancellationToken>()).Returns(stale);
        var handler = new GetStaleLessonContentIdsHandler(_indexRepository, Options.Create(new ContentRetrievalOptions { IndexSweepBatchSize = 7 }));

        var result = await handler.Handle(new GetStaleLessonContentIdsQuery(excluded), TestContext.Current.CancellationToken);

        result.Should().Equal(stale);
        await _indexRepository.Received(1).GetStaleLessonIdsAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.SequenceEqual(excluded)), 7, Arg.Any<CancellationToken>());
    }
}
