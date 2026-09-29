using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public sealed class TeacherInboxRemindersEndpointTests(ApiFactory factory)
{
    private const string RemindersRoute = $"{Route}/reminders";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reminders_RecordedReminder_ListsThreadForSubjectTeacher()
    {
        var subjectId = await SeedSubjectAsync();
        var (_, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedRemindedThreadAsync(subjectId, new TeacherThreadBuilder());

        var body = await client.GetFromJsonAsync<JsonElement>(RemindersRoute, CancellationToken);

        var item = body.EnumerateArray().Should().ContainSingle().Subject;
        (item.GetProperty("threadId").GetGuid(), item.GetProperty("kind").GetString(), item.GetProperty("questionText").GetString()).Should().Be((thread.Id, "FirstReminder", "Why is F = ma?"));
    }

    [Fact]
    public async Task Reminders_OtherSubjectTeacher_ReturnsEmpty()
    {
        var subjectId = await SeedSubjectAsync();
        await SeedRemindedThreadAsync(subjectId, new TeacherThreadBuilder());
        var (_, client) = await SignedInTeacherForAsync(factory, await SeedSubjectAsync());

        var body = await client.GetFromJsonAsync<JsonElement>(RemindersRoute, CancellationToken);

        body.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Reminders_AnsweredThread_IsNotListed()
    {
        var subjectId = await SeedSubjectAsync();
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);
        await SeedRemindedThreadAsync(subjectId, new TeacherThreadBuilder().AnsweredBy(teacher.Id));

        var body = await client.GetFromJsonAsync<JsonElement>(RemindersRoute, CancellationToken);

        body.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Reminders_Student_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync(RemindersRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reminders_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(RemindersRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<TeacherThread> SeedRemindedThreadAsync(Guid subjectId, TeacherThreadBuilder builder)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedThreadAsync(factory, builder.ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.TeacherThreadSlaEvents.Add(TeacherThreadSlaEvent.Record(thread.Id, TeacherThreadSlaEventKind.FirstReminder, thread.SlaDueAt, thread.TeacherId, thread.SubmittedAt.AddHours(12)));
        await context.SaveChangesAsync(CancellationToken);
        return thread;
    }

    private Task<Guid> SeedSubjectAsync() => ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
}
