using Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class TeacherThreadSlaSweepTests(ApiFactory factory)
{
    private static readonly TimeSpan ReplySla = TimeSpan.FromHours(24);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Process_OverdueThread_RecordsEachStageOnce()
    {
        var thread = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(DateTimeOffset.UtcNow.AddHours(-25)));

        await ProcessAsync(thread.Id);
        await ProcessAsync(thread.Id);

        using var scope = factory.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeacherThreadSlaEvents.AsNoTracking().Where(x => x.ThreadId == thread.Id).ToListAsync(CancellationToken);
        events.Select(x => x.Kind).Should().BeEquivalentTo([TeacherThreadSlaEventKind.FirstReminder, TeacherThreadSlaEventKind.SecondReminder, TeacherThreadSlaEventKind.Breach]);
        events.Should().OnlyContain(x => x.SlaDueAt == thread.SlaDueAt);
    }

    [Fact]
    public async Task GetSlaDueIds_ListsDueThreadsAndSkipsRecordedAndAnswered()
    {
        var submittedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var due = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(submittedAt));
        var recorded = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(submittedAt));
        var answered = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(submittedAt).AnsweredBy(teacher.Id));
        using (var seedScope = factory.Services.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.TeacherThreadSlaEvents.Add(TeacherThreadSlaEvent.Record(recorded.Id, TeacherThreadSlaEventKind.FirstReminder, recorded.SlaDueAt, null, submittedAt.AddHours(12)));
            await context.SaveChangesAsync(CancellationToken);
        }

        using var scope = factory.Services.CreateScope();
        var ids = await scope.ServiceProvider.GetRequiredService<ITeacherThreadRepository>().GetSlaDueIdsAsync(due.SubmittedAt.AddHours(13), ReplySla, TimeSpan.FromHours(12), TimeSpan.FromHours(20), [], 100000, CancellationToken);

        ids.Should().Contain(due.Id);
        ids.Should().NotContain([recorded.Id, answered.Id]);
    }

    private async Task<TeacherThread> SeedAsync(TeacherThreadBuilder builder)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        return await SeedThreadAsync(factory, builder.ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
    }

    private async Task ProcessAsync(Guid threadId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessTeacherThreadSlaCommand(threadId), CancellationToken);
    }
}
