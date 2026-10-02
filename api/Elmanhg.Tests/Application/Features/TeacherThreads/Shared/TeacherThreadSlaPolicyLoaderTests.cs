using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.Shared;

public sealed class TeacherThreadSlaPolicyLoaderTests
{
    private readonly IExamPeriodRepository _examPeriodRepository = Substitute.For<IExamPeriodRepository>();

    [Fact]
    public async Task LoadAsync_SettingsAndExamPeriods_BuildsPolicy()
    {
        Periods([ExamPeriod.Create("Final exams", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15), Guid.NewGuid())]);

        var policy = await TeacherThreadSlaPolicyLoader.LoadAsync(new FakeRuntimeSettings(slaCalendar: new SlaCalendarOptions { SkipWeekends = true }), _examPeriodRepository, TestContext.Current.CancellationToken);

        (policy.Calendar.SkipWeekends, policy.Calendar.TimeZone.Id).Should().Be((true, "Africa/Cairo"));
        policy.Calendar.WeekendDays.Should().Equal(DayOfWeek.Friday, DayOfWeek.Saturday);
        policy.Calendar.ExamPeriods.Should().Equal(new SlaDateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15)));
        (policy.ReplySla, policy.FirstReminderAfter, policy.SecondReminderAfter).Should().Be((TimeSpan.FromHours(24), TimeSpan.FromHours(12), TimeSpan.FromHours(20)));
        await _examPeriodRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<Func<IQueryable<ExamPeriod>, IOrderedQueryable<ExamPeriod>>?>(), true);
    }

    [Fact]
    public async Task LoadAsync_RepositoryReturnsNull_UsesNoExamPeriods()
    {
        Periods(null);

        var policy = await TeacherThreadSlaPolicyLoader.LoadAsync(new FakeRuntimeSettings(), _examPeriodRepository, TestContext.Current.CancellationToken);

        policy.Calendar.ExamPeriods.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_OverriddenWeekendDays_ParsesDayNames()
    {
        var runtimeSettings = new FakeRuntimeSettings().Set(SlaCalendarRuntimeSettings.WeekendDays, ["Saturday"]);

        var policy = await TeacherThreadSlaPolicyLoader.LoadAsync(runtimeSettings, _examPeriodRepository, TestContext.Current.CancellationToken);

        policy.Calendar.WeekendDays.Should().Equal(DayOfWeek.Saturday);
    }

    private void Periods(List<ExamPeriod>? periods) => _examPeriodRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<Func<IQueryable<ExamPeriod>, IOrderedQueryable<ExamPeriod>>?>(), Arg.Any<bool>()).Returns(periods);
}
