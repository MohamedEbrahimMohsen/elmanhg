using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Mastery;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Browse;

public sealed class StudentSubjectEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync($"{BrowseTestData.Route}/subjects/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync($"{BrowseTestData.Route}/subjects/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_UnknownSubject_Returns404()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{BrowseTestData.Route}/subjects/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Get_SubjectWithProgress_ReturnsUnitsWithMasteryLessonCountAndBestScore()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var waves = await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 2, CancellationToken);
        var mechanics = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var forces = await ContentTestData.SeedLessonInStateAsync(factory, mechanics, "Forces", 1, LessonState.Published, CancellationToken);
        var energy = await ContentTestData.SeedLessonInStateAsync(factory, mechanics, "Energy", 2, LessonState.Published, CancellationToken);
        await ContentTestData.SeedLessonInStateAsync(factory, mechanics, "Draft", 3, LessonState.Draft, CancellationToken);
        var q1 = await QuestionTestData.SeedQuestionAsync(factory, forces, approved: true, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, forces, approved: true, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, energy, approved: true, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, energy, approved: true, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(client, forces, new Dictionary<Guid, string> { [q1] = "b" });
        await MasteryTestData.PracticeAsync(client, forces, new Dictionary<Guid, string> { [q1] = "b" });

        var body = await BrowseTestData.GetAsync(client, $"subjects/{subjectId}");

        (body.GetProperty("name").GetString(), body.GetProperty("servableCount").GetInt32(), body.GetProperty("masteredCount").GetInt32(), body.GetProperty("masteryPercent").GetInt32()).Should().Be(("Physics", 4, 1, 25));
        var units = body.GetProperty("units").EnumerateArray().ToList();
        units.Select(x => x.GetProperty("id").GetGuid()).Should().Equal(mechanics, waves);
        (units[0].GetProperty("lessonCount").GetInt32(), units[0].GetProperty("masteryPercent").GetInt32(), units[0].GetProperty("bestExamScorePercent").ValueKind).Should().Be((2, 25, JsonValueKind.Null));
        (units[1].GetProperty("lessonCount").GetInt32(), units[1].GetProperty("masteryPercent").GetInt32()).Should().Be((0, 0));
    }
}
