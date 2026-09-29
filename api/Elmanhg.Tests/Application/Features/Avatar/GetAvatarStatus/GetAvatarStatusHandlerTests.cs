using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Avatar.GetAvatarStatus;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Avatar.GetAvatarStatus;

public sealed class GetAvatarStatusHandlerTests
{
    private static readonly DateTimeOffset Now = ExamSessionBuilder.Now.AddMinutes(5);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly IAvatarMessageUsageRepository _usageRepository = Substitute.For<IAvatarMessageUsageRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly GetAvatarStatusHandler _handler;

    public GetAvatarStatusHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(Now);
        AvatarTestData.StubSessions(_sessionRepository);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        AvatarTestData.StubUsedToday(_usageRepository, 0);
        _handler = new GetAvatarStatusHandler(_sessionRepository, _subscriptionRepository, _usageRepository, Options.Create(new AvatarOptions()), Options.Create(new SubscriptionsOptions()), Options.Create(new ExamsOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_FreeStudentWithTwoMessages_ReturnsCountsAndLimits()
    {
        AvatarTestData.StubUsedToday(_usageRepository, 2);

        var result = await HandleAsync();

        result.Should().Be(new AvatarStatusResult(false, PlanTier.Free, 5, 2, 3, 2000, 10));
    }

    [Fact]
    public async Task Handle_UsedOverLimit_ReturnsZeroRemaining()
    {
        AvatarTestData.StubUsedToday(_usageRepository, 7);

        var result = await HandleAsync();

        result.MessagesUsedToday.Should().Be(7);
        result.MessagesRemainingToday.Should().Be(0);
    }

    [Fact]
    public async Task Handle_BaseStudent_ReturnsBaseLimit()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_builder.StudentId, Now));

        var result = await HandleAsync();

        result.Tier.Should().Be(PlanTier.Base);
        result.DailyMessageLimit.Should().Be(50);
        result.MessagesRemainingToday.Should().Be(50);
    }

    [Fact]
    public async Task Handle_ExamInProgress_ReturnsExamInProgressTrue()
    {
        AvatarTestData.StubSessions(_sessionRepository, _builder.Build());

        var result = await HandleAsync();

        result.ExamInProgress.Should().BeTrue();
    }

    private Task<AvatarStatusResult> HandleAsync() => _handler.Handle(new GetAvatarStatusQuery(), TestContext.Current.CancellationToken);
}
