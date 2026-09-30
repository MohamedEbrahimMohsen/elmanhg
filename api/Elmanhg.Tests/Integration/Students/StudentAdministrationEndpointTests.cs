using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Users;
using FluentAssertions;
using System.Net;

namespace Elmanhg.Tests.Integration.Students;

public sealed class StudentAdministrationEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/students";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetProfile_Admin_ReturnsMaskedProfile()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{student.Id}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(CancellationToken)).Should().NotContain(student.Email);
        var body = await UsersTestData.ReadBodyAsync(response, CancellationToken);
        body.GetProperty("id").GetGuid().Should().Be(student.Id);
        body.GetProperty("maskedEmail").GetString().Should().Be($"{student.Email![0]}***@elmanhg.test");
        body.GetProperty("tier").GetString().Should().Be("Free");
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task GetProfile_TeacherId_Returns404()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{teacher.Id}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("STUDENT_NOT_FOUND");
    }

    [Fact]
    public async Task GetProfile_StudentCaller_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync($"{Route}/{student.Id}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetProgress_Admin_ReturnsSubjectsForStudent()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{student.Id}/progress", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await UsersTestData.ReadBodyAsync(response, CancellationToken);
        var subject = body.GetProperty("subjects").EnumerateArray().Single(x => x.GetProperty("subjectId").GetGuid() == subjectId);
        subject.GetProperty("masteredCount").GetInt32().Should().Be(0);
        body.GetProperty("weakSpots").GetProperty("lessons").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetProgress_StudentCaller_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync($"{Route}/{student.Id}/progress", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSessions_Admin_ReturnsEmptyPageForNewStudent()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{student.Id}/sessions", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UsersTestData.ReadBodyAsync(response, CancellationToken)).GetProperty("totalItems").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task GetSessions_PageSizeAboveMax_Returns422()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{Route}/{student.Id}/sessions?pageSize=51", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Contain("SESSION_HISTORY_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task GetSessions_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}/sessions", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken).ConfigureAwait(false);
    }
}
