using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Analytics.RecordFunnelEvent;
using Elmanhg.Domain.Analytics;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Analytics.RecordFunnelEvent;

public sealed class RecordFunnelEventHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    private readonly IFunnelEventRepository _funnelEventRepository = Substitute.For<IFunnelEventRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly RecordFunnelEventHandler _handler;
    private FunnelEvent? _added;

    public RecordFunnelEventHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _funnelEventRepository.AddAsync(Arg.Do<FunnelEvent>(x => _added = x), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _handler = new RecordFunnelEventHandler(_funnelEventRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_Anonymous_AddsEventWithoutUser()
    {
        var anonymousId = Guid.NewGuid();
        _currentUserService.UserId.Returns((Guid?)null);

        await _handler.Handle(new RecordFunnelEventCommand(anonymousId, FunnelEventType.LandingViewed), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        _added!.AnonymousId.Should().Be(anonymousId);
        _added.Type.Should().Be(FunnelEventType.LandingViewed);
        _added.UserId.Should().BeNull();
        _added.OccurredAt.Should().Be(Now);
        await _funnelEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SignedIn_AddsEventWithUserId()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId);

        await _handler.Handle(new RecordFunnelEventCommand(Guid.NewGuid(), FunnelEventType.FirstQuizAnswered), TestContext.Current.CancellationToken);

        _added!.UserId.Should().Be(userId);
        await _funnelEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
