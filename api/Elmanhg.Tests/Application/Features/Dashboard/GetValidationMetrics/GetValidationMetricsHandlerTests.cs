using Core.Errors;
using Elmanhg.Application.Dashboard.GetValidationMetrics;
using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subjects;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetValidationMetrics;

public sealed class GetValidationMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetValidationMetricsHandler _handler;

    public GetValidationMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _questionRepository.GetDecisionStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new QuestionDecisionStats());
        _questionRepository.CountDecisionsByTeacherAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([]);
        _questionRepository.CountDecisionsByDayAsync(Arg.Any<MetricsWindow>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetValidationMetricsHandler(_questionRepository, _userRepository, _subjectRepository, _timeProvider, Options.Create(new DashboardOptions()));
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetValidationMetricsQuery(null, null, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _questionRepository.DidNotReceive().GetDecisionStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsBacklogAndRoundedMedian()
    {
        _questionRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Question, bool>>>()).Returns(17);
        _questionRepository.GetDecisionStatsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, null, Arg.Any<CancellationToken>()).Returns(new QuestionDecisionStats { Approved = 5, Rejected = 2, MedianSecondsToDecision = 7199.6 });

        var result = await _handler.Handle(new GetValidationMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        result.PendingBacklog.Should().Be(17);
        result.Approved.Should().Be(5);
        result.Rejected.Should().Be(2);
        result.MedianSecondsToDecision.Should().Be(7200);
        result.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_NamesTeachersAndOrdersByThroughput()
    {
        var quiet = User.CreateTeacher("Quiet", "quiet@example.com");
        var busy = User.CreateTeacher("Busy", "busy@example.com");
        _questionRepository.CountDecisionsByTeacherAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), null, Arg.Any<CancellationToken>()).Returns([new TeacherDecisionCount(quiet.Id, 1, 0), new TeacherDecisionCount(busy.Id, 3, 2)]);
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>()).Returns([quiet, busy]);

        var result = await _handler.Handle(new GetValidationMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        result.ByTeacher.Should().Equal(new TeacherThroughputResult(busy.Id, "Busy", 3, 2), new TeacherThroughputResult(quiet.Id, "Quiet", 1, 0));
    }

    [Fact]
    public async Task Handle_ZeroFillsDailyDecisions()
    {
        var from = new DateOnly(2026, 1, 13);
        var to = new DateOnly(2026, 1, 15);
        _questionRepository.CountDecisionsByDayAsync(Arg.Any<MetricsWindow>(), null, Arg.Any<CancellationToken>()).Returns([new DailyTotal { Day = new DateOnly(2026, 1, 14), Value = 4 }]);

        var result = await _handler.Handle(new GetValidationMetricsQuery(from, to, null), TestContext.Current.CancellationToken);

        result.DailyDecisions.Should().Equal(new DailyValueResult(from, 0), new DailyValueResult(new DateOnly(2026, 1, 14), 4), new DailyValueResult(to, 0));
    }
}
