using Core.Errors;
using Elmanhg.Application.Dashboard.GetSolveRateMetrics;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subjects;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetSolveRateMetrics;

public sealed class GetSolveRateMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Yesterday = new(2026, 1, 14);
    private static readonly DateOnly Today = new(2026, 1, 15);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IUserActivityDayRepository _userActivityDayRepository = Substitute.For<IUserActivityDayRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetSolveRateMetricsHandler _handler;

    public GetSolveRateMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _sessionRepository.CountAttemptsByDayAsync(Arg.Any<MetricsWindow>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([]);
        _userActivityDayRepository.CountActiveStudentsByDayAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetSolveRateMetricsHandler(_sessionRepository, _userActivityDayRepository, _subjectRepository, _timeProvider, Options.Create(new DashboardOptions()));
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetSolveRateMetricsQuery(null, null, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _sessionRepository.DidNotReceive().CountAttemptsByDayAsync(Arg.Any<MetricsWindow>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DividesAttemptsByActiveStudentDays()
    {
        _sessionRepository.CountAttemptsByDayAsync(Arg.Any<MetricsWindow>(), null, Arg.Any<CancellationToken>()).Returns([Total(Yesterday, 30), Total(Today, 20)]);
        _userActivityDayRepository.CountActiveStudentsByDayAsync(Yesterday, Today, Arg.Any<CancellationToken>()).Returns([Total(Yesterday, 3), Total(Today, 4)]);

        var result = await _handler.Handle(new GetSolveRateMetricsQuery(Yesterday, Today, null), TestContext.Current.CancellationToken);

        result.Attempts.Should().Be(50);
        result.ActiveStudentDays.Should().Be(7);
        result.AttemptsPerActiveStudentPerDay.Should().Be(7.14m);
        result.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_NoActiveStudents_ReturnsNullRate()
    {
        _sessionRepository.CountAttemptsByDayAsync(Arg.Any<MetricsWindow>(), null, Arg.Any<CancellationToken>()).Returns([Total(Today, 5)]);

        var result = await _handler.Handle(new GetSolveRateMetricsQuery(Yesterday, Today, null), TestContext.Current.CancellationToken);

        result.AttemptsPerActiveStudentPerDay.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ZeroFillsDailyAttemptsAndActive()
    {
        _sessionRepository.CountAttemptsByDayAsync(Arg.Any<MetricsWindow>(), null, Arg.Any<CancellationToken>()).Returns([Total(Today, 5)]);
        _userActivityDayRepository.CountActiveStudentsByDayAsync(Yesterday, Today, Arg.Any<CancellationToken>()).Returns([Total(Today, 2)]);

        var result = await _handler.Handle(new GetSolveRateMetricsQuery(Yesterday, Today, null), TestContext.Current.CancellationToken);

        result.Daily.Should().Equal(new SolveRateDayResult(Yesterday, 0, 0), new SolveRateDayResult(Today, 5, 2));
    }

    private static DailyTotal Total(DateOnly day, long value) => new() { Day = day, Value = value };
}
