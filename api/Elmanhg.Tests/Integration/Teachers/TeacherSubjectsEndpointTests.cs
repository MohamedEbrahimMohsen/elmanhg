using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Teachers;

public sealed class TeacherSubjectsEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_AdminAssignsTeacherSubject_Returns200AndPersists()
    {
        var (admin, teacherId, subjectId) = await ArrangeAsync();

        using var response = await admin.PostAsync(Route(teacherId, subjectId), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("teacherId").GetGuid().Should().Be(teacherId);
        body.GetProperty("subjectId").GetGuid().Should().Be(subjectId);
        (await CountLiveRowsAsync(teacherId, subjectId)).Should().Be(1);
    }

    [Fact]
    public async Task Post_AlreadyAssigned_Returns409()
    {
        var (admin, teacherId, subjectId) = await ArrangeAsync();
        await ScopeTestData.AssignAsync(factory, teacherId, subjectId, TestContext.Current.CancellationToken);

        using var response = await admin.PostAsync(Route(teacherId, subjectId), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_SUBJECT_ALREADY_ASSIGNED");
    }

    [Fact]
    public async Task Post_UnknownSubject_Returns404()
    {
        var (admin, teacherId, _) = await ArrangeAsync();

        using var response = await admin.PostAsync(Route(teacherId, Guid.NewGuid()), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Post_TargetIsStudent_Returns400UserNotTeacher()
    {
        var (admin, _, subjectId) = await ArrangeAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);

        using var response = await admin.PostAsync(Route(student.Id, subjectId), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("USER_NOT_TEACHER");
    }

    [Fact]
    public async Task Post_TeacherCaller_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.PostAsync(Route(teacher.Id, subjectId), null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CountLiveRowsAsync(teacher.Id, subjectId)).Should().Be(0);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsync(Route(Guid.NewGuid(), Guid.NewGuid()), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_AdminUnassigns_Returns200AndSoftDeletes()
    {
        var (admin, teacherId, subjectId) = await ArrangeAsync();
        await ScopeTestData.AssignAsync(factory, teacherId, subjectId, TestContext.Current.CancellationToken);

        using var response = await admin.DeleteAsync(Route(teacherId, subjectId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeacherSubjects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.TeacherId == teacherId && x.SubjectId == subjectId, TestContext.Current.CancellationToken);
        row.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NotAssigned_Returns404()
    {
        var (admin, teacherId, subjectId) = await ArrangeAsync();

        using var response = await admin.DeleteAsync(Route(teacherId, subjectId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_SUBJECT_NOT_ASSIGNED");
    }

    [Fact]
    public async Task Post_AfterUnassign_Returns200Reassigns()
    {
        var (admin, teacherId, subjectId) = await ArrangeAsync();
        using (var assigned = await admin.PostAsync(Route(teacherId, subjectId), null, TestContext.Current.CancellationToken))
        using (var unassigned = await admin.DeleteAsync(Route(teacherId, subjectId), TestContext.Current.CancellationToken))
        {
            assigned.StatusCode.Should().Be(HttpStatusCode.OK);
            unassigned.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var response = await admin.PostAsync(Route(teacherId, subjectId), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountLiveRowsAsync(teacherId, subjectId)).Should().Be(1);
    }

    private static string Route(Guid teacherId, Guid subjectId) => $"/api/teachers/{teacherId}/subjects/{subjectId}";

    private async Task<(HttpClient Admin, Guid TeacherId, Guid SubjectId)> ArrangeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken).ConfigureAwait(false);
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", cancellationToken).ConfigureAwait(false);
        var admin = await ScopeTestData.SignedInClientAsync(factory, await ScopeTestData.SeedAdminAsync(factory, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return (admin, teacher.Id, subjectId);
    }

    private async Task<int> CountLiveRowsAsync(Guid teacherId, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeacherSubjects.CountAsync(x => x.TeacherId == teacherId && x.SubjectId == subjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
