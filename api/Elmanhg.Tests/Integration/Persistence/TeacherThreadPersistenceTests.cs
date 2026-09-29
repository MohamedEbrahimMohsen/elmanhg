using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherInbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class TeacherThreadPersistenceTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_StaleTeacherThread_ThrowsTeacherThreadModifiedConcurrently()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var teacherA = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var teacherB = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var thread = await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).Build());
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await firstContext.TeacherThreads.SingleAsync(x => x.Id == thread.Id, CancellationToken);
        var secondCopy = await secondContext.TeacherThreads.SingleAsync(x => x.Id == thread.Id, CancellationToken);
        firstCopy.Claim(teacherA.Id, DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync(CancellationToken);
        secondCopy.Claim(teacherB.Id, DateTimeOffset.UtcNow);

        var act = () => secondContext.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadModifiedConcurrently);
    }

    [Fact]
    public async Task SaveChanges_MarkRepliesRead_LeavesThreadVersionUnchanged()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var thread = await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).AnsweredBy(teacher.Id).Build());
        var versionBefore = (await TeacherInboxTestData.ReadThreadAsync(factory, thread.Id)).Version;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tracked = await context.TeacherThreads.Include(x => x.Messages).SingleAsync(x => x.Id == thread.Id, CancellationToken);
            tracked.MarkRepliesRead(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken);
        }

        var stored = await TeacherInboxTestData.ReadThreadAsync(factory, thread.Id);

        stored.Version.Should().Be(versionBefore);
        stored.Messages.Single(x => x.SenderId == teacher.Id).StudentReadAt.Should().NotBeNull();
    }
}
