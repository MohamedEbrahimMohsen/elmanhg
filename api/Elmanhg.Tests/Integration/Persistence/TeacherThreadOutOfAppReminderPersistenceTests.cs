using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class TeacherThreadOutOfAppReminderPersistenceTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_SecondMarkerForSameThread_ViolatesUniqueIndex()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
        using (var firstScope = factory.Services.CreateScope())
        {
            var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
            first.TeacherThreadOutOfAppReminders.Add(TeacherThreadOutOfAppReminder.Record(thread.Id, TeacherThreadSlaEventKind.SecondReminder, thread.SlaDueAt, thread.SubmittedAt.AddHours(20)));
            await first.SaveChangesAsync(CancellationToken);
        }

        using var secondScope = factory.Services.CreateScope();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        second.TeacherThreadOutOfAppReminders.Add(TeacherThreadOutOfAppReminder.Record(thread.Id, TeacherThreadSlaEventKind.FirstReminder, thread.SlaDueAt, thread.SubmittedAt.AddHours(21)));

        var act = () => second.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.InnerException.Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}
