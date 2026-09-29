using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public static class TeacherThreadTestData
{
    public const string Route = "/api/teacher-threads";

    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<(User Student, HttpClient Client)> SignedInAskTeacherStudentAsync(ApiFactory factory)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(student.Id).StartingAt(start).Build(), CancellationToken).ConfigureAwait(false);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(student.Id).WithPlan(SubscriptionPlan.AskTeacher).StartingAt(start).Build(), CancellationToken).ConfigureAwait(false);
        return (student, await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken).ConfigureAwait(false));
    }

    public static async Task<(Guid SubjectId, Guid LessonId)> SeedPublishedLessonAsync(ApiFactory factory)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        return (subjectId, lessonId);
    }

    public static MultipartFormDataContent QuestionForm(string text, Guid? lessonId = null, Guid? attemptId = null, string? imageFileName = null, string? imageContentType = null, byte[]? imageBytes = null)
    {
        var form = new MultipartFormDataContent { { new StringContent(text), "text" } };
        if (lessonId is { } lesson)
        {
            form.Add(new StringContent(lesson.ToString()), "lessonId");
        }

        if (attemptId is { } attempt)
        {
            form.Add(new StringContent(attempt.ToString()), "attemptId");
        }

        if (imageFileName is not null)
        {
            var image = new ByteArrayContent(imageBytes ?? PngBytes);
            image.Headers.ContentType = new MediaTypeHeaderValue(imageContentType ?? "image/png");
            form.Add(image, "image", imageFileName);
        }

        return form;
    }

    public static async Task<List<TeacherThread>> SeedThreadsAsync(ApiFactory factory, Guid studentId, Guid subjectId, int count, DateTimeOffset submittedAt)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var threadContext = new TeacherThreadContext(subjectId, "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);
        var threads = Enumerable.Range(0, count)
            .Select(index => new TeacherThreadBuilder().ForStudent(studentId).WithContext(threadContext).SubmittedAt(submittedAt.AddSeconds(index)).Build())
            .ToList();
        context.TeacherThreads.AddRange(threads);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return threads;
    }

    public static async Task<List<TeacherThread>> ReadThreadsAsync(ApiFactory factory, Guid studentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.TeacherThreads.Include(x => x.Messages).AsNoTracking().Where(x => x.StudentId == studentId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
