using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GetMyEntitlement;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GetMyEntitlement;

public sealed class GetMyEntitlementHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RecentStart = Now.AddDays(-5);
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Subscription> _subscriptions = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly GetMyEntitlementHandler _handler;

    public GetMyEntitlementHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _subscriptionRepository.FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.Where(call.Arg<Expression<Func<Subscription, bool>>>().Compile()).ToList());
        _handler = new GetMyEntitlementHandler(_subscriptionRepository, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetMyEntitlementQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_NoSubscriptions_ReturnsFreeLimits()
    {
        var result = await Handle();

        result.Should().BeEquivalentTo(new EntitlementResult(PlanTier.Free, false, false, 10, 5, 1, 0, []));
    }

    [Fact]
    public async Task Handle_EntitledBase_ReturnsUnlimitedQuizzesAndBaseAvatarLimit()
    {
        var baseSubscription = Add(SubscriptionPlan.Base, SubscriptionStatus.Active, RecentStart);

        var result = await Handle();

        (result.Tier, result.CanTakeExams, result.DailyQuizQuestionLimit, result.OpenLessonsPerUnit, result.DailyAvatarMessageLimit, result.MonthlyAskTeacherQuestionLimit).Should().Be((PlanTier.Base, true, (int?)null, (int?)null, 50, 0));
        result.Subscriptions.Should().Equal(new SubscriptionResult(baseSubscription.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, SubscriptionStatus.Active, RecentStart, RecentStart.AddMonths(1), RecentStart.AddMonths(1).AddDays(3), null));
    }

    [Fact]
    public async Task Handle_BaseAndAskTeacher_ReturnsAskTeacherQuota()
    {
        Add(SubscriptionPlan.Base, SubscriptionStatus.Active, RecentStart);
        Add(SubscriptionPlan.AskTeacher, SubscriptionStatus.Active, RecentStart);

        var result = await Handle();

        (result.HasAskTeacher, result.MonthlyAskTeacherQuestionLimit, result.Subscriptions.Count).Should().Be((true, 20, 2));
    }

    [Fact]
    public async Task Handle_PastDueBaseWithinGrace_ReturnsBase()
    {
        Add(SubscriptionPlan.Base, SubscriptionStatus.PastDue, Now.AddMonths(-1).AddDays(-1));

        var result = await Handle();

        result.Tier.Should().Be(PlanTier.Base);
        result.Subscriptions.Select(x => x.Status).Should().Equal(SubscriptionStatus.PastDue);
    }

    [Fact]
    public async Task Handle_BaseLapsedBeyondGrace_ReturnsFree()
    {
        Add(SubscriptionPlan.Base, SubscriptionStatus.Active, Now.AddMonths(-1).AddDays(-4));

        var result = await Handle();

        result.Tier.Should().Be(PlanTier.Free);
    }

    [Fact]
    public async Task Handle_OtherStudentsSubscription_ReturnsFree()
    {
        _subscriptions.Add(new SubscriptionBuilder().StartingAt(RecentStart).Build());

        var result = await Handle();

        result.Tier.Should().Be(PlanTier.Free);
    }

    private Task<EntitlementResult> Handle() => _handler.Handle(new GetMyEntitlementQuery(), TestContext.Current.CancellationToken);

    private Subscription Add(SubscriptionPlan plan, SubscriptionStatus status, DateTimeOffset start)
    {
        var subscription = new SubscriptionBuilder().ForStudent(_studentId).WithPlan(plan).InStatus(status).StartingAt(start).Build();
        _subscriptions.Add(subscription);
        return subscription;
    }
}
