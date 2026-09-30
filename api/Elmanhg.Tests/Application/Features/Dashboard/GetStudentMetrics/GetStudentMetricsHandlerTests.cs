using Elmanhg.Application.Dashboard.GetStudentMetrics;
using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetStudentMetrics;

public sealed class GetStudentMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 1, 15);
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserActivityDayRepository _userActivityDayRepository = Substitute.For<IUserActivityDayRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetStudentMetricsHandler _handler;

    public GetStudentMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _userActivityDayRepository.CountActiveStudentsByDayAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetStudentMetricsHandler(_userRepository, _userActivityDayRepository, _timeProvider, Options.Create(new DashboardOptions()));
    }

    [Fact]
    public async Task Handle_ReturnsRepositoryCountsForTheirWindows()
    {
        _userRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, bool>>>()).Returns(120, 9, 4);
        _userActivityDayRepository.CountActiveStudentsAsync(Today, Today, Arg.Any<CancellationToken>()).Returns(7);
        _userActivityDayRepository.CountActiveStudentsAsync(new DateOnly(2025, 12, 17), Today, Arg.Any<CancellationToken>()).Returns(55);

        var result = await _handler.Handle(new GetStudentMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.Total.Should().Be(120);
        result.NewInRange.Should().Be(9);
        result.NewThisWeek.Should().Be(4);
        result.ActiveToday.Should().Be(7);
        result.ActiveThisMonth.Should().Be(55);
        result.From.Should().Be(new DateOnly(2025, 12, 17));
        result.To.Should().Be(Today);
        result.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ZeroFillsDailyActiveForEveryDay()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 3);
        _userActivityDayRepository.CountActiveStudentsByDayAsync(from, to, Arg.Any<CancellationToken>()).Returns([new DailyTotal { Day = new DateOnly(2026, 1, 2), Value = 6 }]);

        var result = await _handler.Handle(new GetStudentMetricsQuery(from, to), TestContext.Current.CancellationToken);

        result.DailyActive.Should().Equal(new DailyValueResult(from, 0), new DailyValueResult(new DateOnly(2026, 1, 2), 6), new DailyValueResult(to, 0));
    }
}
