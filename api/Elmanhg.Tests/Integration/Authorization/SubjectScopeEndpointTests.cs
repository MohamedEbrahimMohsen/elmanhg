using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Authorization;

public sealed class SubjectScopeEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_PhysicsTeacherOnMathContent_Returns403SubjectOutOfScope()
    {
        var (client, _, mathId) = await PhysicsTeacherAsync();

        using var response = await client.GetAsync(ContentRoute(mathId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadAsync(response, "code")).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task Post_PhysicsTeacherOnMathContent_Returns403SubjectOutOfScope()
    {
        var (client, _, mathId) = await PhysicsTeacherAsync();

        using var response = await client.PostAsync(ContentRoute(mathId), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadAsync(response, "code")).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task Get_PhysicsTeacherOnPhysicsContent_Returns200()
    {
        var (client, physicsId, _) = await PhysicsTeacherAsync();

        using var response = await client.GetAsync(ContentRoute(physicsId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync(response, "subjectId")).Should().Be(physicsId.ToString());
    }

    [Fact]
    public async Task Post_PhysicsTeacherOnPhysicsContent_Returns200()
    {
        var (client, physicsId, _) = await PhysicsTeacherAsync();

        using var response = await client.PostAsync(ContentRoute(physicsId), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync(response, "subjectId")).Should().Be(physicsId.ToString());
    }

    [Fact]
    public async Task Get_AfterUnassign_Returns403SubjectOutOfScope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var physicsId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", cancellationToken);
        await ScopeTestData.AssignAsync(factory, teacher.Id, physicsId, cancellationToken);
        using var teacherClient = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);
        using var adminClient = await ScopeTestData.SignedInClientAsync(factory, await ScopeTestData.SeedAdminAsync(factory, cancellationToken), cancellationToken);
        using (var before = await teacherClient.GetAsync(ContentRoute(physicsId), cancellationToken))
        {
            before.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var unassign = await adminClient.DeleteAsync($"/api/teachers/{teacher.Id}/subjects/{physicsId}", cancellationToken);
        using var response = await teacherClient.GetAsync(ContentRoute(physicsId), cancellationToken);

        unassign.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadAsync(response, "code")).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task Get_AdminOnMathContent_Returns200()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mathId = await ScopeTestData.SeedSubjectAsync(factory, "Math", cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, await ScopeTestData.SeedAdminAsync(factory, cancellationToken), cancellationToken);

        using var response = await client.GetAsync(ContentRoute(mathId), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_StudentOnMathContent_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var mathId = await ScopeTestData.SeedSubjectAsync(factory, "Math", cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, await ScopeTestData.SeedStudentAsync(factory, cancellationToken), cancellationToken);

        using var response = await client.PostAsync(ContentRoute(mathId), null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(ContentRoute(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string ContentRoute(Guid subjectId) => $"/api/test/subjects/{subjectId}/content";

    private async Task<(HttpClient Client, Guid PhysicsId, Guid MathId)> PhysicsTeacherAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken).ConfigureAwait(false);
        var physicsId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", cancellationToken).ConfigureAwait(false);
        var mathId = await ScopeTestData.SeedSubjectAsync(factory, "Math", cancellationToken).ConfigureAwait(false);
        await ScopeTestData.AssignAsync(factory, teacher.Id, physicsId, cancellationToken).ConfigureAwait(false);
        var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken).ConfigureAwait(false);
        return (client, physicsId, mathId);
    }

    private static async Task<string?> ReadAsync(HttpResponseMessage response, string property)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty(property).GetString();
    }
}
