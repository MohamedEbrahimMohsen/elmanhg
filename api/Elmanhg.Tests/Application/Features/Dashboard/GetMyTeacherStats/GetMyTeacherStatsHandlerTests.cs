using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Dashboard.GetMyTeacherStats;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetMyTeacherStats;

public sealed class GetMyTeacherStatsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetMyTeacherStatsHandler _handler;

    public GetMyTeacherStatsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _currentUserService.UserId.Returns(_callerId);
        _questionRepository.GetDecisionStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new QuestionDecisionStats());
        _teacherThreadRepository.GetReplyStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new TeacherReplyStats());
        _handler = new GetMyTeacherStatsHandler(_questionRepository, _teacherThreadRepository, _currentUserService, _timeProvider, Options.Create(new DashboardOptions()));
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetMyTeacherStatsQuery(null, null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _questionRepository.DidNotReceive().GetDecisionStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _teacherThreadRepository.DidNotReceive().GetReplyStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsCallerDecisionAndReplyStats()
    {
        _questionRepository.GetDecisionStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, _callerId, Arg.Any<CancellationToken>()).Returns(new QuestionDecisionStats { Approved = 5, Rejected = 2, MedianSecondsToDecision = 7199.6 });
        _teacherThreadRepository.GetReplyStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, _callerId, Arg.Any<CancellationToken>()).Returns(new TeacherReplyStats { Replies = 20, RepliedWithinSla = 19, MedianReplySeconds = 3600.4 });

        var result = await _handler.Handle(new GetMyTeacherStatsQuery(null, null), TestContext.Current.CancellationToken);

        result.Approved.Should().Be(5);
        result.Rejected.Should().Be(2);
        result.MedianSecondsToDecision.Should().Be(7200);
        result.Replies.Should().Be(20);
        result.RepliedWithinSla.Should().Be(19);
        result.SlaComplianceRate.Should().Be(0.95m);
        result.MedianReplySeconds.Should().Be(3600);
        result.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_NoActivity_ReturnsNullRateAndMedians()
    {
        var result = await _handler.Handle(new GetMyTeacherStatsQuery(null, null), TestContext.Current.CancellationToken);

        result.SlaComplianceRate.Should().BeNull();
        result.MedianSecondsToDecision.Should().BeNull();
        result.MedianReplySeconds.Should().BeNull();
        result.Approved.Should().Be(0);
        result.Rejected.Should().Be(0);
        result.Replies.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ExplicitRange_QueriesCairoDayBoundariesForCaller()
    {
        var start = new DateTimeOffset(2026, 1, 4, 22, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 10, 22, 0, 0, TimeSpan.Zero);

        var result = await _handler.Handle(new GetMyTeacherStatsQuery(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10)), TestContext.Current.CancellationToken);

        await _questionRepository.Received(1).GetDecisionStatsAsync(start, end, null, _callerId, Arg.Any<CancellationToken>());
        await _teacherThreadRepository.Received(1).GetReplyStatsAsync(start, end, null, _callerId, Arg.Any<CancellationToken>());
        result.From.Should().Be(new DateOnly(2026, 1, 5));
        result.To.Should().Be(new DateOnly(2026, 1, 10));
    }

    [Fact]
    public async Task Handle_NoRange_DefaultsToLastThirtyCairoDays()
    {
        var result = await _handler.Handle(new GetMyTeacherStatsQuery(null, null), TestContext.Current.CancellationToken);

        result.To.Should().Be(new DateOnly(2026, 1, 15));
        result.From.Should().Be(new DateOnly(2025, 12, 17));
    }
}
