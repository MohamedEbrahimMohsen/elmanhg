using Elmanhg.Application.MathStepGrading.GetDueMathStepGradeIds;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.MathStepGrading;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.GetDueMathStepGradeIds;

public sealed class GetDueMathStepGradeIdsHandlerTests
{
    [Fact]
    public async Task Handle_PassesNowAndBatchSize()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<IMathStepGradeRepository>();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        List<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        repository.GetDueIdsAsync(now, 7, Arg.Any<CancellationToken>()).Returns(ids);
        var handler = new GetDueMathStepGradeIdsHandler(repository, Options.Create(new MathStepGradingOptions { SweepBatchSize = 7 }), timeProvider);

        var result = await handler.Handle(new GetDueMathStepGradeIdsQuery(), TestContext.Current.CancellationToken);

        result.Should().Equal(ids);
        await repository.Received(1).GetDueIdsAsync(now, 7, Arg.Any<CancellationToken>());
    }
}
