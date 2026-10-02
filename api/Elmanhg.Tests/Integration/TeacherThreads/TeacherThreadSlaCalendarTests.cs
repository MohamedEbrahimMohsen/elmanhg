using Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;
using Elmanhg.Application.TeacherThreads.RescheduleTeacherThreadSlas;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Configuration;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using static Elmanhg.Tests.Integration.SlaCalendars.SlaCalendarTestData;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

[Collection(RuntimeSettingsCollection.Name)]
public sealed class TeacherThreadSlaCalendarTests(ApiFactory factory) : IAsyncLifetime
{
    private const int BatchSize = 500;
    private static readonly DateTimeOffset ThursdayNoon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SundayMidnightCairo = new(2026, 10, 3, 21, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await ResetAsync();

    public async ValueTask DisposeAsync() => await ResetAsync();

    [Fact]
    public async Task Reschedule_SkipWeekendsTurnedOn_MovesOpenThreadPastWeekend()
    {
        var thread = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(ThursdayNoon));
        await SetOverrideAsync(factory, "slaCalendar.skipWeekends", "true");

        await RescheduleAllAsync();

        var stored = await ReadThreadAsync(factory, thread.Id);
        (stored.SlaWindowStartedAt, stored.FirstReminderDueAt, stored.SecondReminderDueAt, stored.SlaDueAt).Should().Be((ThursdayNoon, SundayMidnightCairo.AddHours(3), SundayMidnightCairo.AddHours(11), SundayMidnightCairo.AddHours(15)));
    }

    [Fact]
    public async Task Reschedule_ExamPeriodCoversWeekend_CountsEveryDay()
    {
        var thread = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(ThursdayNoon));
        await SetOverrideAsync(factory, "slaCalendar.skipWeekends", "true");
        using var admin = await ConfigurationTestData.AdminClientAsync(factory);
        using var created = await admin.PostAsJsonAsync(ExamPeriodsRoute, new { name = "Final exams", startDate = "2026-10-02", endDate = "2026-10-03" }, CancellationToken);

        await RescheduleAllAsync();

        created.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadThreadAsync(factory, thread.Id)).SlaDueAt.Should().Be(ThursdayNoon.AddHours(24));
    }

    [Fact]
    public async Task Reschedule_AnsweredThread_IsUnchanged()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var thread = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(ThursdayNoon).AnsweredBy(teacher.Id));
        await SetOverrideAsync(factory, "slaCalendar.skipWeekends", "true");

        await RescheduleAllAsync();

        var stored = await ReadThreadAsync(factory, thread.Id);
        (stored.SlaDueAt, stored.FirstReminderDueAt, stored.SlaScheduleFingerprint).Should().Be((thread.SlaDueAt, thread.FirstReminderDueAt, thread.SlaScheduleFingerprint));
    }

    [Fact]
    public async Task GetSlaDueIds_RescheduledThread_UsesStoredCalendarTimes()
    {
        var thread = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(ThursdayNoon));
        await SetOverrideAsync(factory, "slaCalendar.skipWeekends", "true");
        await RescheduleAllAsync();

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITeacherThreadRepository>();
        var saturday = await repository.GetSlaDueIdsAsync(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero), [], 100000, CancellationToken);
        var sunday = await repository.GetSlaDueIdsAsync(SundayMidnightCairo.AddHours(3), [], 100000, CancellationToken);

        saturday.Should().NotContain(thread.Id);
        sunday.Should().Contain(thread.Id);
    }

    [Fact]
    public async Task Reschedule_ThreadWithRecordedReminder_DoesNotRecordItAgain()
    {
        var thread = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(DateTimeOffset.UtcNow.AddDays(-10)));
        using (var seedScope = factory.Services.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.TeacherThreadSlaEvents.Add(TeacherThreadSlaEvent.Record(thread.Id, TeacherThreadSlaEventKind.FirstReminder, thread.SlaWindowStartedAt, thread.SlaDueAt, null, thread.FirstReminderDueAt));
            await context.SaveChangesAsync(CancellationToken);
        }

        await SetOverrideAsync(factory, "slaCalendar.skipWeekends", "true");
        await RescheduleAllAsync();
        await using (var processScope = factory.Services.CreateAsyncScope())
        {
            await processScope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessTeacherThreadSlaCommand(thread.Id), CancellationToken);
        }

        using var scope = factory.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeacherThreadSlaEvents.AsNoTracking().Where(x => x.ThreadId == thread.Id).ToListAsync(CancellationToken);
        events.Select(x => x.Kind).Should().BeEquivalentTo([TeacherThreadSlaEventKind.FirstReminder, TeacherThreadSlaEventKind.SecondReminder, TeacherThreadSlaEventKind.Breach]);
        events.Should().OnlyContain(x => x.WindowStartedAt == thread.SlaWindowStartedAt);
    }

    private async Task ResetAsync()
    {
        await ConfigurationTestData.ClearOverridesAsync(factory);
        await ClearExamPeriodsAsync(factory);
    }

    private async Task<TeacherThread> SeedAsync(TeacherThreadBuilder builder)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        return await SeedThreadAsync(factory, builder.ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
    }

    private async Task RescheduleAllAsync()
    {
        int rescheduled;
        do
        {
            await using var scope = factory.Services.CreateAsyncScope();
            rescheduled = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RescheduleTeacherThreadSlasCommand(BatchSize), CancellationToken);
        }
        while (rescheduled == BatchSize);
    }
}
