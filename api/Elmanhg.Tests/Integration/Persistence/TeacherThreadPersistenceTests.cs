using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.TeacherThreads;
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
    public async Task SaveChanges_StaleRate_ThrowsTeacherThreadModifiedConcurrently()
    {
        var (thread, _) = await SeedAnsweredThreadAsync(x => x);

        var act = () => SaveStaleCopyAsync(thread.Id, x => x.Rate(5, DateTimeOffset.UtcNow), x => x.Rate(4, DateTimeOffset.UtcNow));

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadModifiedConcurrently);
    }

    [Fact]
    public async Task SaveChanges_StaleFinalReply_ThrowsTeacherThreadModifiedConcurrently()
    {
        var (thread, teacherId) = await SeedAnsweredThreadAsync(x => x.FollowedUp());

        var act = () => SaveStaleCopyAsync(thread.Id, x => x.Reply(teacherId, "First final reply.", DateTimeOffset.UtcNow), x => x.Reply(teacherId, "Second final reply.", DateTimeOffset.UtcNow));

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

    private async Task<(TeacherThread Thread, Guid TeacherId)> SeedAnsweredThreadAsync(Func<TeacherThreadBuilder, TeacherThreadBuilder> shape)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken).ConfigureAwait(false);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken).ConfigureAwait(false);
        var builder = new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).AnsweredBy(teacher.Id);
        return (await TeacherInboxTestData.SeedThreadAsync(factory, shape(builder).Build()).ConfigureAwait(false), teacher.Id);
    }

    private async Task SaveStaleCopyAsync(Guid threadId, Action<TeacherThread> winner, Action<TeacherThread> loser)
    {
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await firstContext.TeacherThreads.Include(x => x.Messages).SingleAsync(x => x.Id == threadId, CancellationToken).ConfigureAwait(false);
        var secondCopy = await secondContext.TeacherThreads.Include(x => x.Messages).SingleAsync(x => x.Id == threadId, CancellationToken).ConfigureAwait(false);
        winner(firstCopy);
        await firstContext.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        loser(secondCopy);
        await secondContext.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }
}
