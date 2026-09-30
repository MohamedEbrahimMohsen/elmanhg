using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Shared.Analytics;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Elmanhg.Tests.Application.Features.Shared.Analytics;

public sealed record ActivityProbeRequest : IRequest<object>;

public sealed class UserActivityBehaviourTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserActivityDayRepository _userActivityDayRepository = Substitute.For<IUserActivityDayRepository>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly UserActivityBehaviour<ActivityProbeRequest, object> _behaviour;
    private readonly List<UserActivityDay> _recorded = [];
    private object _response = new();

    public UserActivityBehaviourTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _currentUserService.UserId.Returns(_userId);
        _userActivityDayRepository.AddIfAbsentAsync(Arg.Do<UserActivityDay>(_recorded.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _behaviour = new UserActivityBehaviour<ActivityProbeRequest, object>(_currentUserService, _userActivityDayRepository, _memoryCache, _timeProvider, Options.Create(new DashboardOptions()), NullLogger<UserActivityBehaviour<ActivityProbeRequest, object>>.Instance);
    }

    [Fact]
    public async Task Handle_AuthenticatedRequest_RecordsCairoTodayForUser()
    {
        var response = await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);

        response.Should().BeSameAs(_response);
        _recorded.Should().ContainSingle();
        _recorded[0].UserId.Should().Be(_userId);
        _recorded[0].Day.Should().Be(new DateOnly(2026, 1, 15));
        _recorded[0].FirstSeenAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_SameUserSameDayTwice_RecordsOnce()
    {
        await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);

        await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);

        await _userActivityDayRepository.Received(1).AddIfAbsentAsync(Arg.Any<UserActivityDay>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameUserNextDay_RecordsAgain()
    {
        await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);
        _timeProvider.GetUtcNow().Returns(Now.AddDays(1));

        await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);

        await _userActivityDayRepository.Received(2).AddIfAbsentAsync(Arg.Any<UserActivityDay>(), Arg.Any<CancellationToken>());
        _recorded.Select(x => x.Day).Should().Equal(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 16));
    }

    [Fact]
    public async Task Handle_AnonymousNonAuthResponse_RecordsNothing()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);

        await _userActivityDayRepository.DidNotReceive().AddIfAbsentAsync(Arg.Any<UserActivityDay>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AnonymousAuthResultResponse_RecordsAuthResultUser()
    {
        var signedInId = Guid.NewGuid();
        _currentUserService.UserId.Returns((Guid?)null);
        _response = new AuthResult("token", new AuthUserResult(signedInId, "Student", "Student", null, "student@example.com", false), "refresh");

        await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);

        _recorded.Should().ContainSingle().Which.UserId.Should().Be(signedInId);
    }

    [Fact]
    public async Task Handle_NextThrows_RecordsNothingAndRethrows()
    {
        var act = () => _behaviour.Handle(new ActivityProbeRequest(), _ => throw new InvalidOperationException("handler failed"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _userActivityDayRepository.DidNotReceive().AddIfAbsentAsync(Arg.Any<UserActivityDay>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RecordFails_ReturnsResponseAndRetriesOnNextRequest()
    {
        _userActivityDayRepository.AddIfAbsentAsync(Arg.Any<UserActivityDay>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));

        var response = await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);
        await _behaviour.Handle(new ActivityProbeRequest(), Next, TestContext.Current.CancellationToken);

        response.Should().BeSameAs(_response);
        await _userActivityDayRepository.Received(2).AddIfAbsentAsync(Arg.Any<UserActivityDay>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _memoryCache.Dispose();

    private Task<object> Next(CancellationToken cancellationToken) => Task.FromResult(_response);
}
