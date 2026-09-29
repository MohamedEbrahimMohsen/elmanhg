using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public static class TeacherInboxTestData
{
    public const string Route = "/api/teacher-inbox";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<(User Teacher, HttpClient Client)> SignedInTeacherForAsync(ApiFactory factory, Guid subjectId)
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken).ConfigureAwait(false);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, CancellationToken).ConfigureAwait(false);
        return (teacher, await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken).ConfigureAwait(false));
    }

    public static async Task<TeacherThread> SeedThreadAsync(ApiFactory factory, TeacherThread thread)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.TeacherThreads.Add(thread);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return thread;
    }

    public static async Task<TeacherThread> ReadThreadAsync(ApiFactory factory, Guid threadId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.TeacherThreads.Include(x => x.Messages).AsNoTracking().SingleAsync(x => x.Id == threadId, CancellationToken).ConfigureAwait(false);
    }

    public static TeacherThreadContext ContextFor(Guid subjectId) => new(subjectId, "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);
}
