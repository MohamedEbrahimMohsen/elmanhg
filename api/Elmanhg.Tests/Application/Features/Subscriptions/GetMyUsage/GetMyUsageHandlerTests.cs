using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GetMyUsage;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GetMyUsage;

public sealed class GetMyUsageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly GetMyUsageHandler _handler;

    public GetMyUsageHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new GetMyUsageHandler(_subscriptionRepository, _sessionRepository, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle();

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_FreeStudent_ReturnsLimitUsedAndRemaining()
    {
        StubUsed(3);

        var result = await Handle();

        result.Should().Be(new UsageResult(PlanTier.Free, false, 10, 3, 7, 5));
    }

    [Fact]
    public async Task Handle_FreeStudentOverLimit_ReturnsZeroRemaining()
    {
        StubUsed(12);

        var result = await Handle();

        (result.QuizQuestionsUsedToday, result.QuizQuestionsRemainingToday).Should().Be((12, (int?)0));
    }

    [Fact]
    public async Task Handle_SubscribedStudent_ReturnsUnlimited()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));
        StubUsed(40);

        var result = await Handle();

        (result.Tier, result.DailyQuizQuestionLimit, result.QuizQuestionsRemainingToday, result.DailyAvatarMessageLimit).Should().Be((PlanTier.Base, (int?)null, (int?)null, 50));
    }

    private Task<UsageResult> Handle() => _handler.Handle(new GetMyUsageQuery(), TestContext.Current.CancellationToken);

    private void StubUsed(int used) => _sessionRepository.CountQuizAttemptsOnDayAsync(_studentId, "Africa/Cairo", new DateOnly(2026, 10, 1), Arg.Any<CancellationToken>()).Returns(used);
}
