using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Teachers;

public sealed class TeachersEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/teachers";

    [Fact]
    public async Task Get_Admin_ReturnsTeachersOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        var admin = await ScopeTestData.SeedAdminAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, cancellationToken);

        using var response = await client.GetAsync(Route, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var teachers = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray().ToList();
        var ids = teachers.Select(x => x.GetProperty("id").GetGuid()).ToList();
        teachers.Should().Contain(x => x.GetProperty("id").GetGuid() == teacher.Id && x.GetProperty("displayName").GetString() == "Teacher");
        ids.Should().NotContain(admin.Id).And.NotContain(student.Id);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.GetAsync(Route, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
