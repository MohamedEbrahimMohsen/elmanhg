using Elmanhg.Application.Dashboard.GetFunnelMetrics;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetFunnelMetrics;

public sealed class GetFunnelMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly IFunnelEventRepository _funnelEventRepository = Substitute.For<IFunnelEventRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetFunnelMetricsHandler _handler;

    public GetFunnelMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _funnelEventRepository.CountVisitorsByStepAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns([]);
        _funnelEventRepository.GetLandingToFirstAnswerTimingAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new FunnelTiming());
        _handler = new GetFunnelMetricsHandler(_funnelEventRepository, _timeProvider, Options.Create(new DashboardOptions()));
    }

    [Fact]
    public async Task Handle_OrdersStepsAndComputesConversionFromPrevious()
    {
        ReturnSteps(new FunnelStepCount(FunnelEventType.SignUpStarted, 4), new FunnelStepCount(FunnelEventType.LandingViewed, 8), new FunnelStepCount(FunnelEventType.SignUpCompleted, 3), new FunnelStepCount(FunnelEventType.FirstQuizAnswered, 1), new FunnelStepCount(FunnelEventType.OnboardingCompleted, 2));

        var result = await _handler.Handle(new GetFunnelMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.Steps.Should().Equal(
            new FunnelStepResult(FunnelEventType.LandingViewed, 8, null),
            new FunnelStepResult(FunnelEventType.SignUpStarted, 4, 0.5m),
            new FunnelStepResult(FunnelEventType.SignUpCompleted, 3, 0.75m),
            new FunnelStepResult(FunnelEventType.OnboardingCompleted, 2, 0.6667m),
            new FunnelStepResult(FunnelEventType.FirstQuizAnswered, 1, 0.5m));
        result.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_PreviousStepZero_ReturnsNullConversion()
    {
        ReturnSteps(new FunnelStepCount(FunnelEventType.LandingViewed, 5), new FunnelStepCount(FunnelEventType.SignUpCompleted, 2));

        var result = await _handler.Handle(new GetFunnelMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.Steps.Single(x => x.Type == FunnelEventType.SignUpCompleted).ConversionFromPrevious.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ZeroFillsMissingSteps()
    {
        ReturnSteps(new FunnelStepCount(FunnelEventType.LandingViewed, 5));

        var result = await _handler.Handle(new GetFunnelMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.Steps.Select(x => x.Type).Should().Equal(Enum.GetValues<FunnelEventType>());
        result.Steps.Skip(1).Should().OnlyContain(x => x.Visitors == 0);
    }

    [Fact]
    public async Task Handle_MapsTimingRoundedToSeconds()
    {
        _funnelEventRepository.GetLandingToFirstAnswerTimingAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new FunnelTiming { Completed = 2, MedianSeconds = 209.5 });

        var result = await _handler.Handle(new GetFunnelMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.CompletedJourneys.Should().Be(2);
        result.MedianLandingToFirstAnswerSeconds.Should().Be(210);
    }

    private void ReturnSteps(params FunnelStepCount[] steps)
    {
        _funnelEventRepository.CountVisitorsByStepAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(steps.ToList());
    }
}
