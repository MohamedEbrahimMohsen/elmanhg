using Elmanhg.Application.EssayGrading.GetDueEssayGradeIds;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.EssayGrading.GetDueEssayGradeIds;

public sealed class GetDueEssayGradeIdsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsDueIdsLimitedToBatchSize()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<IEssayGradeRepository>();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        List<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        repository.GetDueIdsAsync(now, 7, Arg.Any<CancellationToken>()).Returns(ids);
        var handler = new GetDueEssayGradeIdsHandler(repository, Options.Create(new EssayGradingOptions { SweepBatchSize = 7 }), timeProvider);

        var result = await handler.Handle(new GetDueEssayGradeIdsQuery(), TestContext.Current.CancellationToken);

        result.Should().Equal(ids);
        await repository.Received(1).GetDueIdsAsync(now, 7, Arg.Any<CancellationToken>());
    }
}
