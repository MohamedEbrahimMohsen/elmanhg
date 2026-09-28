using Elmanhg.Domain.Subjects;
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

namespace Elmanhg.Tests.Integration.Content;

public sealed class SubjectsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/subjects";

    [Fact]
    public async Task Get_Admin_ReturnsSeededSubjectWithUnitCount()
    {
        var subjectId = await SeedSubjectAsync();
        await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 2, TestContext.Current.CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var item = items.EnumerateArray().Should().ContainSingle(x => x.GetProperty("id").GetGuid() == subjectId).Subject;
        item.GetProperty("unitCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Get_Teacher_ReturnsOnlyAssignedSubjects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var assignedId = await SeedSubjectAsync();
        var otherId = await SeedSubjectAsync();
        await ScopeTestData.AssignAsync(factory, teacher.Id, assignedId, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.GetAsync(Route, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(assignedId).And.NotContain(otherId);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_Admin_ReturnsUnitsInOrder()
    {
        var subjectId = await SeedSubjectAsync();
        var second = await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 2, TestContext.Current.CancellationToken);
        var first = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{subjectId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("units").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(first, second);
    }

    [Fact]
    public async Task GetById_Admin_ReturnsUnitLessonCounts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        var withLessons = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken);
        var withoutLessons = await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 2, cancellationToken);
        await ContentTestData.SeedLessonAsync(factory, withLessons, "Newton's laws", 1, [], cancellationToken);
        await ContentTestData.SeedLessonAsync(factory, withLessons, "Momentum", 2, [], cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{subjectId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var units = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("units").EnumerateArray().ToList();
        units.Single(x => x.GetProperty("id").GetGuid() == withLessons).GetProperty("lessonCount").GetInt32().Should().Be(2);
        units.Single(x => x.GetProperty("id").GetGuid() == withoutLessons).GetProperty("lessonCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetById_UnknownSubject_Returns404SubjectNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_UnassignedTeacher_Returns403SubjectOutOfScope()
    {
        var subjectId = await SeedSubjectAsync();
        using var client = await TeacherClientAsync(null);

        using var response = await client.GetAsync($"{Route}/{subjectId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task GetById_AssignedTeacher_Returns200()
    {
        var subjectId = await SeedSubjectAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.GetAsync($"{Route}/{subjectId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Admin_PersistsSubjectAndWritesAuditRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, new { name = "Chemistry" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        (await ContentTestData.ReadOrderAsync(factory, id, cancellationToken)).Should().BeGreaterThanOrEqualTo(1);
        var audit = await ContentTestData.ReadAuditAsync(factory, "Subject.Create", id, cancellationToken);
        audit.Outcome.Should().Be("Success");
        audit.Diff.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_EmptyName_Returns422SubjectNameRequired()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route, new { name = string.Empty }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("SUBJECT_NAME_REQUIRED");
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        using var client = await TeacherClientAsync(null);

        using var response = await client.PostAsJsonAsync(Route, new { name = "Chemistry" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(Route, new { name = "Chemistry" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Admin_RenamesSubject()
    {
        var subjectId = await SeedSubjectAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{subjectId}", new { name = "Biology" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadSubjectAsync(subjectId)).Name.Should().Be("Biology");
    }

    [Fact]
    public async Task Put_UnknownSubject_Returns404SubjectNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", new { name = "Biology" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Put_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route}/{subjectId}", new { name = "Biology" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadSubjectAsync(subjectId)).Name.Should().Be("Physics");
    }

    [Fact]
    public async Task PutPosition_MoveBeforeSibling_ReordersSubjects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await ContentTestData.SeedSubjectAsync(factory, "A", 1, cancellationToken);
        var second = await ContentTestData.SeedSubjectAsync(factory, "B", 2, cancellationToken);
        var position = await ContentTestData.ReadOrderAsync(factory, first, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{second}/position", new { position }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ContentTestData.ReadOrderAsync(factory, second, cancellationToken)).Should().BeLessThan(await ContentTestData.ReadOrderAsync(factory, first, cancellationToken));
    }

    [Fact]
    public async Task PutPosition_PositionZero_Returns422SubjectPositionInvalid()
    {
        var subjectId = await SeedSubjectAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route}/{subjectId}/position", new { position = 0 }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("SUBJECT_POSITION_INVALID");
    }

    [Fact]
    public async Task PutPosition_Student_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, cancellationToken);

        using var response = await client.PutAsJsonAsync($"{Route}/{subjectId}/position", new { position = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_SubjectWithoutUnits_SoftDeletesAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route}/{subjectId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadSubjectAsync(subjectId)).IsDeleted.Should().BeTrue();
        (await ContentTestData.ReadAuditAsync(factory, "Subject.Delete", subjectId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Delete_SubjectWithUnits_Returns400SubjectHasUnitsAndAuditsFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route}/{subjectId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_HAS_UNITS");
        (await ReadSubjectAsync(subjectId)).IsDeleted.Should().BeFalse();
        var audit = await ContentTestData.ReadAuditAsync(factory, "Subject.Delete", subjectId, cancellationToken);
        audit.Outcome.Should().Be("Failure");
        audit.ErrorCode.Should().Be("SUBJECT_HAS_UNITS");
    }

    [Fact]
    public async Task Delete_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.DeleteAsync($"{Route}/{subjectId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadSubjectAsync(subjectId)).IsDeleted.Should().BeFalse();
    }

    private Task<Guid> SeedSubjectAsync() => ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> TeacherClientAsync(Guid? assignedSubjectId)
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        if (assignedSubjectId is not null)
        {
            await ScopeTestData.AssignAsync(factory, teacher.Id, assignedSubjectId.Value, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        return await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<Subject> ReadSubjectAsync(Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subjects.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == subjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
