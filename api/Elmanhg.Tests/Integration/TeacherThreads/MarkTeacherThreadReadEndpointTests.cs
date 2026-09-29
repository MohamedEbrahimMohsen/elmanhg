using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherInbox;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class MarkTeacherThreadReadEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MarkRead_OwnAnsweredThread_ClearsNewReply()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (thread, teacherId) = await SeedAnsweredThreadAsync(student.Id);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.PostAsync($"{TeacherThreadTestData.Route}/{thread.Id}/read", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await client.GetFromJsonAsync<JsonElement>($"{TeacherThreadTestData.Route}/{thread.Id}", CancellationToken);
        body.GetProperty("hasUnreadReply").GetBoolean().Should().BeFalse();
        var stored = await TeacherInboxTestData.ReadThreadAsync(factory, thread.Id);
        stored.Messages.Single(x => x.SenderId == teacherId).StudentReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkRead_OtherStudentsThread_Returns404()
    {
        var owner = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (thread, _) = await SeedAnsweredThreadAsync(owner.Id);
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, other, CancellationToken);

        using var response = await client.PostAsync($"{TeacherThreadTestData.Route}/{thread.Id}/read", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("TEACHER_THREAD_NOT_FOUND");
    }

    [Fact]
    public async Task MarkRead_Teacher_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (thread, _) = await SeedAnsweredThreadAsync(student.Id);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.PostAsync($"{TeacherThreadTestData.Route}/{thread.Id}/read", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(TeacherThread Thread, Guid TeacherId)> SeedAnsweredThreadAsync(Guid studentId)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var thread = await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(studentId).WithContext(TeacherInboxTestData.ContextFor(subjectId)).AnsweredBy(teacher.Id).Build());
        return (thread, teacher.Id);
    }
}
