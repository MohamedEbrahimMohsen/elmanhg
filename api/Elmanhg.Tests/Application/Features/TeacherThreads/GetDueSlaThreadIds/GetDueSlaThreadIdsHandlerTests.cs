using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.GetDueSlaThreadIds;

public sealed class GetDueSlaThreadIdsHandlerTests
{
    [Fact]
    public async Task Handle_UsesClockOptionsAndExcludedIds_ReturnsRepositoryIds()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<ITeacherThreadRepository>();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);
        List<Guid> excluded = [Guid.NewGuid()];
        List<Guid> ids = [Guid.NewGuid(), Guid.NewGuid()];
        repository.GetSlaDueIdsAsync(now, TimeSpan.FromHours(24), TimeSpan.FromHours(12), TimeSpan.FromHours(20), excluded, 50, Arg.Any<CancellationToken>()).Returns(ids);
        var handler = new GetDueSlaThreadIdsHandler(repository, Options.Create(new AskTeacherOptions()), new FakeRuntimeSettings(subscriptions: new SubscriptionsOptions { AskTeacherReplySlaHours = 24 }, askTeacher: new AskTeacherOptions()), timeProvider);

        var result = await handler.Handle(new GetDueSlaThreadIdsQuery(excluded), TestContext.Current.CancellationToken);

        result.Should().Equal(ids);
        await repository.Received(1).GetSlaDueIdsAsync(now, TimeSpan.FromHours(24), TimeSpan.FromHours(12), TimeSpan.FromHours(20), excluded, 50, Arg.Any<CancellationToken>());
    }
}
