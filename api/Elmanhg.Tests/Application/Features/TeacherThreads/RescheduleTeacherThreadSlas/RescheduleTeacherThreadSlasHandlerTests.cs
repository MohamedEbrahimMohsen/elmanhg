using Core.DDD.Models;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.RescheduleTeacherThreadSlas;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.RescheduleTeacherThreadSlas;

public sealed class RescheduleTeacherThreadSlasHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(1);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly RescheduleTeacherThreadSlasHandler _handler;
    private Expression<Func<TeacherThread, bool>>? _filter;

    public RescheduleTeacherThreadSlasHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _handler = new RescheduleTeacherThreadSlasHandler(_teacherThreadRepository, Substitute.For<IExamPeriodRepository>(), new FakeRuntimeSettings(slaCalendar: new SlaCalendarOptions { SkipWeekends = true }), _timeProvider);
    }

    [Fact]
    public async Task Handle_StaleOpenThreads_ReschedulesAndSavesOnce()
    {
        List<TeacherThread> threads = [new TeacherThreadBuilder().Build(), new TeacherThreadBuilder().SubmittedAt(TeacherThreadBuilder.DefaultSubmittedAt.AddHours(2)).Build()];
        Page(threads);

        var rescheduled = await Handle(50);

        rescheduled.Should().Be(2);
        threads.Select(x => x.SlaSchedule()).Should().Equal(threads.Select(x => TeacherThreadSlaPolicies.CairoWeekends().ScheduleFrom(x.SlaWindowStartedAt)));
        threads.Should().OnlyContain(x => x.UpdationDate == Now);
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoStaleThreads_ReturnsZeroWithoutSaving()
    {
        Page([]);

        var rescheduled = await Handle(50);

        rescheduled.Should().Be(0);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesBatchSizeAndCurrentFingerprintFilter()
    {
        Page([]);

        await Handle(7);

        await _teacherThreadRepository.Received(1).FindPaginatedAsync(1, 7, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<TeacherThread, bool>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), false);
        var filter = _filter!.Compile();
        filter(new TeacherThreadBuilder().WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()).Build()).Should().BeFalse();
        filter(new TeacherThreadBuilder().Build()).Should().BeTrue();
        filter(new TeacherThreadBuilder().AnsweredBy(Guid.NewGuid()).Build()).Should().BeFalse();
    }

    private void Page(List<TeacherThread> threads) => _teacherThreadRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Do<Expression<Func<TeacherThread, bool>>?>(x => _filter = x), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
        .Returns(new PageData<TeacherThread> { Items = threads, PageNumber = 1, PageSize = threads.Count, TotalItems = threads.Count, TotalPages = 1 });

    private Task<int> Handle(int batchSize) => _handler.Handle(new RescheduleTeacherThreadSlasCommand(batchSize), TestContext.Current.CancellationToken);
}
