using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherThreads.TeacherThreadTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class TeacherThreadReadEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetList_ReturnsOwnThreadsNewestFirst()
    {
        var (subjectId, _) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInAskTeacherStudentAsync(factory);
        var (other, _) = await SignedInAskTeacherStudentAsync(factory);
        var older = (await SeedThreadsAsync(factory, student.Id, subjectId, 1, DateTimeOffset.UtcNow.AddHours(-2)))[0];
        var newer = (await SeedThreadsAsync(factory, student.Id, subjectId, 1, DateTimeOffset.UtcNow.AddHours(-1)))[0];
        await SeedThreadsAsync(factory, other.Id, subjectId, 1, DateTimeOffset.UtcNow);

        var body = await client.GetFromJsonAsync<JsonElement>($"{Route}?pageNumber=1&pageSize=20", CancellationToken);

        body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(newer.Id, older.Id);
        body.GetProperty("items")[0].GetProperty("questionText").GetString().Should().Be("Why is F = ma?");
        body.GetProperty("totalItems").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetList_ThreadPastSla_ReportsOverdue()
    {
        var (subjectId, _) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInAskTeacherStudentAsync(factory);
        await SeedThreadsAsync(factory, student.Id, subjectId, 1, DateTimeOffset.UtcNow.AddHours(-25));

        var body = await client.GetFromJsonAsync<JsonElement>(Route, CancellationToken);

        body.GetProperty("items")[0].GetProperty("isOverdue").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetList_PageSizeOverMax_Returns422()
    {
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await client.GetAsync($"{Route}?pageSize=51", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task GetById_OwnThread_ReturnsContextAndMessages()
    {
        var (subjectId, _) = await SeedPublishedLessonAsync(factory);
        var (student, client) = await SignedInAskTeacherStudentAsync(factory);
        var thread = (await SeedThreadsAsync(factory, student.Id, subjectId, 1, DateTimeOffset.UtcNow))[0];

        using var response = await client.GetAsync($"{Route}/{thread.Id}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("context").GetProperty("subjectId").GetGuid().Should().Be(subjectId);
        var message = body.GetProperty("messages").EnumerateArray().Should().ContainSingle().Subject;
        (message.GetProperty("text").GetString(), message.GetProperty("isFromStudent").GetBoolean()).Should().Be(("Why is F = ma?", true));
    }

    [Fact]
    public async Task GetById_OtherStudentsThread_Returns404()
    {
        var (subjectId, _) = await SeedPublishedLessonAsync(factory);
        var (other, _) = await SignedInAskTeacherStudentAsync(factory);
        var (_, client) = await SignedInAskTeacherStudentAsync(factory);
        var thread = (await SeedThreadsAsync(factory, other.Id, subjectId, 1, DateTimeOffset.UtcNow))[0];

        using var response = await client.GetAsync($"{Route}/{thread.Id}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_NOT_FOUND");
    }

    [Fact]
    public async Task GetList_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetList_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
