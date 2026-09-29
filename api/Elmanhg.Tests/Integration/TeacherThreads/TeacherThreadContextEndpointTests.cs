using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherThreads.TeacherThreadTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class TeacherThreadContextEndpointTests(ApiFactory factory)
{
    private const string ContextRoute = $"{Route}/context";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_PublishedLesson_ReturnsNames()
    {
        var (_, lessonId) = await SeedPublishedLessonAsync(factory);
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.GetAsync($"{ContextRoute}?lessonId={lessonId}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("subjectName").GetString(), body.GetProperty("unitName").GetString(), body.GetProperty("lessonName").GetString()).Should().Be(("Physics", "Mechanics", "Newton's laws"));
        body.GetProperty("questionId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_DraftLesson_Returns404LessonNotFound()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Draft, CancellationToken);
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.GetAsync($"{ContextRoute}?lessonId={lessonId}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Get_BaseWithoutAddOn_Returns403AskTeacherRequiresSubscription()
    {
        var (_, lessonId) = await SeedPublishedLessonAsync(factory);
        var (_, client) = await Sessions.SessionTestData.SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{ContextRoute}?lessonId={lessonId}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("ASK_TEACHER_REQUIRES_SUBSCRIPTION");
    }

    [Fact]
    public async Task Get_NoContext_Returns422()
    {
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.GetAsync(ContextRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_CONTEXT_INVALID");
    }

    [Fact]
    public async Task Get_Admin_Returns403()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.GetAsync($"{ContextRoute}?lessonId={Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
