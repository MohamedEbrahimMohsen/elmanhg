using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.GetExpiredTrainingExportIds;
using Elmanhg.Domain.TrainingExports;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TrainingExports.GetExpiredTrainingExportIds;

public sealed class GetExpiredTrainingExportIdsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsExpiredIdsLimitedToBatchSizeAndExcludingDeferred()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<ITrainingExportRepository>();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        Guid[] excluded = [Guid.NewGuid()];
        List<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        repository.GetExpiredIdsAsync(now, 4, excluded, Arg.Any<CancellationToken>()).Returns(ids);
        var handler = new GetExpiredTrainingExportIdsHandler(repository, Options.Create(new TrainingExportsOptions { RetentionSweepBatchSize = 4 }), timeProvider);

        var result = await handler.Handle(new GetExpiredTrainingExportIdsQuery(excluded), TestContext.Current.CancellationToken);

        result.Should().Equal(ids);
        await repository.Received(1).GetExpiredIdsAsync(now, 4, excluded, Arg.Any<CancellationToken>());
    }
}
