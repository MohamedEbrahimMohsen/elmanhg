using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net.Http.Headers;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public static class TeacherInboxTestData
{
    public const string Route = "/api/teacher-inbox";

    public static readonly byte[] WebmBytes = [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81];

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

    public static MultipartFormDataContent VoiceForm(byte[] bytes, string fileName = "voice.webm", string contentType = "audio/webm", int durationSeconds = 12)
    {
        var audio = new ByteArrayContent(bytes);
        audio.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new MultipartFormDataContent { { audio, "audio", fileName }, { new StringContent(durationSeconds.ToString(CultureInfo.InvariantCulture)), "durationSeconds" } };
    }

    public static TeacherThreadContext ContextFor(Guid subjectId) => new(subjectId, "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);
}
