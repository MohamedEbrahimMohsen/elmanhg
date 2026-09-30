using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.GetDueTrainingExportIds;
using Elmanhg.Domain.TrainingExports;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TrainingExports.GetDueTrainingExportIds;

public sealed class GetDueTrainingExportIdsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsDueIdsLimitedToBatchSize()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<ITrainingExportRepository>();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        List<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        repository.GetDueIdsAsync(now, 3, Arg.Any<CancellationToken>()).Returns(ids);
        var handler = new GetDueTrainingExportIdsHandler(repository, Options.Create(new TrainingExportsOptions { SweepBatchSize = 3 }), timeProvider);

        var result = await handler.Handle(new GetDueTrainingExportIdsQuery(), TestContext.Current.CancellationToken);

        result.Should().Equal(ids);
        await repository.Received(1).GetDueIdsAsync(now, 3, Arg.Any<CancellationToken>());
    }
}
