using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.GetDueVoiceDraftIds;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.GetDueVoiceDraftIds;

public sealed class GetDueVoiceDraftIdsHandlerTests
{
    [Fact]
    public async Task Handle_UsesClockAndBatchSize_ReturnsRepositoryIds()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<ITeacherVoiceDraftRepository>();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        List<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        repository.GetDueIdsAsync(now, 5, Arg.Any<CancellationToken>()).Returns(ids);
        var handler = new GetDueVoiceDraftIdsHandler(repository, Options.Create(new AskTeacherOptions { TranscriptionSweepBatchSize = 5 }), timeProvider);

        var result = await handler.Handle(new GetDueVoiceDraftIdsQuery(), TestContext.Current.CancellationToken);

        result.Should().Equal(ids);
        await repository.Received(1).GetDueIdsAsync(now, 5, Arg.Any<CancellationToken>());
    }
}
