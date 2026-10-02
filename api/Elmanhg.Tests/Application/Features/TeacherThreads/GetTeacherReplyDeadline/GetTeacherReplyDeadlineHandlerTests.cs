using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.GetTeacherReplyDeadline;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.GetTeacherReplyDeadline;

public sealed class GetTeacherReplyDeadlineHandlerTests
{
    private static readonly DateTimeOffset ThursdayNoon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly IExamPeriodRepository _examPeriodRepository = Substitute.For<IExamPeriodRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    public GetTeacherReplyDeadlineHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(ThursdayNoon);
    }

    [Fact]
    public async Task Handle_SkipOnThursday_ReturnsSundayDeadlineAndSkipFlag()
    {
        var result = await Handle(skipWeekends: true);

        result.Should().Be(new TeacherReplyDeadlineResult(24, new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), true));
    }

    [Fact]
    public async Task Handle_SkipOff_ReturnsWallClockDeadline()
    {
        var result = await Handle(skipWeekends: false);

        result.Should().Be(new TeacherReplyDeadlineResult(24, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), false));
    }

    [Fact]
    public async Task Handle_OneDayWeekendOnThursday_ReturnsSaturdayDeadlineAndSkipFlag()
    {
        var result = await Handle(skipWeekends: true, weekendDays: "Friday");

        result.Should().Be(new TeacherReplyDeadlineResult(24, new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero), true));
    }

    [Fact]
    public async Task Handle_ExamPeriodCoversWeekend_ReturnsWallClockDeadline()
    {
        _examPeriodRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<Func<IQueryable<ExamPeriod>, IOrderedQueryable<ExamPeriod>>?>(), Arg.Any<bool>())
            .Returns([ExamPeriod.Create("Final exams", new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 3), Guid.NewGuid())]);

        var result = await Handle(skipWeekends: true);

        result.Should().Be(new TeacherReplyDeadlineResult(24, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), false));
    }

    private Task<TeacherReplyDeadlineResult> Handle(bool skipWeekends, string weekendDays = "Friday,Saturday") => new GetTeacherReplyDeadlineHandler(_examPeriodRepository, new FakeRuntimeSettings(slaCalendar: new SlaCalendarOptions { SkipWeekends = skipWeekends, WeekendDays = weekendDays }), _timeProvider).Handle(new GetTeacherReplyDeadlineQuery(), TestContext.Current.CancellationToken);
}
