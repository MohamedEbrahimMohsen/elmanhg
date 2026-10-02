using Elmanhg.Tests.Integration.Configuration;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class TeacherReplyDeadlineEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/teacher-threads/reply-deadline";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_AsStudent_ReturnsDeadline()
    {
        var (_, client) = await TeacherThreadTestData.SignedInAskTeacherStudentAsync(factory);
        var before = TimeProvider.System.GetUtcNow();

        using var response = await client.GetAsync(Route, CancellationToken);

        var after = TimeProvider.System.GetUtcNow();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var slaDueAt = body.GetProperty("slaDueAt").GetDateTimeOffset();
        (body.GetProperty("replySlaHours").GetInt32(), body.GetProperty("skipsUncountedDays").GetBoolean()).Should().Be((24, false));
        slaDueAt.Should().BeOnOrAfter(before.AddHours(24)).And.BeOnOrBefore(after.AddHours(24));
    }

    [Fact]
    public async Task Get_AsTeacher_Returns403()
    {
        using var teacher = await ConfigurationTestData.TeacherClientAsync(factory);

        using var response = await teacher.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
