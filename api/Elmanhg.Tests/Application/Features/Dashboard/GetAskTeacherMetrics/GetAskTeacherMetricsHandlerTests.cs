using Core.Errors;
using Elmanhg.Application.Dashboard.GetAskTeacherMetrics;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetAskTeacherMetrics;

public sealed class GetAskTeacherMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherThreadSlaEventRepository _teacherThreadSlaEventRepository = Substitute.For<ITeacherThreadSlaEventRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetAskTeacherMetricsHandler _handler;

    public GetAskTeacherMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherThreadRepository.GetReplyStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new TeacherReplyStats());
        _handler = new GetAskTeacherMetricsHandler(_teacherThreadRepository, _teacherThreadSlaEventRepository, _subjectRepository, _timeProvider, Options.Create(new DashboardOptions()));
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetAskTeacherMetricsQuery(null, null, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _teacherThreadSlaEventRepository.DidNotReceive().CountBreachesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsCountsAndComplianceRate()
    {
        _teacherThreadRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<TeacherThread, bool>>>()).Returns(9, 6, 2);
        _teacherThreadSlaEventRepository.CountBreachesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, Arg.Any<CancellationToken>()).Returns(3);
        _teacherThreadRepository.GetReplyStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, null, Arg.Any<CancellationToken>()).Returns(new TeacherReplyStats { Replies = 20, RepliedWithinSla = 19, MedianReplySeconds = 3600.4 });

        var result = await _handler.Handle(new GetAskTeacherMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        result.OpenThreads.Should().Be(9);
        result.AwaitingReply.Should().Be(6);
        result.OverdueNow.Should().Be(2);
        result.SlaBreaches.Should().Be(3);
        result.Replies.Should().Be(20);
        result.RepliedWithinSla.Should().Be(19);
        result.SlaComplianceRate.Should().Be(0.95m);
        result.MedianReplySeconds.Should().Be(3600);
        result.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_NoReplies_ReturnsNullComplianceAndMedian()
    {
        var result = await _handler.Handle(new GetAskTeacherMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        result.SlaComplianceRate.Should().BeNull();
        result.MedianReplySeconds.Should().BeNull();
    }
}
